using R3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoxelDig
{
    /// <summary>
    /// 探索モードで、クリックした発掘ポイント（DigSite）を選択するコンポーネント
    /// ・発掘ポイントをクリック → 選択
    /// ・何もない所をクリック → 選択解除
    /// 発掘モード中は DigSessionManager がこのコンポーネントを無効にする
    ///
    /// PlayerInput（Invoke Unity Events）の Point → OnPoint、Dig → OnClick に登録して使う
    /// （掘る操作と同じ左クリックを共有している）
    /// </summary>
    public class DigSiteSelector : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

        [Tooltip("発掘ポイントが置かれているレイヤー（壁などに隠れているときは選べないよう、壁のレイヤーも含めておく）")]
        [SerializeField] private LayerMask raycastLayers = ~0;

        [SerializeField] private float rayDistance = 200f;

        // 今選ばれている発掘ポイント（UI が購読して、ボタンの表示を切り替える）
        private readonly ReactiveProperty<DigSite> selected = new ReactiveProperty<DigSite>(null);
        public ReadOnlyReactiveProperty<DigSite> Selected => selected;

        private Vector2 pointerPosition;
        private bool clickRequested;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        private void OnEnable()
        {
            // 無効中のクリックが残らないようにする
            clickRequested = false;
        }

        private void OnDestroy()
        {
            selected.Dispose();
        }

        // ---------------------------------------------------------------
        // 入力（PlayerInput の Unity Events から呼ばれる）
        // ---------------------------------------------------------------

        public void OnPoint(InputAction.CallbackContext context)
        {
            pointerPosition = context.ReadValue<Vector2>();
        }

        public void OnClick(InputAction.CallbackContext context)
        {
            // UI の上かどうかの判定は Update で行うので、ここでは記録だけ
            if (context.performed && isActiveAndEnabled) clickRequested = true;
        }

        // ---------------------------------------------------------------
        // 更新
        // ---------------------------------------------------------------

        private void Update()
        {
            if (!clickRequested) return;
            clickRequested = false;

            // UI（「掘る」ボタンなど）をクリックしたときは選択を変えない
            if (UiPointerUtility.IsOverUi(pointerPosition)) return;

            Ray ray = targetCamera.ScreenPointToRay(pointerPosition);
            DigSite site = null;
            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, raycastLayers, QueryTriggerInteraction.Collide))
            {
                // 子オブジェクトのコライダーに当たった場合も見つけられるよう、親方向に探す
                site = hit.collider.GetComponentInParent<DigSite>();
            }

            Select(site);
        }

        /// <summary>選択を切り替える（null で選択解除）</summary>
        public void Select(DigSite site)
        {
            // 発掘済みのポイントも選べる（カードに名前と「発掘済み」を表示する）
            if (selected.Value == site) return;

            if (selected.Value != null) selected.Value.SetSelected(false);
            selected.Value = site;
            if (site != null) site.SetSelected(true);
        }

        public void ClearSelection() => Select(null);
    }
}
