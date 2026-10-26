using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace VoxelDig
{
    /// <summary>
    /// DOTween の Tween が終わるまで await で待つための拡張メソッド
    ///
    /// UniTask にも Tween.ToUniTask() があるが、DOTween の ASMDEF 設定などの条件がそろわないと使えないため、
    /// どの環境でも動くように自前で用意している
    /// </summary>
    public static class TweenAwaitExtensions
    {
        /// <summary>
        /// Tween が終わる（または Kill される）まで待つ
        /// </summary>
        /// <param name="tween">待ちたい Tween</param>
        /// <param name="token">キャンセルされたら Tween を止めて、待つのをやめる</param>
        public static UniTask WaitForKillAsync(this Tween tween, CancellationToken token = default)
        {
            // もう終わっている Tween なら、待たずにすぐ終わる
            if (tween == null || !tween.IsActive()) return UniTask.CompletedTask;

            // 「終わったら結果をセットする」入れ物。Task を外に返して、そこで await してもらう
            var completion = new UniTaskCompletionSource();

            // キャンセルされたら、Tween を止めてキャンセル扱いにする
            CancellationTokenRegistration registration = default;
            if (token.CanBeCanceled)
            {
                registration = token.Register(() =>
                {
                    completion.TrySetCanceled(token);
                    tween.Kill();
                });
            }

            // 終わったときの処理（TrySetResult は2回呼ばれても2回目は無視されるので安全）
            TweenCallback finish = () =>
            {
                registration.Dispose();
                completion.TrySetResult();
            };

            // OnComplete：最後まで再生したとき（DOTween の設定で Auto Kill が OFF でも必ず呼ばれる）
            // OnKill    ：途中で止められたとき（DOKill など）
            // 両方に登録しておくと、どちらで終わっても待ちが解除される
            tween.OnComplete(finish);
            tween.OnKill(finish);

            return completion.Task;
        }
    }
}
