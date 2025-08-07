using UnityEngine;

/// <summary>
/// 重み付き確率抽選クラス
/// </summary>
public class WeightedChooser
{
    // 各要素の重みリスト
    private float[] _weights;

    // 重みの総和（初期化時に計算される）
    private float _totalWeight;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="weights">重みリスト</param>
    public WeightedChooser(float[] weights)
    {
        _weights = weights;

        // 重みの総和計算
        for (var i = 0; i < _weights.Length; i++)
        {
            _totalWeight += _weights[i];
        }
    }

    /// <summary>
    /// 重み付きの確率抽選を実施する
    /// </summary>
    /// <returns>選択された要素のインデックス</returns>
    public int Choose()
    {
        // 0～重みの総和の範囲の乱数値取得
        var randomPoint = Random.Range(0, _totalWeight);
        //Debug.Log(randomPoint);
        // 乱数値が属する要素を先頭から順に選択
        var currentWeight = 0f;
        for (var i = 0; i < _weights.Length; i++)
        {
            // 現在要素までの重みの総和を求める
            currentWeight += _weights[i];

            // 乱数値が現在要素の範囲内かチェック
            if (randomPoint < currentWeight)
            {
                return i;
            }
        }

        // 乱数値が重みの総和以上なら末尾要素とする
        return _weights.Length - 1;
    }
}