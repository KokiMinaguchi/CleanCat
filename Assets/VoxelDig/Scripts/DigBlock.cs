using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace VoxelDig
{
    /// <summary>ゲームの進行状態</summary>
    public enum DigState
    {
        Digging,   // 掘っている最中
        Revealing, // クリア演出中（残りの土が崩れていく）
        Cleared,   // クリア
        Failed,    // ダメージが限界を超えて失敗
    }

    /// <summary>壊れたボクセル1個の情報（エフェクト・素材の獲得用）</summary>
    public struct BrokenVoxel
    {
        public Vector3 worldPosition;
        public Color32 color;
        public VoxelType type; // 壊れる前の種類（土 or 石）。強化の素材を数えるのに使う
    }

    /// <summary>1回掘った結果（エフェクトや効果音の出し分けに使う）</summary>
    public class DigResult
    {
        public readonly List<BrokenVoxel> broken = new List<BrokenVoxel>();
        public bool hitHard;     // 石を叩いたが壊れなかった
        public bool hitTreasure; // お宝を叩いてしまった

        public bool HasAnyHit => broken.Count > 0 || hitHard || hitTreasure;

        public void Clear()
        {
            broken.Clear();
            hitHard = false;
            hitTreasure = false;
        }
    }

    /// <summary>
    /// CSV のモデルを土の塊に埋め込み、掘る処理・クリア判定・失敗判定を担当するコンポーネント
    /// </summary>
    public class DigBlock : MonoBehaviour
    {
        [Header("モデル")]
        [Tooltip("埋めるボクセルモデルの CSV（kinoko.csv など）。TextAsset として読み込む")]
        [SerializeField] private TextAsset voxelCsv;

        [Tooltip("頂点カラーで描画するマテリアル（VoxelVertexColorLit シェーダー）")]
        [SerializeField] private Material voxelMaterial;

        [Tooltip("1ボクセルの大きさ（メートル）")]
        [SerializeField] private float voxelSize = 0.1f;

        [Tooltip("1チャンクの大きさ（マス）。小さいほど掘ったときの作り直しが軽い")]
        [SerializeField] private int chunkSize = 16;

        [Header("生成")]
        [SerializeField] private BlockGenerationSettings generation = new BlockGenerationSettings();

        [Tooltip("ON なら毎回ちがう形のブロックになる")]
        [SerializeField] private bool useRandomSeed = true;

        [SerializeField] private int seed = 12345;

        [Tooltip("ON なら Start で自動生成する。DigSessionManager から生成するプレハブでは OFF にする")]
        [SerializeField] private bool generateOnStart = true;

        [Header("ルール")]
        [Tooltip("発掘率（お宝に接した土をどれだけ取り除いたか）がこの割合に達したら、残りの土を全部壊してクリアにする。1 で全部掘るまでクリアにならない")]
        [Range(0.1f, 1f)]
        [SerializeField] private float clearThreshold = 0.9f;

        [Tooltip("ダメージがこの値に達したら失敗")]
        [SerializeField] private int maxDamage = 60;

        [Tooltip("クリア時に残りの土が崩れ落ちる演出の時間（秒）")]
        [SerializeField] private float revealDuration = 1.5f;

        // ---- 外部（UI・エフェクト）に公開する状態。R3 の ReactiveProperty で変化を通知する ----
        private readonly ReactiveProperty<DigState> state = new ReactiveProperty<DigState>(DigState.Digging);
        private readonly ReactiveProperty<float> progress = new ReactiveProperty<float>(0f);
        private readonly ReactiveProperty<int> damage = new ReactiveProperty<int>(0);
        private readonly Subject<IReadOnlyList<BrokenVoxel>> revealBroken = new Subject<IReadOnlyList<BrokenVoxel>>();

        public ReadOnlyReactiveProperty<DigState> State => state;
        public ReadOnlyReactiveProperty<float> Progress => progress;   // 0〜1（お宝に接した土をどれだけ取り除いたか）
        public ReadOnlyReactiveProperty<int> Damage => damage;
        public Observable<IReadOnlyList<BrokenVoxel>> RevealBroken => revealBroken; // クリア演出で崩れた土
        public int MaxDamage => maxDamage;
        public float ClearThreshold => clearThreshold; // UI でクリアラインを表示したいとき用

        /// <summary>ブロックの中心（ワールド座標）。カメラの注視点に使う</summary>
        public Vector3 WorldCenter => transform.position;

        /// <summary>ブロックのだいたいの大きさ（メートル）</summary>
        public float WorldSize => grid == null ? 1f : grid.Size.magnitude * voxelSize;

        private VoxelModel model;
        private TextAsset loadedCsv; // model をどの CSV から読み込んだか（CSV が変わったら読み直す）
        private VoxelGrid grid;
        private Transform chunkRoot;
        private VoxelChunk[,,] chunks;
        private Vector3Int chunkCount;
        private CancellationTokenSource revealCts;

        private void Start()
        {
            // Setup から既に生成済みなら、二重に生成しない
            if (generateOnStart && grid == null) Generate();
        }

        /// <summary>
        /// 埋めるモデルと大きさを外から指定する（Generate の前に呼ぶ）
        /// </summary>
        /// <param name="csv">埋めるボクセルモデルの CSV（null ならプレハブに設定されたものを使う）</param>
        /// <param name="fitWorldSize">
        /// ブロック全体の一番長い辺がこの長さ（メートル）になるよう voxelSize を自動計算する。
        /// 0 以下なら voxelSize はそのまま
        /// </param>
        public void Setup(TextAsset csv, float fitWorldSize)
        {
            if (csv != null) voxelCsv = csv;
            LoadModelIfNeeded();
            if (model == null) return;

            if (fitWorldSize > 0f)
            {
                // ブロックの大きさ（マス）= モデル + 両側の土（padding）+ 両側の空気1マス
                int margin = (generation.padding + 1) * 2;
                int maxCells = Mathf.Max(model.size.x, Mathf.Max(model.size.y, model.size.z)) + margin;
                voxelSize = fitWorldSize / maxCells;
            }
        }

        /// <summary>CSV がまだ読み込まれていない、または差し替えられたときだけ読み込む</summary>
        private void LoadModelIfNeeded()
        {
            if (voxelCsv == null) return;
            if (model != null && loadedCsv == voxelCsv) return;

            model = VoxelCsvLoader.Load(voxelCsv.text);
            loadedCsv = voxelCsv;
        }

        private void LateUpdate()
        {
            // 1フレームに何回掘っても、メッシュの作り直しはフレームの最後に1回だけにする
            if (chunks == null) return;
            foreach (var chunk in chunks)
            {
                if (chunk.IsDirty) chunk.Rebuild();
            }
        }

        private void OnDestroy()
        {
            revealCts?.Cancel();
            revealCts?.Dispose();
            state.Dispose();
            progress.Dispose();
            damage.Dispose();
            revealBroken.Dispose();
        }

        // ---------------------------------------------------------------
        // 生成
        // ---------------------------------------------------------------

        /// <summary>
        /// ブロックを（作り直して）新しいゲームを始める
        /// </summary>
        public void Generate()
        {
            if (voxelCsv == null)
            {
                Debug.LogError("[DigBlock] voxelCsv が設定されていません", this);
                return;
            }

            // 実行中の演出があれば止める
            revealCts?.Cancel();
            revealCts?.Dispose();
            revealCts = null;

            // CSV の読み込みは最初の1回だけ（差し替えられたときは読み直す）
            LoadModelIfNeeded();

            int usedSeed = useRandomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : seed;
            grid = VoxelGrid.Create(model, generation, usedSeed);

            CreateChunks();

            // 状態をリセット
            damage.Value = 0;
            progress.Value = 0f;
            state.Value = DigState.Digging;
        }

        private void CreateChunks()
        {
            // 前回のチャンクを削除
            if (chunkRoot != null) Destroy(chunkRoot.gameObject);

            chunkRoot = new GameObject("Chunks").transform;
            chunkRoot.SetParent(transform, false);
            // ブロックの中心がこのオブジェクトの位置に来るようにずらす
            chunkRoot.localPosition = -(Vector3)grid.Size * voxelSize * 0.5f;

            chunkCount = new Vector3Int(
                Mathf.CeilToInt(grid.Size.x / (float)chunkSize),
                Mathf.CeilToInt(grid.Size.y / (float)chunkSize),
                Mathf.CeilToInt(grid.Size.z / (float)chunkSize));

            chunks = new VoxelChunk[chunkCount.x, chunkCount.y, chunkCount.z];

            for (int z = 0; z < chunkCount.z; z++)
            for (int y = 0; y < chunkCount.y; y++)
            for (int x = 0; x < chunkCount.x; x++)
            {
                var go = new GameObject($"Chunk_{x}_{y}_{z}",
                    typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(VoxelChunk));
                go.layer = gameObject.layer; // Raycast のレイヤー判定用に親と揃える
                go.transform.SetParent(chunkRoot, false);

                var chunk = go.GetComponent<VoxelChunk>();
                chunk.Initialize(grid, new Vector3Int(x, y, z) * chunkSize, chunkSize, voxelSize, voxelMaterial);
                chunks[x, y, z] = chunk;
            }
        }

        // ---------------------------------------------------------------
        // 座標変換
        // ---------------------------------------------------------------

        /// <summary>
        /// Raycast が当たった面から「叩いたボクセル」のマス座標を求める
        /// 当たった点は面の上なので、法線と逆方向に半マス押し込んでからマスに変換する
        /// </summary>
        public bool TryGetHitCell(RaycastHit hit, out Vector3Int cell)
        {
            cell = default;
            if (grid == null) return false;

            Vector3 inside = hit.point - hit.normal * (voxelSize * 0.5f);
            cell = WorldToCell(inside);
            return grid.InBounds(cell);
        }

        public Vector3Int WorldToCell(Vector3 world)
        {
            Vector3 local = chunkRoot.InverseTransformPoint(world) / voxelSize;
            return Vector3Int.FloorToInt(local);
        }

        /// <summary>マスの中心のワールド座標</summary>
        public Vector3 CellToWorld(Vector3Int cell)
        {
            return chunkRoot.TransformPoint(((Vector3)cell + Vector3.one * 0.5f) * voxelSize);
        }

        public VoxelType GetVoxel(Vector3Int cell) => grid?.GetVoxel(cell.x, cell.y, cell.z) ?? VoxelType.Empty;

        public float VoxelSize => voxelSize;

        // ---------------------------------------------------------------
        // 掘る
        // ---------------------------------------------------------------

        /// <summary>
        /// 指定したマスを中心に、道具の範囲を掘る
        /// </summary>
        /// <param name="center">叩いたマス</param>
        /// <param name="tool">使った道具</param>
        /// <param name="result">結果を書き込む入れ物（呼び出し側で使い回す）</param>
        public void Dig(Vector3Int center, DigTool tool, DigResult result)
        {
            Dig(center, Vector3.zero, tool, result);
        }

        /// <summary>
        /// 指定したマスから、道具の掘り方で掘る
        /// ・Sphere：center を中心に丸く削る
        /// ・Tunnel：center から worldDirection（カメラの視線の向き）へ、tool.depth マス分トンネルを掘る
        /// </summary>
        /// <param name="center">叩いたマス</param>
        /// <param name="worldDirection">掘り進む向き（ワールド座標。Tunnel のときだけ使う）</param>
        /// <param name="tool">使った道具</param>
        /// <param name="result">結果を書き込む入れ物（呼び出し側で使い回す）</param>
        public void Dig(Vector3Int center, Vector3 worldDirection, DigTool tool, DigResult result)
        {
            result.Clear();
            if (state.Value != DigState.Digging || grid == null) return;

            if (tool.shape == DigShape.Tunnel && tool.depth > 1 && worldDirection.sqrMagnitude > 0.0001f)
            {
                DigTunnel(center, worldDirection, tool, result);
            }
            else
            {
                DigSphere(center, tool, result, true);
            }

            FinishDig(tool, result);
        }

        /// <summary>
        /// ドリル：叩いたマスから奥へ向かって、小さな球を少しずつずらしながら削っていく
        /// </summary>
        private void DigTunnel(Vector3Int center, Vector3 worldDirection, DigTool tool, DigResult result)
        {
            // ワールドの向き → ブロックの中（マス目）の向きに直す
            // （ブロックが回転していても、ちゃんと視線の奥へ掘れるように）
            Vector3 direction = chunkRoot.InverseTransformDirection(worldDirection).normalized;

            // 叩いたマスの中心から出発して、1マスずつ奥へ進む
            Vector3 start = (Vector3)center + Vector3.one * 0.5f;
            Vector3Int lastCell = new Vector3Int(int.MinValue, 0, 0);

            for (int step = 0; step < tool.depth; step++)
            {
                Vector3 point = start + direction * step;
                Vector3Int cell = Vector3Int.FloorToInt(point);

                // 斜めに進むと同じマスを2回通ることがあるので、そのときは飛ばす
                if (cell == lastCell) continue;
                lastCell = cell;

                // 1歩目（叩いたマス）だけは、お宝を直接叩いた扱いにする
                DigSphere(cell, tool, result, step == 0);

                // お宝に当たったら、それより奥は掘らない（お宝を貫通しないように）
                if (result.hitTreasure) break;
            }
        }

        /// <summary>
        /// center を中心に、道具の半径の球の範囲を削る
        /// </summary>
        /// <param name="centerIsDirectHit">true なら、center のお宝は埋まっていてもダメージを受ける</param>
        private void DigSphere(Vector3Int center, DigTool tool, DigResult result, bool centerIsDirectHit)
        {
            int r = Mathf.CeilToInt(tool.radius);
            float r2 = tool.radius * tool.radius;

            for (int dz = -r; dz <= r; dz++)
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                // 球の範囲外はスキップ
                if (dx * dx + dy * dy + dz * dz > r2) continue;

                int x = center.x + dx, y = center.y + dy, z = center.z + dz;
                if (!grid.InBounds(x, y, z)) continue;

                int i = grid.Index(x, y, z);
                switch (grid.types[i])
                {
                    case VoxelType.Dirt:
                    case VoxelType.Stone:
                        DamageSoil(i, x, y, z, tool.power, result);
                        break;

                    case VoxelType.Treasure:
                        // 直接叩いたマス、または既に露出しているマスだけダメージを受ける
                        // （まだ土に埋まっている部分は土が守ってくれる）
                        bool isCenter = centerIsDirectHit && dx == 0 && dy == 0 && dz == 0;
                        if (isCenter || grid.IsExposed(x, y, z))
                        {
                            DamageTreasure(i, x, y, z);
                            result.hitTreasure = true;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 掘り終わったあとの共通処理：お宝のダメージ・発掘率の更新・勝敗の判定
        /// </summary>
        private void FinishDig(DigTool tool, DigResult result)
        {
            // お宝を叩いてしまったらダメージを加算（1回の掘りにつき1回分）
            if (result.hitTreasure)
            {
                damage.Value = Mathf.Min(maxDamage, damage.Value + tool.treasureDamage);
            }

            UpdateProgress();

            // 勝敗の判定
            // 発掘率がクリアラインに達したら（または全部掘り終えたら）クリア
            // → 残りの土を全部壊す演出のあと Cleared になり、DigHud がリザルトを表示する
            if (progress.Value >= clearThreshold || grid.ContactRemaining <= 0)
            {
                StartReveal();
            }
            else if (damage.Value >= maxDamage)
            {
                state.Value = DigState.Failed;
            }
        }

        /// <summary>土・石の耐久値を減らし、0 になったら壊す</summary>
        private void DamageSoil(int i, int x, int y, int z, int power, DigResult result)
        {
            int newHp = grid.hp[i] - power;
            if (newHp <= 0)
            {
                result.broken.Add(new BrokenVoxel { worldPosition = CellToWorld(new Vector3Int(x, y, z)), color = grid.colors[i], type = grid.types[i] });
                RemoveVoxel(i, x, y, z);
                return;
            }

            // まだ壊れない → ヒビが入ったように少し暗くする
            grid.hp[i] = (byte)newHp;
            int maxHp = grid.types[i] == VoxelType.Stone ? generation.stoneHp : generation.dirtHp;
            float ratio = newHp / (float)Mathf.Max(1, maxHp);
            grid.colors[i] = Darken(grid.baseColors[i], Mathf.Lerp(0.55f, 1f, ratio));
            result.hitHard = true;
            MarkDirty(x, y, z);
        }

        /// <summary>お宝を叩いたとき：壊れはしないが、叩くほど色が暗くなる</summary>
        private void DamageTreasure(int i, int x, int y, int z)
        {
            if (grid.treasureHits[i] < 255) grid.treasureHits[i]++;
            float brightness = Mathf.Max(0.4f, 1f - grid.treasureHits[i] * 0.15f);
            grid.colors[i] = Darken(grid.baseColors[i], brightness);
            MarkDirty(x, y, z);
        }

        /// <summary>マスを空気にする</summary>
        private void RemoveVoxel(int i, int x, int y, int z)
        {
            if (grid.contact[i])
            {
                grid.contact[i] = false;
                grid.ContactRemaining--;
            }
            grid.types[i] = VoxelType.Empty;
            grid.hp[i] = 0;
            MarkDirty(x, y, z);
        }

        private void UpdateProgress()
        {
            if (grid.ContactTotal == 0)
            {
                progress.Value = 1f;
                return;
            }
            progress.Value = 1f - grid.ContactRemaining / (float)grid.ContactTotal;
        }

        /// <summary>
        /// マスが変わったら、そのマスのチャンクを作り直し対象にする
        /// チャンクの端のマスなら、隣のチャンクの面も変わるので隣も対象にする
        /// </summary>
        private void MarkDirty(int x, int y, int z)
        {
            MarkChunkDirty(x, y, z);
            int lx = x % chunkSize, ly = y % chunkSize, lz = z % chunkSize;
            if (lx == 0) MarkChunkDirty(x - 1, y, z);
            if (lx == chunkSize - 1) MarkChunkDirty(x + 1, y, z);
            if (ly == 0) MarkChunkDirty(x, y - 1, z);
            if (ly == chunkSize - 1) MarkChunkDirty(x, y + 1, z);
            if (lz == 0) MarkChunkDirty(x, y, z - 1);
            if (lz == chunkSize - 1) MarkChunkDirty(x, y, z + 1);
        }

        private void MarkChunkDirty(int x, int y, int z)
        {
            if (!grid.InBounds(x, y, z)) return;
            chunks[x / chunkSize, y / chunkSize, z / chunkSize].IsDirty = true;
        }

        private static Color32 Darken(Color32 c, float k)
        {
            return new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), c.a);
        }

        // ---------------------------------------------------------------
        // デバッグ
        // ---------------------------------------------------------------

        /// <summary>
        /// 【デバッグ用】残っている土・石をすべて壊して、すぐクリアにする
        /// 通常のクリアと同じ演出（土が崩れ落ちる）を再生するので、クリア後の流れも確認できる
        /// Inspector の DigBlock を右クリック（⋮）→「Debug/全部壊す」からも呼べる（再生中のみ）
        /// </summary>
        [ContextMenu("Debug/全部壊す")]
        public void DebugBreakAll()
        {
            if (!Application.isPlaying) return;

            // 掘っている最中だけ有効（クリア演出中・クリア後・失敗後は何もしない）
            if (grid == null || state.Value != DigState.Digging) return;

            Debug.Log("[DigBlock] デバッグ：すべての土を壊してクリアにします", this);

            StartReveal();
        }

        // ---------------------------------------------------------------
        // クリア演出
        // ---------------------------------------------------------------

        /// <summary>
        /// クリア演出を開始する（残りの土・石を全部壊して、最後に Cleared になる）
        /// </summary>
        private void StartReveal()
        {
            // 残りは演出で全部壊れるので、発掘率は 100% にしておく
            progress.Value = 1f;

            revealCts?.Cancel();
            revealCts?.Dispose();
            revealCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            RevealAsync(revealCts.Token).Forget();
        }

        /// <summary>
        /// 残っている土を上から順に崩していき、お宝を全部見せる演出
        /// </summary>
        private async UniTaskVoid RevealAsync(CancellationToken token)
        {
            state.Value = DigState.Revealing;

            // 残っている土・石を集めて、高い位置から順に並べる
            var remaining = new List<Vector3Int>();
            for (int z = 0; z < grid.Size.z; z++)
            for (int y = 0; y < grid.Size.y; y++)
            for (int x = 0; x < grid.Size.x; x++)
            {
                VoxelType t = grid.types[grid.Index(x, y, z)];
                if (t == VoxelType.Dirt || t == VoxelType.Stone) remaining.Add(new Vector3Int(x, y, z));
            }
            remaining.Sort((a, b) => b.y.CompareTo(a.y));

            const int steps = 30; // 何回に分けて崩すか
            int perStep = Mathf.Max(1, Mathf.CeilToInt(remaining.Count / (float)steps));
            var batch = new List<BrokenVoxel>();

            for (int start = 0; start < remaining.Count; start += perStep)
            {
                batch.Clear();
                int end = Mathf.Min(start + perStep, remaining.Count);
                for (int k = start; k < end; k++)
                {
                    Vector3Int p = remaining[k];
                    int i = grid.Index(p.x, p.y, p.z);
                    batch.Add(new BrokenVoxel { worldPosition = CellToWorld(p), color = grid.colors[i], type = grid.types[i] });
                    RemoveVoxel(i, p.x, p.y, p.z);
                }

                // エフェクト側に「これだけ崩れた」と通知
                revealBroken.OnNext(batch);

                await UniTask.Delay(TimeSpan.FromSeconds(revealDuration / steps), cancellationToken: token);
            }

            state.Value = DigState.Cleared;
        }

        // ---------------------------------------------------------------
        // 評価
        // ---------------------------------------------------------------

        /// <summary>
        /// 成功率（0〜1）。お宝の傷の少なさ。
        /// ダメージ 0 なら 1（100%）、ダメージが上限に達すると 0（0%）
        /// </summary>
        public float SuccessRate => 1f - Mathf.Clamp01(damage.Value / (float)Mathf.Max(1, maxDamage));

        /// <summary>ダメージの少なさでランクを決める</summary>
        public string GetRank()
        {
            float ratio = damage.Value / (float)Mathf.Max(1, maxDamage);
            if (ratio <= 0.05f) return "S";
            if (ratio <= 0.25f) return "A";
            if (ratio <= 0.5f) return "B";
            return "C";
        }
    }
}
