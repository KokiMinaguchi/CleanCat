using System;
using UnityEngine;

namespace VoxelDig
{
    /// <summary>
    /// 道具の掘り方
    /// </summary>
    public enum DigShape
    {
        /// <summary>叩いたマスを中心に、丸く削る（ハンマー・ノミ）</summary>
        Sphere,

        /// <summary>叩いたマスから、カメラの視線の奥へ向かってトンネル状に削る（ドリル）</summary>
        Tunnel,
    }

    /// <summary>
    /// 掘る道具の性能（DigController の Inspector で一覧として設定する）
    /// </summary>
    [Serializable]
    public class DigTool
    {
        [Tooltip("UI に表示する名前")]
        public string displayName = "ハンマー";

        [Tooltip("掘り方。Sphere：丸く削る ／ Tunnel：視線の奥へ向かって穴を掘り進む（ドリル用）")]
        public DigShape shape = DigShape.Sphere;

        [Tooltip("一度に削る範囲の半径（マス）。0.5 で1マスだけ。Tunnel のときは穴の太さ")]
        public float radius = 1.6f;

        [Tooltip("Tunnel のとき、1回で奥へ何マス掘り進むか（Sphere のときは使わない）")]
        [Min(1)]
        public int depth = 1;

        [Tooltip("1回で減らす耐久値（土は1、石は3で壊れる）")]
        public int power = 1;

        [Tooltip("お宝を叩いてしまったときに増えるダメージ")]
        public int treasureDamage = 3;

        [Tooltip("掘ったときのカメラの揺れの強さ")]
        public float shakeStrength = 0.08f;

        [Header("押しっぱなし")]
        [Tooltip("押しっぱなしのとき、次に掘るまでの間隔（秒）。0 なら DigController の Hold Repeat Interval を使う")]
        [Min(0f)]
        public float holdInterval = 0f;

        [Tooltip("ON：お宝に当たったら、ボタンを離して押し直すまで止まる（ドリルが引っかかるイメージ。お宝を削りすぎないように）")]
        public bool stopOnTreasure = false;

        [Header("音（空欄なら DigEffects の音を使う）")]
        [Tooltip("掘ったときの音を、この道具だけ差し替える")]
        public AudioClip digClip;

        [Tooltip("押している間ずっと鳴る音（ドリルの「ギュイーン」など）。空欄なら鳴らさない")]
        public AudioClip loopClip;

        /// <summary>
        /// 同じ中身のコピーを作る（強化したときに、Inspector の元の値を書き換えないようにするため）
        /// </summary>
        public DigTool Clone()
        {
            // MemberwiseClone：フィールドを全部そのまま写した、別のインスタンスを作る
            return (DigTool)MemberwiseClone();
        }
    }
}
