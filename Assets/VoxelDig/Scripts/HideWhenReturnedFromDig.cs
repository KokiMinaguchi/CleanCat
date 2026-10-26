using UnityEngine;

namespace VoxelDig
{
    /// <summary>
    /// 発掘シーンから戻ってきたときに、このオブジェクトを非表示にする
    ///
    /// タイトルキャンバスなど「ゲームを始めたときだけ出したいもの」に付けておくと、
    /// 発掘シーンから戻ってきて探索シーンが読み込み直されたときには、最初から隠れた状態になる
    ///
    /// 使い方：TitleCanvas に付けるだけ（設定項目なし）
    /// </summary>
    // ほかのスクリプトより先に Awake を実行して、タイトルの Start（BGM を鳴らす、入力を止める など）が動く前に隠す
    [DefaultExecutionOrder(-1000)]
    public class HideWhenReturnedFromDig : MonoBehaviour
    {
        private void Awake()
        {
            // 一度でも発掘シーンへ行っていれば、ここへは「戻ってきた」ということ
            if (DigSceneData.HasEnteredDigScene)
            {
                // 非表示にすると、このオブジェクトと子の Start / Update は動かなくなる
                gameObject.SetActive(false);
            }
        }
    }
}
