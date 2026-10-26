using UnityEngine;
using DG.Tweening;
using NaughtyAttributes;

namespace SleepingAnimals
{
    public class IrisShot : MonoBehaviour
    {
        [SerializeField] RectTransform unmask;
        readonly Vector2 IRIS_IN_SCALE = new Vector2(30, 30);
        readonly float SCALE_DURATION = 1;

        /// <summary>
        /// フェードイン（徐々に表示）
        /// </summary>
        [Button]
        public void IrisIn()
        {
            unmask.DOScale(IRIS_IN_SCALE, SCALE_DURATION).SetEase(Ease.InCubic);
        }

        /// <summary>
        /// フェードアウト（徐々に非表示）
        /// </summary>
        [Button]
        public void IrisOut()
        {
            unmask.DOScale(new Vector3(0, 0, 0), SCALE_DURATION).SetEase(Ease.OutCubic);
        }
    }
}
