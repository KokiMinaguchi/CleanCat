using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VoxelDig
{
    /// <summary>
    /// 「画面上のこの位置に UI があるか」を調べる便利クラス
    ///
    /// EventSystem.IsPointerOverGameObject() は Input System と組み合わせると、
    /// 呼ぶタイミングによって正しい結果が返らないことがある。
    /// そこで、指定した位置に UI のレイキャストを直接飛ばして確実に判定する
    /// </summary>
    public static class UiPointerUtility
    {
        // 毎回 new しないよう使い回す（GC を減らす）
        private static readonly List<RaycastResult> Results = new List<RaycastResult>();
        private static PointerEventData pointerData;
        private static EventSystem cachedEventSystem;

        /// <summary>
        /// 画面上の位置 screenPosition に、クリックを受け取る UI があるか
        /// </summary>
        public static bool IsOverUi(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            // シーンが変わって EventSystem が別物になったら作り直す
            if (pointerData == null || cachedEventSystem != eventSystem)
            {
                pointerData = new PointerEventData(eventSystem);
                cachedEventSystem = eventSystem;
            }

            pointerData.position = screenPosition;
            Results.Clear();
            // Canvas の GraphicRaycaster すべてにレイを飛ばす
            // （Raycast Target が ON の Image や Text、ボタンなどに当たる）
            eventSystem.RaycastAll(pointerData, Results);
            return Results.Count > 0;
        }
    }
}
