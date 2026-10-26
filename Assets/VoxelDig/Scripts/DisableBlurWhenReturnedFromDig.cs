using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoxelDig
{
    /// <summary>
    /// 発掘シーンから戻ってきたときに、Volume のぼかし（Depth of Field）を OFF にする
    ///
    /// タイトル用にぼかしを ON にしたまま保存しているシーンでは、
    /// 発掘シーンから戻ってシーンが読み込み直されると、ぼかし ON の状態に戻ってしまう。
    /// （タイトルは HideWhenReturnedFromDig で最初から隠れるので、タイトルを閉じたときの「ぼかし OFF」が呼ばれない）
    /// そこで、戻ってきたときだけ、ここでぼかしを OFF にする。
    ///
    /// 使い方：ぼかしが入っている Global Volume に付けるだけ
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class DisableBlurWhenReturnedFromDig : MonoBehaviour
    {
        private void Awake()
        {
            // ゲームを始めたとき（まだ発掘シーンへ行っていないとき）は、タイトル用のぼかしをそのまま使う
            if (!DigSceneData.HasEnteredDigScene) return;

            var volume = GetComponent<Volume>();

            // volume.profile を使うと、このシーンの中だけで使うコピーが作られる
            // （sharedProfile を書き換えると、Volume Profile のアセットそのものが変わって、
            //   再生を止めたあともぼかしが OFF のまま残ってしまう）
            if (volume.profile.TryGet(out DepthOfField depthOfField))
            {
                depthOfField.active = false;
            }
        }
    }
}
