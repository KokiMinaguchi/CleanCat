using System;
using UnityEngine;

namespace VoxelDig
{
    /// <summary>
    /// 選択カード（DigSiteCard）をどこに出すかの決め方
    /// </summary>
    public enum CardPlacementMode
    {
        /// <summary>ブロックの左右の空いている方に出す（吹き出しのしっぽ付き）</summary>
        AutoSide,

        /// <summary>基準点（ブロックの中心、または Card Anchor Point）から、画面上で決まった量だけずらした位置に出す</summary>
        AnchorOffset,

        /// <summary>画面上の決まった位置に出す（ブロックには付いていかない）</summary>
        FixedScreen,
    }

    /// <summary>
    /// 選択カードの出し方の設定
    /// DigSiteCard（全体の初期設定）と DigSite（発掘ポイントごとの上書き）の両方で使う
    /// </summary>
    [Serializable]
    public class CardPlacement
    {
        [Tooltip("AutoSide：ブロックの左右に自動 ／ AnchorOffset：基準点からずらす ／ FixedScreen：画面の決まった位置")]
        public CardPlacementMode mode = CardPlacementMode.AutoSide;

        [Tooltip("【AutoSide】ブロックの中心からカードの端までの横の距離（Canvas の単位）")]
        public float sideGap = 120f;

        [Tooltip("【AnchorOffset】基準点からカードの中心までのずれ（Canvas の単位）。例：(0, 250) でブロックの上")]
        public Vector2 screenOffset = new Vector2(0f, 250f);

        [Tooltip("【FixedScreen】画面上の位置（0〜1。(0,0)=左下、(0.5,0.5)=中央、(1,1)=右上）")]
        public Vector2 viewportPosition = new Vector2(0.5f, 0.25f);
    }
}
