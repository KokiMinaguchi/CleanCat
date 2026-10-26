using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VoxelDig
{
    /// <summary>
    /// 発掘ポイントを選んだときに出る、立体ピクロス風のカード UI
    ///
    /// ・選んだブロックの横に吹き出しのように表示され、カメラが動いてもブロックに付いていく
    /// ・ブロックが画面の右側にあればカードは左に、左側にあれば右に出る（しっぽも自動で向きが変わる）
    /// ・名前は掘るまで「？？？」、サイズと難しさ（★）だけを見せる
    ///
    /// UI の部品は「Tools > VoxelDig > 選択カードを作成」で自動生成できる（参照も自動で設定される）
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class DigSiteCard : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("ブロックの位置を画面の位置に変換するカメラ（未設定ならメインカメラ）")]
        [SerializeField] private Camera targetCamera;

        [Header("部品")]
        [SerializeField] private Image headerImage;       // 色付きのヘッダー
        [SerializeField] private Image headerFillImage;   // ヘッダー下側の角を四角くする板（ヘッダーと同じ色にする）
        [SerializeField] private TMP_Text numberText;     // 「No.01」
        [SerializeField] private TMP_Text titleText;      // 名前（または「？？？」）
        [SerializeField] private TMP_Text sizeText;       // 「40×39×40」
        [SerializeField] private Image[] starImages;      // 難しさの★（5個）
        [SerializeField] private Button digButton;        // 「掘る」
        [SerializeField] private Button closeButton;      // 「×」
        [SerializeField] private RectTransform tail;      // 吹き出しのしっぽ（右向きの三角形）

        [Header("発掘済みの表示")]
        [Tooltip("「掘る」ボタンの文字（発掘済みのときに書き換える。無くても動く）")]
        [SerializeField] private TMP_Text digButtonText;

        [Tooltip("発掘済みのときだけ表示するもの（「発掘済み」のバッジなど。無くても動く）")]
        [SerializeField] private GameObject completedBadge;

        [SerializeField] private string digLabel = "掘る";
        [SerializeField] private string completedLabel = "発掘済み";
        [SerializeField] private string redigLabel = "もう一度掘る";

        [Header("見た目")]
        [SerializeField] private Color starOnColor = new Color(0.953f, 0.718f, 0.169f);
        [SerializeField] private Color starOffColor = new Color(0.847f, 0.824f, 0.784f);

        [Tooltip("名前を隠しているときに表示する文字")]
        [SerializeField] private string hiddenName = "？？？";

        [Header("配置")]
        [Tooltip("カードの出し方（全発掘ポイント共通の初期設定）。DigSite の Override Card Placement を ON にすると、ポイントごとに上書きできる")]
        [SerializeField] private CardPlacement defaultPlacement = new CardPlacement();

        [Tooltip("画面の端からこれ以上は離す（Canvas の単位）")]
        [SerializeField] private float screenMargin = 24f;

        [Tooltip("ブロックを追いかけるなめらかさ（大きいほどピタッと付いてくる）")]
        [SerializeField] private float followSmoothing = 20f;

        [Header("アニメーション")]
        [SerializeField] private float showDuration = 0.25f;
        [SerializeField] private float hideDuration = 0.15f;

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private RectTransform canvasRect;   // 一番上の Canvas の RectTransform（位置の計算はこの座標で行う）
        private Canvas rootCanvas;

        private DigSite site;               // 今表示している発掘ポイント
        private bool isShown;
        private bool snapNextFrame;         // 次のフレームは追いかけずに一瞬で移動する（出した直後用）
        private bool isBehindCamera;        // ブロックがカメラの後ろにあって、カードを消している
        private Sequence showSequence;

        /// <summary>「掘る」ボタン（ExploreSceneController がクリック時の処理を登録する）</summary>
        public Button DigButton => digButton;

        /// <summary>「×」ボタン</summary>
        public Button CloseButton => closeButton;

        /// <summary>今カードに表示している発掘ポイント</summary>
        public DigSite Site => site;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = (RectTransform)transform;
            rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
            canvasRect = (RectTransform)rootCanvas.transform;
            if (targetCamera == null) targetCamera = Camera.main;

            // 最初は隠しておく
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void OnDestroy()
        {
            showSequence?.Kill();
        }

        // ---------------------------------------------------------------
        // 表示・非表示
        // ---------------------------------------------------------------

        /// <summary>
        /// 発掘ポイントの情報でカードを表示する
        /// </summary>
        public void Show(DigSite newSite)
        {
            if (newSite == null)
            {
                Hide();
                return;
            }

            bool wasShown = isShown;
            site = newSite;
            isShown = true;
            ApplyContent(newSite);

            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            showSequence?.Kill();
            if (!wasShown)
            {
                // 隠れていた状態から出すとき：その場にパッと移動して、ポンッと拡大しながら出す
                snapNextFrame = true;
                UpdatePosition(true);
                rectTransform.localScale = Vector3.one * 0.85f;
                showSequence = DOTween.Sequence()
                    .Join(canvasGroup.DOFade(1f, showDuration))
                    .Join(rectTransform.DOScale(1f, showDuration).SetEase(Ease.OutBack))
                    .SetUpdate(true);
            }
            else
            {
                // 別のブロックに切り替えたとき：表示したまま、少しだけ弾ませる
                canvasGroup.alpha = 1f;
                rectTransform.localScale = Vector3.one;
                showSequence = DOTween.Sequence()
                    .Append(rectTransform.DOPunchScale(Vector3.one * 0.06f, 0.2f, 6))
                    .SetUpdate(true);
            }
        }

        /// <summary>カードを隠す</summary>
        public void Hide()
        {
            if (!isShown) return;
            isShown = false;

            // 隠れ始めたらすぐにクリックを受け付けないようにする
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            showSequence?.Kill();
            showSequence = DOTween.Sequence()
                .Join(canvasGroup.DOFade(0f, hideDuration))
                .Join(rectTransform.DOScale(0.92f, hideDuration))
                .SetUpdate(true)
                .OnComplete(() => site = null);
        }

        /// <summary>カードの中身（番号・名前・サイズ・★・色）を書き換える</summary>
        private void ApplyContent(DigSite s)
        {
            // ヘッダーの色（発掘ポイントごとのテーマ色）
            if (headerImage != null) headerImage.color = s.ThemeColor;
            if (headerFillImage != null) headerFillImage.color = s.ThemeColor;

            if (numberText != null)
            {
                numberText.SetText("No.{0:00}", s.SiteNumber);
                numberText.color = s.ThemeColor;
            }

            // 名前：立体ピクロスのように、掘り出すまでは「？？？」にする
            if (titleText != null)
            {
                titleText.SetText(s.HideNameUntilDug && !s.IsCompleted ? hiddenName : s.DisplayName);
            }

            // サイズ：CSV の size 行（X×Y×Z）
            if (sizeText != null)
            {
                Vector3Int size = s.ModelSize;
                if (size == Vector3Int.zero) sizeText.SetText("?×?×?");
                else sizeText.SetText($"{size.x}×{size.y}×{size.z}");
            }

            // 「掘る」ボタン：まだ掘っていない → 掘る
            //               発掘済み → もう一度掘れる設定なら「もう一度掘る」、そうでなければ押せない「発掘済み」
            bool canDig = !s.IsCompleted || s.AllowRedig;
            if (digButton != null) digButton.interactable = canDig;
            if (digButtonText != null)
            {
                string label = !s.IsCompleted ? digLabel : (s.AllowRedig ? redigLabel : completedLabel);
                digButtonText.SetText(label);
            }
            if (completedBadge != null) completedBadge.SetActive(s.IsCompleted);

            // 難しさ：★を difficulty 個だけ点ける
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] != null) starImages[i].color = i < s.Difficulty ? starOnColor : starOffColor;
            }
        }

        // ---------------------------------------------------------------
        // ブロックへの追従
        // ---------------------------------------------------------------

        private void LateUpdate()
        {
            // 表示中、または消えていく途中はブロックに付いていく
            if (site == null) return;
            UpdatePosition(snapNextFrame);
            snapNextFrame = false;
        }

        /// <summary>
        /// 設定（CardPlacement）に従ってカードの位置を決める
        ///
        /// 位置の計算は「一番上の Canvas の座標」で行い、最後にワールド座標に変換して置く。
        /// こうすると Screen Space - Overlay / Camera のどちらでも、
        /// カードがどんな階層（パネルの子など）に入っていても同じように動く
        /// </summary>
        /// <param name="snap">true なら追いかけずに一瞬で移動する</param>
        private void UpdatePosition(bool snap)
        {
            if (targetCamera == null || site == null) return;

            // 発掘ポイントごとの設定があればそれを、無ければカードの初期設定を使う
            CardPlacement placement = site.PlacementOverride ?? defaultPlacement;

            // Overlay の Canvas ではカメラに null を渡す。Camera / World Space では Canvas のカメラを使う
            Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            Rect canvas = canvasRect.rect;

            // ---- カードの大きさ（Canvas の単位）----
            // カードや親の Scale が 1 以外でも正しく計算できるよう、Canvas との拡大率の比で直す
            float scaleToCanvas = rectTransform.lossyScale.x / canvasRect.lossyScale.x;
            Vector2 cardSize = rectTransform.rect.size * scaleToCanvas;
            float halfWidth = cardSize.x * 0.5f;
            float halfHeight = cardSize.y * 0.5f;

            // ---- 基準点（ブロック）の画面上の位置 ----
            Vector2 anchorLocal = Vector2.zero;
            bool hasAnchor = false;
            if (placement.mode != CardPlacementMode.FixedScreen)
            {
                // 3D の位置 → 画面のピクセル位置
                Vector3 screenPos = targetCamera.WorldToScreenPoint(site.CardAnchorPosition);

                // カメラの後ろにあるときは表示しない（z がマイナス = カメラの後ろ）
                if (screenPos.z < 0f)
                {
                    canvasGroup.alpha = 0f;
                    isBehindCamera = true;
                    return;
                }

                // 画面のピクセル位置 → Canvas 上の位置
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out anchorLocal);
                hasAnchor = true;
            }

            if (isBehindCamera)
            {
                // カメラの前に戻ってきたら、表示中ならまた見えるようにする
                isBehindCamera = false;
                if (isShown) canvasGroup.alpha = 1f;
            }

            // ---- カードの中心をどこに置くか ----
            Vector2 target;
            switch (placement.mode)
            {
                case CardPlacementMode.AnchorOffset:
                    // 基準点から決まった量だけずらす
                    target = anchorLocal + placement.screenOffset;
                    break;

                case CardPlacementMode.FixedScreen:
                    // 画面の割合（0〜1）→ Canvas 上の位置
                    target = new Vector2(
                        Mathf.Lerp(canvas.xMin, canvas.xMax, placement.viewportPosition.x),
                        Mathf.Lerp(canvas.yMin, canvas.yMax, placement.viewportPosition.y));
                    break;

                default: // AutoSide
                    // ブロックが画面の右半分にあればカードを左に、左半分なら右に出す
                    bool placeLeft = anchorLocal.x > canvas.center.x;
                    target = anchorLocal + new Vector2(placeLeft ? -(placement.sideGap + halfWidth) : placement.sideGap + halfWidth, 0f);
                    break;
            }

            // 画面からはみ出さないように収める
            target.x = Mathf.Clamp(target.x, canvas.xMin + screenMargin + halfWidth, canvas.xMax - screenMargin - halfWidth);
            target.y = Mathf.Clamp(target.y, canvas.yMin + screenMargin + halfHeight, canvas.yMax - screenMargin - halfHeight);

            // カードの pivot が中心でなくても、カードの「中心」が target に来るように補正する
            Vector2 pivotOffset = (rectTransform.pivot - new Vector2(0.5f, 0.5f)) * cardSize;
            Vector2 targetPivot = target + pivotOffset;

            // なめらかに追いかける（出した直後は一瞬で移動）
            Vector2 current = canvasRect.InverseTransformPoint(rectTransform.position);
            float t = snap ? 1f : 1f - Mathf.Exp(-followSmoothing * Time.unscaledDeltaTime);
            Vector2 next = Vector2.Lerp(current, targetPivot, t);

            // Canvas の座標 → ワールド座標にして置く（Screen Space - Camera でも Canvas の面の上に乗る）
            rectTransform.position = canvasRect.TransformPoint(next);

            UpdateTail(hasAnchor, anchorLocal, next - pivotOffset, halfWidth, halfHeight, scaleToCanvas);
        }

        /// <summary>
        /// しっぽをブロック側の辺に移動して、ブロックの方を向ける
        /// ブロックがカードの真上・真下にあるとき（横に出ていないとき）は、しっぽを隠す
        /// </summary>
        private void UpdateTail(bool hasAnchor, Vector2 anchorLocal, Vector2 cardCenter, float halfWidth, float halfHeight, float scaleToCanvas)
        {
            if (tail == null) return;

            // ブロックがカードの横にはみ出している（＝カードの左右どちらかにある）ときだけ表示する
            float dx = anchorLocal.x - cardCenter.x;
            bool showTail = hasAnchor && Mathf.Abs(dx) > halfWidth;
            tail.gameObject.SetActive(showTail);
            if (!showTail) return;

            bool blockIsRight = dx > 0f;

            // しっぽの高さは、ブロックの高さに合わせる（カードの角にはみ出さない範囲で）
            // tail はカードの子なので、カードの座標（Canvas の単位 ÷ 拡大率）に直して置く
            float localY = (anchorLocal.y - cardCenter.y) / scaleToCanvas;
            float limit = halfHeight / scaleToCanvas - 56f;
            localY = Mathf.Clamp(localY, -limit, limit);

            // ブロックが右にあるなら右の辺から右向き（三角形をそのまま）、左にあるなら左の辺から左向き（左右反転）
            float edgeX = (blockIsRight ? halfWidth : -halfWidth) / scaleToCanvas;
            tail.anchoredPosition = new Vector2(edgeX, localY);
            tail.localScale = new Vector3(blockIsRight ? 1f : -1f, 1f, 1f);
        }
    }
}
