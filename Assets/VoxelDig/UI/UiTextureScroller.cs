using UnityEngine;
using UnityEngine.UI;

namespace VoxelDig
{
    /// <summary>
    /// UI の背景に、模様のテクスチャを敷き詰めてスクロールさせる（パンの線画が斜めに流れる背景など）
    ///
    /// RawImage の「UV Rect」をずらしているだけなので、専用のシェーダーは不要
    ///
    /// 使い方：
    ///   1. 模様のテクスチャ（上下左右がつながる絵）の Wrap Mode を Repeat にする
    ///   2. Canvas に RawImage を作り、画面いっぱいに広げて、Texture に模様を設定する
    ///   3. このコンポーネントを付ける
    ///
    /// 背景の色は、RawImage の後ろに単色の Image を置くか、模様のテクスチャ自体に塗っておく
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class UiTextureScroller : MonoBehaviour
    {
        [Tooltip("流れる速さ（Canvas の単位 / 秒）。X がプラスで右へ、Y がプラスで上へ流れる。例：(30, -30) で右下へ")]
        [SerializeField] private Vector2 scrollSpeed = new Vector2(30f, -30f);

        [Tooltip("模様1枚分の大きさ（Canvas の単位）。画面の大きさが変わっても、模様の大きさは変わらない")]
        [SerializeField] private float tileSize = 256f;

        [Tooltip("ON なら、ポーズ中（Time.timeScale = 0）でも流れ続ける")]
        [SerializeField] private bool useUnscaledTime = true;

        private RawImage rawImage;
        private RectTransform rectTransform;
        private Vector2 offset; // 今どれだけずれているか（模様何枚分か）

        private void Awake()
        {
            rawImage = GetComponent<RawImage>();
            rectTransform = (RectTransform)transform;
        }

        private void Update()
        {
            if (tileSize <= 0f) return;

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            // 「Canvas の単位 / 秒」→「模様何枚分 / 秒」に直して、ずらす量を進める
            // UV を増やすと模様は逆向きに動いて見えるので、マイナスにして直感どおりの向きにする
            offset -= scrollSpeed / tileSize * deltaTime;

            // 0〜1 の範囲に戻す（どこまでも大きな数にならないように。模様はつながっているので見た目は変わらない）
            offset.x = Mathf.Repeat(offset.x, 1f);
            offset.y = Mathf.Repeat(offset.y, 1f);

            // 表示する大きさの中に、模様を何枚並べるか（画面が広いほど枚数が増える）
            Vector2 tiles = rectTransform.rect.size / tileSize;

            // UV Rect：どこから（offset）、何枚分（tiles）を表示するか
            rawImage.uvRect = new Rect(offset, tiles);
        }
    }
}
