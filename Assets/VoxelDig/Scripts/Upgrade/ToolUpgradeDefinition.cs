using System;
using UnityEngine;

namespace VoxelDig
{
    /// <summary>
    /// 強化できる性能の種類
    /// </summary>
    public enum UpgradeStat
    {
        /// <summary>掘る範囲（半径）を広げる。ドリルなら穴が太くなる</summary>
        Radius,

        /// <summary>押しっぱなしで掘る速さを上げる（掘る間隔を短くする）</summary>
        Speed,

        /// <summary>1回で減らす耐久値を上げる（石を早く壊せる）</summary>
        Power,

        /// <summary>ドリルが1回で奥へ掘り進むマス数を増やす</summary>
        Depth,
    }

    /// <summary>
    /// 強化1項目の設定（「ハンマーの範囲」「ドリルの速さ」など）
    /// ToolUpgradeSystem の Inspector で一覧として設定する
    /// </summary>
    [Serializable]
    public class ToolUpgradeDefinition
    {
        [Tooltip("セーブに使う名前（英数字で、ほかの項目と重ならないように。あとから変えると、その項目のレベルが 0 に戻る）")]
        public string id = "hammer_radius";

        [Tooltip("UI に表示する名前")]
        public string displayName = "ハンマー：範囲";

        [Tooltip("どの道具を強化するか（DigController の Tools の番号。0 = ハンマー、1 = ノミ、2 = ドリル）")]
        [Min(0)]
        public int toolIndex = 0;

        [Tooltip("強化する性能")]
        public UpgradeStat stat = UpgradeStat.Radius;

        [Tooltip("1レベルごとの強化量\n" +
                 "・Radius：半径に足す量（マス）\n" +
                 "・Speed：掘る間隔に掛ける倍率（0.85 なら 1レベルごとに間隔が 85% になる＝速くなる）\n" +
                 "・Power / Depth：足す量（整数に丸める）")]
        public float amountPerLevel = 0.25f;

        [Tooltip("最大レベル")]
        [Min(1)]
        public int maxLevel = 5;

        [Header("費用（レベル 0 → 1 のときの値。レベルが上がるごとに Cost Growth 倍になる）")]
        [Tooltip("必要なブロックの数")]
        [Min(0)]
        public int cost = 150;

        [Tooltip("レベルが1上がるごとに、費用を何倍にするか")]
        [Min(1f)]
        public float costGrowth = 1.6f;

        /// <summary>
        /// 今のレベルから1つ上げるのに必要なブロックの数
        /// </summary>
        public int GetCost(int currentLevel)
        {
            // 費用 ＝ 最初の費用 × (Cost Growth の レベル 乗)
            // 例：150, 1.6 倍 → 150 → 240 → 384 → 614 …
            return Mathf.RoundToInt(cost * Mathf.Pow(costGrowth, currentLevel));
        }
    }
}
