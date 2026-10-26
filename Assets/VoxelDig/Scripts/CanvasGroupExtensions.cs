using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace VoxelDig
{
    /// <summary>
    /// CanvasGroup（パネル）の表示・非表示を簡単に書くための拡張メソッド
    /// panel.SetVisible(true) のように、CanvasGroup のメソッドのように呼べる
    /// </summary>
    public static class CanvasGroupExtensions
    {
        /// <summary>
        /// フェードで表示／非表示にする
        /// 非表示のときはクリックも通さない（見えないボタンを押せないようにする）
        /// </summary>
        /// <param name="duration">フェード時間（0 ならすぐに切り替える）</param>
        /// <returns>フェードが終わるまで待てる UniTask</returns>
        public static UniTask SetVisible(this CanvasGroup panel, bool visible, float duration = 0.2f)
        {
            if (panel == null) return UniTask.CompletedTask;

            panel.DOKill();
            panel.interactable = visible;
            panel.blocksRaycasts = visible;

            float alpha = visible ? 1f : 0f;
            if (duration <= 0f)
            {
                panel.alpha = alpha;
                return UniTask.CompletedTask;
            }

            // フェードが終わるまで待てるようにする（TweenAwaitExtensions.WaitForKillAsync）
            return panel.DOFade(alpha, duration).SetUpdate(true).WaitForKillAsync();
        }
    }
}
