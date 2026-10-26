using UnityEngine;

namespace VoxelDig
{
    /// <summary>
    /// Screen Space - Overlay の Canvas に置いた UI を、3D のオブジェクトの位置に付いていかせる
    ///
    /// World Space の Canvas と違って画面に直接描かれるので、
    /// フォグやポストエフェクトの影響を受けず、柱などにも隠れない
    ///
    /// 使い方：Overlay の Canvas の中の UI（ラベルなど）に付けて、Target に追いかけたいオブジェクトを設定する
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UiFollowWorldTarget : MonoBehaviour
    {
        [Tooltip("追いかける 3D のオブジェクト（Block01 など）")]
        [SerializeField] private Transform target;

        [Tooltip("ターゲットからのずれ（ワールド座標、メートル）。上に出したいなら Y を増やす")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);

        [Tooltip("画面上でのずれ（Canvas の単位）")]
        [SerializeField] private Vector2 screenOffset = Vector2.zero;

        [Tooltip("位置を画面に変換するカメラ（未設定ならメインカメラ）")]
        [SerializeField] private Camera targetCamera;

        private RectTransform rectTransform;
        private RectTransform parentRect;
        private CanvasGroup canvasGroup; // カメラの後ろに回ったときに隠す用（無ければ自動で付ける）

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            parentRect = (RectTransform)transform.parent;
            if (targetCamera == null) targetCamera = Camera.main;

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>追いかける対象を変える（コードから設定するとき用）</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void LateUpdate()
        {
            if (target == null || targetCamera == null) return;

            // 3D の位置 → 画面のピクセル位置
            Vector3 screenPos = targetCamera.WorldToScreenPoint(target.position + worldOffset);

            // カメラの後ろにあるときは隠す（z がマイナス = カメラの後ろ）
            bool isVisible = screenPos.z > 0f;
            canvasGroup.alpha = isVisible ? 1f : 0f;
            if (!isVisible) return;

            // 画面のピクセル位置 → Canvas 上の位置（Overlay の Canvas ではカメラに null を渡す）
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, null, out Vector2 localPos);
            rectTransform.anchoredPosition = localPos + screenOffset;
        }
    }
}
