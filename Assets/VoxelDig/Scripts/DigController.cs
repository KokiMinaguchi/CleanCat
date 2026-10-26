using R3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoxelDig
{
    /// <summary>
    /// プレイヤーの操作を受け取って、マウスの下のボクセルを掘るコンポーネント
    /// ・左クリックで掘る（押しっぱなしで連続）
    /// ・1 / 2 / 3 キーで道具切り替え（ハンマー / ノミ / ドリル）
    /// ・R キーでやり直し
    ///
    /// PlayerInput（Behavior = Invoke Unity Events）の各イベントに
    /// OnPoint / OnDig / OnSelectTool1 / OnSelectTool2 / OnSelectTool3 / OnRestart を登録して使う
    /// </summary>
    public class DigController : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private DigBlock block;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private OrbitCamera orbitCamera;
        [SerializeField] private DigEffects effects;

        [Tooltip("掘る場所を示すカーソル（半透明のキューブなど。無くても動く）")]
        [SerializeField] private Transform digCursor;

        [Header("Raycast")]
        [Tooltip("ブロックが置かれているレイヤー")]
        [SerializeField] private LayerMask blockLayer = ~0;
        [SerializeField] private float rayDistance = 100f;

        [Header("道具")]
        [Tooltip("使える道具の一覧。0番目が最初に選ばれる")]
        [SerializeField] private DigTool[] tools =
        {
            new DigTool { displayName = "ハンマー", radius = 1.6f, power = 1, treasureDamage = 4, shakeStrength = 0.08f },
            new DigTool { displayName = "ノミ", radius = 0.5f, power = 3, treasureDamage = 1, shakeStrength = 0.03f },
            CreateDefaultDrill(),
        };

        [Tooltip("押しっぱなしのとき、次に掘るまでの間隔（秒）。0 で連続掘りしない")]
        [SerializeField] private float holdRepeatInterval = 0.15f;

        [Header("カーソルの色")]
        [SerializeField] private Color cursorNormalColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color cursorDangerColor = new Color(1f, 0.2f, 0.2f, 0.45f);

        // ---- UI に公開する状態 ----
        private readonly ReactiveProperty<int> currentToolIndex = new ReactiveProperty<int>(0);
        public ReadOnlyReactiveProperty<int> CurrentToolIndex => currentToolIndex;

        // 実際に使う道具（Inspector の tools をコピーして、強化の分を足したもの）
        // tools そのものを書き換えると、再生を止めたあとも Inspector の値が変わったままになるので、コピーを使う
        private DigTool[] runtimeTools;

        /// <summary>今持っている道具（強化込み）</summary>
        public DigTool CurrentTool => RuntimeTools[currentToolIndex.Value];

        /// <summary>道具の一覧（強化込み）</summary>
        public DigTool[] Tools => RuntimeTools;

        /// <summary>Inspector で設定した、強化する前の道具（強化の計算の元にする）</summary>
        public DigTool GetBaseTool(int index) => tools[index];

        /// <summary>道具ごとの間隔が 0 のときに使う、押しっぱなしの間隔</summary>
        public float DefaultHoldInterval => holdRepeatInterval;

        // ---- 掘った結果の通知（強化の素材を数えるのに使う）----
        private readonly Subject<DigResult> dug = new Subject<DigResult>();

        /// <summary>
        /// 1回掘るたびに通知される
        /// ※ DigResult は使い回しているので、受け取った処理の中ですぐに使い、保存しないこと
        /// </summary>
        public Observable<DigResult> Dug => dug;

        private DigTool[] RuntimeTools
        {
            get
            {
                // 最初に使われたときに、Inspector の値をコピーして作る
                if (runtimeTools == null || runtimeTools.Length != tools.Length) ResetRuntimeTools();
                return runtimeTools;
            }
        }

        private Vector2 pointerPosition; // マウスの画面上の位置
        private bool digPressed;         // このフレームでクリックされたか
        private bool digHeld;            // 押しっぱなしか
        private bool stalled;            // ドリルがお宝に引っかかって止まっているか（離すまで掘れない）
        private float repeatTimer;
        private Vector3 rayDirection;    // マウスの下へ向かう視線の向き（ドリルが掘り進む向き）
        private Quaternion cursorDefaultRotation;

        private readonly DigResult digResult = new DigResult(); // 毎回 new しないよう使い回す

        private Renderer cursorRenderer;
        private MaterialPropertyBlock cursorProps;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;

            if (digCursor != null)
            {
                // カーソル自身に Raycast が当たらないようにコライダーを切る
                if (digCursor.TryGetComponent(out Collider col)) col.enabled = false;
                cursorRenderer = digCursor.GetComponent<Renderer>();
                cursorProps = new MaterialPropertyBlock();
                cursorDefaultRotation = digCursor.rotation;
                digCursor.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            // 無効になっている間に溜まった入力を捨てる
            // （「掘る」ボタンを押したクリックで、いきなり掘ってしまわないように）
            digPressed = false;
            digHeld = false;
            stalled = false;
        }

        private void OnDisable()
        {
            if (digCursor != null) digCursor.gameObject.SetActive(false);
            // ドリルの音が鳴りっぱなしにならないように止める
            if (effects != null) effects.SetToolLoop(null, false);
        }

        private void OnDestroy()
        {
            currentToolIndex.Dispose();
            dug.Dispose();
        }

        // ---------------------------------------------------------------
        // 強化（ToolUpgradeSystem から呼ばれる）
        // ---------------------------------------------------------------

        /// <summary>強化を全部外して、Inspector の値に戻す</summary>
        public void ResetRuntimeTools()
        {
            runtimeTools = new DigTool[tools.Length];
            for (int i = 0; i < tools.Length; i++)
            {
                runtimeTools[i] = tools[i].Clone();
            }
        }

        /// <summary>
        /// 強化した道具に差し替える
        /// </summary>
        /// <param name="index">何番目の道具か</param>
        /// <param name="tool">強化した値の入った道具（GetBaseTool(index).Clone() に強化を足したもの）</param>
        public void SetRuntimeTool(int index, DigTool tool)
        {
            if (index < 0 || index >= RuntimeTools.Length || tool == null) return;
            runtimeTools[index] = tool;
        }

        /// <summary>
        /// 掘る対象のブロックを差し替える（DigSessionManager から呼ばれる）
        /// </summary>
        public void SetBlock(DigBlock newBlock)
        {
            block = newBlock;
        }

        // ---------------------------------------------------------------
        // 入力（PlayerInput の Unity Events から呼ばれる）
        // ---------------------------------------------------------------

        /// <summary>マウスの位置（Pointer/position）</summary>
        public void OnPoint(InputAction.CallbackContext context)
        {
            pointerPosition = context.ReadValue<Vector2>();
        }

        /// <summary>掘るボタン（マウス左ボタン）</summary>
        public void OnDig(InputAction.CallbackContext context)
        {
            // 無効中（探索モード）のクリックは無視する。離したときの処理だけは常に行う
            if (context.performed && isActiveAndEnabled)
            {
                // ここで直接掘らず、Update で処理する
                // （入力イベントの中では UI の上かどうかの判定が正しく取れないため）
                digPressed = true;
                digHeld = true;
                stalled = false;
            }
            else if (context.canceled)
            {
                digHeld = false;
                stalled = false; // 離したら、引っかかっていたドリルも元に戻る
            }
        }

        public void OnSelectTool1(InputAction.CallbackContext context)
        {
            if (context.performed) SelectTool(0);
        }

        public void OnSelectTool2(InputAction.CallbackContext context)
        {
            if (context.performed) SelectTool(1);
        }

        public void OnSelectTool3(InputAction.CallbackContext context)
        {
            if (context.performed) SelectTool(2);
        }

        public void OnRestart(InputAction.CallbackContext context)
        {
            if (context.performed && isActiveAndEnabled && block != null) block.Generate();
        }

        /// <summary>道具を切り替える（UI のボタンからも呼べるよう public）</summary>
        public void SelectTool(int index)
        {
            if (index < 0 || index >= tools.Length) return;
            currentToolIndex.Value = index;
            stalled = false;
        }

        /// <summary>ドリルの初期設定（Inspector の右クリックメニューからも追加できる）</summary>
        private static DigTool CreateDefaultDrill()
        {
            return new DigTool
            {
                displayName = "ドリル",
                shape = DigShape.Tunnel,
                radius = 0.9f,        // 細めの穴
                depth = 4,            // 1回で奥へ4マス掘り進む
                power = 1,            // 土は一瞬、石は少し時間がかかる
                treasureDamage = 2,
                shakeStrength = 0.015f, // 細かくブルブル揺れる
                holdInterval = 0.05f, // 押している間、とても速く掘り続ける
                stopOnTreasure = true,
            };
        }

        /// <summary>
        /// 既にシーンに置いてある DigController にドリルを追加する
        /// （tools の一覧はシーンに保存されるので、スクリプトの初期値を変えても既存のシーンには入らないため）
        /// Inspector の DigController を右クリック →「道具/ドリルを追加」
        /// </summary>
        [ContextMenu("道具/ドリルを追加")]
        private void AddDrillTool()
        {
            foreach (DigTool t in tools)
            {
                if (t != null && t.shape == DigShape.Tunnel)
                {
                    Debug.Log("[DigController] ドリル（Tunnel の道具）は既に入っています", this);
                    return;
                }
            }

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Add Drill Tool");
#endif
            var list = new System.Collections.Generic.List<DigTool>(tools) { CreateDefaultDrill() };
            tools = list.ToArray();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Debug.Log($"[DigController] ドリルを {tools.Length} 番目の道具として追加しました", this);
        }

        // ---------------------------------------------------------------
        // 更新
        // ---------------------------------------------------------------

        private void Update()
        {
            if (block == null) return;

            DigTool tool = CurrentTool;
            bool canDig = block.State.CurrentValue == DigState.Digging;
            bool overUi = UiPointerUtility.IsOverUi(pointerPosition);

            // マウスの下にあるボクセルを調べる
            bool hasTarget = false;
            Vector3Int targetCell = default;
            if (canDig && !overUi)
            {
                Ray ray = targetCamera.ScreenPointToRay(pointerPosition);
                rayDirection = ray.direction;
                if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, blockLayer))
                {
                    hasTarget = block.TryGetHitCell(hit, out targetCell);
                }
            }

            UpdateCursor(hasTarget, targetCell);

            // 押しっぱなしの間隔：道具ごとの設定があればそちらを使う（ドリルは速く、ハンマーはゆっくり）
            float interval = tool.holdInterval > 0f ? tool.holdInterval : holdRepeatInterval;

            // 掘るかどうかの判定（クリックした瞬間 + 押しっぱなしで一定間隔ごと）
            bool shouldDig = false;
            if (digPressed)
            {
                shouldDig = true;
                repeatTimer = interval;
            }
            else if (digHeld && !stalled && interval > 0f)
            {
                repeatTimer -= Time.deltaTime;
                if (repeatTimer <= 0f)
                {
                    shouldDig = true;
                    // 引き算で次の時間を決める（フレームが重くても、掘る回数が減りにくい）
                    repeatTimer += interval;
                    if (repeatTimer <= 0f) repeatTimer = interval;
                }
            }
            digPressed = false;

            if (shouldDig && hasTarget)
            {
                Dig(targetCell);
            }

            // ドリルの「ギュイーン」音：押していて、掘れる場所を狙っている間だけ鳴らす
            if (effects != null)
            {
                bool loopPlaying = digHeld && !stalled && hasTarget && tool.loopClip != null;
                effects.SetToolLoop(tool.loopClip, loopPlaying);
            }
        }

        private void Dig(Vector3Int cell)
        {
            DigTool tool = CurrentTool;
            block.Dig(cell, rayDirection, tool, digResult);

            if (!digResult.HasAnyHit) return;

            // 掘った結果を知らせる（強化の素材を数える ToolUpgradeSystem などが受け取る）
            dug.OnNext(digResult);

            // ドリルがお宝に当たったら、離すまで止める（押しっぱなしでお宝を削り続けないように）
            if (digResult.hitTreasure && tool.stopOnTreasure) stalled = true;

            // エフェクト・効果音・カメラの揺れ
            if (effects != null) effects.PlayDig(digResult, tool.digClip);
            if (orbitCamera != null)
            {
                // お宝を叩いたときは強めに揺らして「やってしまった感」を出す
                float strength = digResult.hitTreasure ? tool.shakeStrength * 2.5f : tool.shakeStrength;
                orbitCamera.Shake(strength);
            }
        }

        /// <summary>
        /// 掘る範囲をカーソルで表示する。お宝を直接狙っているときは赤くする
        /// </summary>
        private void UpdateCursor(bool visible, Vector3Int cell)
        {
            if (digCursor == null) return;

            digCursor.gameObject.SetActive(visible);
            if (!visible) return;

            DigTool tool = CurrentTool;
            Vector3 cellCenter = block.CellToWorld(cell);
            // 道具の範囲の直径に合わせる（少しだけ大きくしてボクセルに埋もれないように）
            float diameter = Mathf.Max(1f, tool.radius * 2f) * block.VoxelSize * 1.02f;

            if (tool.shape == DigShape.Tunnel && tool.depth > 1 && rayDirection.sqrMagnitude > 0.0001f)
            {
                // ドリル：視線の奥へ伸びた細長い箱にして、「ここまで掘り進む」を見せる
                Vector3 dir = rayDirection.normalized;
                float length = (tool.depth - 1) * block.VoxelSize + diameter;
                digCursor.rotation = Quaternion.LookRotation(dir);
                // 手前の端が叩いたマスの手前に来るよう、長さの半分だけ奥へずらす
                digCursor.position = cellCenter + dir * (length * 0.5f - diameter * 0.5f);
                digCursor.localScale = new Vector3(diameter, diameter, length);
            }
            else
            {
                digCursor.rotation = cursorDefaultRotation;
                digCursor.position = cellCenter;
                digCursor.localScale = Vector3.one * diameter;
            }

            if (cursorRenderer != null)
            {
                bool danger = block.GetVoxel(cell) == VoxelType.Treasure;
                cursorRenderer.GetPropertyBlock(cursorProps);
                cursorProps.SetColor(BaseColorId, danger ? cursorDangerColor : cursorNormalColor);
                cursorRenderer.SetPropertyBlock(cursorProps);
            }
        }
    }
}
