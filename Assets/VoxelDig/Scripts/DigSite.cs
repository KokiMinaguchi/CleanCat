using DG.Tweening;
using UnityEngine;

namespace VoxelDig
{
    /// <summary>
    /// 探索シーンに置く「発掘ポイント」
    /// 光るキューブなどの見た目に付けておくと、クリックで選択できるようになる
    /// 「掘る」を押すと、ここに設定した CSV を持って発掘シーンへ移動する
    ///
    /// 必要なもの：Collider（クリック判定用。無ければ BoxCollider を自動で付ける）
    /// </summary>
    public class DigSite : MonoBehaviour
    {
        [Header("中身")]
        [Tooltip("発掘ポイントごとに違う ID（クリア済みの記録に使う）。空ならシーン名 + オブジェクト名")]
        [SerializeField] private string siteId;

        [Tooltip("UI に表示する名前")]
        [SerializeField] private string displayName = "謎のブロック";

        [Tooltip("埋まっているボクセルモデルの CSV")]
        [SerializeField] private TextAsset voxelCsv;

        [Header("選択カードの表示")]
        [Tooltip("カードに出す番号（No.01 など）")]
        [SerializeField] private int siteNumber = 1;

        [Tooltip("難しさ（★の数、1〜5）")]
        [Range(1, 5)]
        [SerializeField] private int difficulty = 2;

        [Tooltip("カードのヘッダーの色（発掘ポイントごとに変えると見分けやすい）")]
        [SerializeField] private Color themeColor = new Color(0.953f, 0.604f, 0.169f); // オレンジ

        [Tooltip("ON なら、掘り出すまで名前を「？？？」で隠す（立体ピクロス風）")]
        [SerializeField] private bool hideNameUntilDug = true;

        [Tooltip("ON なら、発掘済みでも何度でも掘れる（OFF なら発掘済みは見るだけ）")]
        [SerializeField] private bool allowRedig = true;

        [Header("選択カードの位置")]
        [Tooltip("カードの位置の基準点。空のオブジェクトを置いて設定すると、そこを基準にする（未設定ならブロックの中心）")]
        [SerializeField] private Transform cardAnchorPoint;

        [Tooltip("ON にすると、この発掘ポイントだけカードの出し方を下の設定で上書きする（OFF ならカード側の初期設定）")]
        [SerializeField] private bool overrideCardPlacement = false;

        [SerializeField] private CardPlacement cardPlacement = new CardPlacement();

        [Header("掘り出したアイテム")]
        [Tooltip("発掘済みになったら、ブロックの代わりに掘り出したモデル（CSV）を表示する")]
        [SerializeField] private bool showDugItem = true;

        [Tooltip("アイテムのマテリアル（VoxelMat など、頂点カラーで色が付くもの）")]
        [SerializeField] private Material itemMaterial;

        [Tooltip("ブロックの大きさに対するアイテムの大きさ（1 でブロックと同じ大きさ）")]
        [SerializeField] private float itemSizeRatio = 0.9f;

        [Tooltip("ON：ブロックの底に置く ／ OFF：ブロックの中心に置く")]
        [SerializeField] private bool placeItemOnBottom = true;

        [Tooltip("アイテムをゆっくり回す速さ（度/秒）。0 で回さない")]
        [SerializeField] private float itemRotateSpeed = 20f;

        [Tooltip("クリアして戻ってきたときの出現演出の時間（秒）")]
        [SerializeField] private float itemAppearDuration = 0.6f;

        [Tooltip("出現演出を始めるまでの待ち時間（画面が明るくなるのを待つ）")]
        [SerializeField] private float itemAppearDelay = 0.4f;

        [Header("発掘済みの見た目")]
        [Tooltip("発掘済みになったら隠すもの（光るキューブの子オブジェクトなど）。未設定ならこのオブジェクトの Renderer 全部")]
        [SerializeField] private GameObject[] hideWhenCompleted;

        [Tooltip("発掘済みになったら表示するもの（空の台座など。無くても動く）")]
        [SerializeField] private GameObject completedVisual;

        [Header("選択中の見た目")]
        [Tooltip("選択中だけ表示するもの（足元の輪っかなど。無くても動く）")]
        [SerializeField] private GameObject selectionMarker;

        [Tooltip("選択中に、ふわふわ拡大縮小する量")]
        [SerializeField] private float selectPulseScale = 0.06f;
        [SerializeField] private float selectPulseDuration = 0.6f;

        private Collider siteCollider;
        private Vector3 baseScale;
        private Tween pulseTween;
        private Mesh itemMesh; // 自分で作ったメッシュ（破棄するときに使う）
        private Transform dugItem;      // 掘り出したアイテム（発掘済みのときだけある）
        private Vector3 dugItemScale;   // アイテムの元の大きさ（選択中のふわふわ拡大から戻すとき用）
        private Tween appearTween;      // アイテムの出現演出

        public string DisplayName => displayName;
        public TextAsset VoxelCsv => voxelCsv;
        public int SiteNumber => siteNumber;
        public int Difficulty => difficulty;
        public Color ThemeColor => themeColor;
        public bool HideNameUntilDug => hideNameUntilDug;
        public bool AllowRedig => allowRedig;

        // CSV のサイズ（最初に聞かれたときに1回だけ読む）
        private Vector3Int? modelSize;

        /// <summary>埋まっているモデルの大きさ（CSV の size 行）。読めなければ (0,0,0)</summary>
        public Vector3Int ModelSize
        {
            get
            {
                if (modelSize == null)
                {
                    VoxelCsvLoader.TryReadSize(voxelCsv != null ? voxelCsv.text : null, out Vector3Int size);
                    modelSize = size;
                }
                return modelSize.Value;
            }
        }

        /// <summary>
        /// カードを並べる基準になる位置
        /// Card Anchor Point が設定されていればその位置、無ければブロックの見た目の中心
        /// （選択中のふわふわ拡大でずれないよう、Collider の中心を使う）
        /// </summary>
        public Vector3 CardAnchorPosition
        {
            get
            {
                if (cardAnchorPoint != null) return cardAnchorPoint.position;
                return siteCollider != null ? siteCollider.bounds.center : transform.position;
            }
        }

        /// <summary>この発掘ポイント専用のカードの出し方（上書きしないなら null）</summary>
        public CardPlacement PlacementOverride => overrideCardPlacement ? cardPlacement : null;

        /// <summary>ID が空なら「シーン名/オブジェクト名」を ID として使う</summary>
        public string SiteId => string.IsNullOrEmpty(siteId) ? $"{gameObject.scene.name}/{name}" : siteId;

        /// <summary>発掘済み（クリア済み）かどうか</summary>
        public bool IsCompleted { get; private set; }

        private void Awake()
        {
            // Collider は抽象クラスなので RequireComponent では自動追加できない → 無ければ自分で付ける
            siteCollider = GetComponentInChildren<Collider>();
            if (siteCollider == null) siteCollider = gameObject.AddComponent<BoxCollider>();

            baseScale = transform.localScale;
            if (selectionMarker != null) selectionMarker.SetActive(false);
            if (completedVisual != null) completedVisual.SetActive(false);

            // 発掘シーンから戻ってきたとき、クリア済みなら発掘済みの見た目にする
            if (DigSceneData.IsCompleted(SiteId)) ApplyCompleted();
        }

        /// <summary>
        /// Scene ビューで選択したとき、カードの基準点を黄色い球で表示する（位置合わせ用）
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Vector3 anchor;
            if (cardAnchorPoint != null)
            {
                anchor = cardAnchorPoint.position;
            }
            else
            {
                // 再生前は Awake が呼ばれていないので、ここで Collider を探す
                var col = GetComponentInChildren<Collider>();
                anchor = col != null ? col.bounds.center : transform.position;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(anchor, 0.3f);
            Gizmos.DrawLine(transform.position, anchor);
        }

        private void OnDestroy()
        {
            pulseTween?.Kill();
            // 自分で new した Mesh は自分で破棄する（メモリリーク防止）
            if (itemMesh != null) Destroy(itemMesh);
        }

        /// <summary>選択中の見た目にする／戻す</summary>
        public void SetSelected(bool selected)
        {
            if (selectionMarker != null) selectionMarker.SetActive(selected);

            // 発掘済みならアイテムを、まだならブロック自身をふわふわさせる
            Transform pulseTarget = dugItem != null ? dugItem : transform;
            Vector3 pulseBaseScale = dugItem != null ? dugItemScale : baseScale;

            // 出現演出の途中なら、先に最後まで進めてしまう（大きさのアニメーションがぶつからないように）
            if (appearTween != null && appearTween.IsActive()) appearTween.Complete();

            pulseTween?.Kill();
            pulseTarget.localScale = pulseBaseScale;

            if (selected)
            {
                // ふわふわと拡大縮小をくり返す（Yoyo で行ったり来たり）
                pulseTween = pulseTarget
                    .DOScale(pulseBaseScale * (1f + selectPulseScale), selectPulseDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetLink(pulseTarget.gameObject);
            }
        }

        /// <summary>
        /// 発掘済みの見た目にする（ブロックを隠して、掘り出したアイテムを置く）
        /// クリック判定（Collider）は残すので、アイテムをクリックすると選択できる
        /// </summary>
        private void ApplyCompleted()
        {
            IsCompleted = true;

            // ブロックを隠す前に、ブロックの大きさと位置を測っておく（アイテムを同じ場所に置くため）
            bool hasBounds = TryGetPlaceholderBounds(out Bounds placeholderBounds);

            if (hideWhenCompleted != null && hideWhenCompleted.Length > 0)
            {
                foreach (var go in hideWhenCompleted)
                {
                    if (go != null) go.SetActive(false);
                }
            }
            else
            {
                foreach (var r in GetComponentsInChildren<Renderer>())
                {
                    r.enabled = false;
                }
            }

            if (completedVisual != null) completedVisual.SetActive(true);

            // ※ Collider は残しておく（掘り出したアイテムをクリックしてもカードが出るように）

            // ブロックの代わりに、掘り出したアイテムを置く
            if (showDugItem && hasBounds) SpawnDugItem(placeholderBounds);
        }

        /// <summary>
        /// ブロック（プレースホルダー）の見た目全体を囲む箱を求める
        /// </summary>
        private bool TryGetPlaceholderBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                // パーティクルは大きさの計算に含めない
                if (r is ParticleSystemRenderer) continue;

                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
            return found;
        }

        /// <summary>
        /// CSV のモデルからアイテムを作って、ブロックがあった場所に置く
        /// </summary>
        private void SpawnDugItem(Bounds placeholderBounds)
        {
            if (voxelCsv == null) return;

            VoxelModel model = VoxelCsvLoader.Load(voxelCsv.text);
            if (model.points.Count == 0) return;

            itemMesh = VoxelModelMeshBuilder.Build(model);

            // アイテム用の GameObject を作る
            // ブロックの子にすると、ブロックの scale（3 など）の影響を受けるので、同じ親の下（兄弟）に置く
            var item = new GameObject($"{name}_DugItem", typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(transform.parent, false);
            item.GetComponent<MeshFilter>().sharedMesh = itemMesh;
            if (itemMaterial != null) item.GetComponent<MeshRenderer>().sharedMaterial = itemMaterial;

            // 大きさ：ブロックの一番長い辺 × itemSizeRatio を、モデルの一番長い辺に合わせる
            // （メッシュは 1ボクセル = 1 で作っているので、scale = 1ボクセルの大きさ）
            Vector3 size = placeholderBounds.size;
            float targetSize = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * itemSizeRatio;
            float voxelScale = targetSize / VoxelModelMeshBuilder.GetMaxDimension(model);
            Vector3 finalScale = Vector3.one * voxelScale;

            // 位置：メッシュの原点は「底の中心」なので、ブロックの底（または中心）に合わせる
            Vector3 position = placeholderBounds.center;
            if (placeItemOnBottom) position.y = placeholderBounds.min.y;
            else position.y -= targetSize * 0.5f;

            item.transform.position = position;
            item.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            item.transform.localScale = finalScale;

            // 選択中のふわふわ拡大で使うので覚えておく
            dugItem = item.transform;
            dugItemScale = finalScale;

            // ゆっくり回す（SetLink：アイテムが破棄されたら Tween も一緒に止まる）
            if (itemRotateSpeed > 0f)
            {
                item.transform
                    .DORotate(new Vector3(0f, 360f, 0f), 360f / itemRotateSpeed, RotateMode.WorldAxisAdd)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Restart)
                    .SetLink(item);
            }

            // クリアしてすぐ戻ってきたときだけ、ポンッと出てくる演出を再生する
            if (DigSceneData.ConsumeJustCompleted(SiteId))
            {
                item.transform.localScale = Vector3.zero;
                appearTween = item.transform
                    .DOScale(finalScale, itemAppearDuration)
                    .SetDelay(itemAppearDelay)
                    .SetEase(Ease.OutBack)
                    .SetLink(item);
            }
        }
    }
}
