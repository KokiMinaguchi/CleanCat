using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace VoxelDig.EditorTools
{
    /// <summary>
    /// 立体ピクロス風の「選択カード」UI を自動で作るエディタ拡張
    ///
    /// メニュー「Tools > VoxelDig > 選択カードを作成」を実行すると：
    ///   1. 角丸・影・★・しっぽのスプライト（画像）を Assets/VoxelDig/UI/Generated に作る
    ///   2. シーンに Canvas と DigSiteCard（カード一式）とフェード用の黒い画面を作る
    ///   3. シーンにある ExploreSceneController の Card と Fade Overlay に自動で設定する
    ///
    /// ※ Editor フォルダに入っているので、ビルドには含まれない
    /// ※ Project ウィンドウで日本語の TMP フォントアセットを選んでから実行すると、そのフォントを使う
    /// </summary>
    public static partial class VoxelDigUiBuilder
    {
        // 生成した画像を保存する場所
        private const string SpriteFolder = "Assets/VoxelDig/UI/Generated";

        // ---- 色（立体ピクロス風の、白っぽくて柔らかい配色）----
        private static readonly Color CardColor = Hex("FBFAF7");     // カードの白
        private static readonly Color ThemeColor = Hex("F39A2B");    // ヘッダーのオレンジ（DigSite ごとに上書きされる）
        private static readonly Color TitleColor = Hex("4A4540");    // 名前の文字
        private static readonly Color ChipColor = Hex("EFECE6");     // サイズ・★の背景
        private static readonly Color ChipTextColor = Hex("6D665E");
        private static readonly Color ButtonColor = Hex("5CC56A");   // 「掘る」の緑
        private static readonly Color ButtonEdgeColor = Hex("3E9A4B"); // ボタンの下の濃い緑（厚みに見せる）
        private static readonly Color HintColor = Hex("A39B90");

        // 角丸スプライトの角の半径（ピクセル）。Image の pixelsPerUnitMultiplier で見た目の角の大きさを変える
        private const float SpriteRadius = 48f;

        [MenuItem("Tools/VoxelDig/選択カードを作成")]
        private static void CreateSelectCard()
        {
            // ---- 1. スプライトを作る ----
            Sprite rounded = CreateRoundedRectSprite("RoundedRect.png");
            Sprite shadow = CreateShadowSprite("SoftShadow.png");
            Sprite star = CreateStarSprite("Star.png");
            Sprite tail = CreateTailSprite("Tail.png");

            // フォント：Project で TMP フォントアセットを選んでいればそれ、無ければ TMP の標準フォント
            TMP_FontAsset font = Selection.activeObject as TMP_FontAsset;
            if (font == null) font = TMP_Settings.defaultFontAsset;

            // ---- 2. Canvas を作る ----
            var canvasGo = new GameObject("VoxelDigCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            // 画面サイズが変わっても同じ見た目になるよう、1920×1080 を基準に拡大縮小する
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create VoxelDig Select Card");

            // ---- 3. カードを作る ----
            DigSiteCard card = BuildCard(canvasGo.transform, rounded, shadow, star, tail, font);

            // ---- 4. フェード用の黒い画面（一番手前に置く）----
            CanvasGroup fade = BuildFadeOverlay(canvasGo.transform);

            // ---- 5. EventSystem が無ければ作る（Input System 用のモジュールで）----
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }

            // ---- 6. ExploreSceneController に設定する ----
            var controller = Object.FindFirstObjectByType<ExploreSceneController>();
            if (controller != null)
            {
                var so = new SerializedObject(controller);
                so.FindProperty("card").objectReferenceValue = card;
                so.FindProperty("fadeOverlay").objectReferenceValue = fade;
                so.ApplyModifiedProperties();
                Debug.Log("[VoxelDigUiBuilder] ExploreSceneController に Card と Fade Overlay を設定しました", controller);
            }
            else
            {
                Debug.LogWarning("[VoxelDigUiBuilder] ExploreSceneController が見つからないので、Card と Fade Overlay は手動で設定してください");
            }

            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Selection.activeGameObject = card.gameObject;
            Debug.Log("[VoxelDigUiBuilder] 選択カードを作成しました。古い選択パネル（Canvas）は削除してください");
        }

        // ===================================================================
        // カードの組み立て
        // ===================================================================

        private static DigSiteCard BuildCard(Transform parent, Sprite rounded, Sprite shadow, Sprite star, Sprite tailSprite, TMP_FontAsset font)
        {
            // カードの大きさ：幅 460 × 高さ 400（1920×1080 基準）
            RectTransform cardRect = CreateRect("DigSiteCard", parent);
            SetRect(cardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 400));
            cardRect.gameObject.AddComponent<CanvasGroup>();
            var card = cardRect.gameObject.AddComponent<DigSiteCard>();

            // ---- 影（カードより少し下にずらして、ふわっと浮いて見せる）----
            RectTransform shadowRect = CreateRect("Shadow", cardRect);
            Stretch(shadowRect, new Vector2(-24, -40), new Vector2(24, 4));
            AddImage(shadowRect, shadow, new Color(0f, 0f, 0f, 0.45f), 1f, false);

            // ---- しっぽ（吹き出しの三角。位置と向きは DigSiteCard が動かす）----
            RectTransform tailRect = CreateRect("Tail", cardRect);
            SetRect(tailRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(228, 0), new Vector2(30, 46));
            Image tailImage = AddImage(tailRect, tailSprite, CardColor, 1f, false);
            tailImage.type = Image.Type.Simple;

            // ---- 本体（白い角丸の板）----
            RectTransform body = CreateRect("Body", cardRect);
            Stretch(body, Vector2.zero, Vector2.zero);
            AddImage(body, rounded, CardColor, SpriteRadius / 28f, true); // 角の半径 28

            // ---- ヘッダー（色付きの帯）----
            RectTransform header = CreateRect("Header", body);
            SetRect(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 64));
            Image headerImage = AddImage(header, rounded, ThemeColor, SpriteRadius / 28f, false);

            // ヘッダーの下側の角を四角くするための板（上側だけ角丸に見せる）
            RectTransform headerFill = CreateRect("HeaderFill", body);
            SetRect(headerFill, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -32), new Vector2(0, 32));
            Image headerFillImage = AddImage(headerFill, null, ThemeColor, 1f, false);
            headerFillImage.type = Image.Type.Simple;
            headerFill.SetSiblingIndex(header.GetSiblingIndex()); // ヘッダーより奥に描く

            // 番号「No.01」（白い角丸の中にテーマ色の文字）
            RectTransform numberPill = CreateRect("NumberPill", header);
            SetRect(numberPill, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(124, 40));
            AddImage(numberPill, rounded, Color.white, SpriteRadius / 14f, false);
            RectTransform numberTextRect = CreateRect("NumberText", numberPill);
            Stretch(numberTextRect, Vector2.zero, Vector2.zero);
            TMP_Text numberText = AddText(numberTextRect, "No.01", 26, ThemeColor, font);

            // 閉じるボタン「×」（半透明の白い丸）
            RectTransform closeRect = CreateRect("CloseButton", header);
            SetRect(closeRect, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(44, 44));
            Image closeImage = AddImage(closeRect, rounded, new Color(1f, 1f, 1f, 0.3f), SpriteRadius / 22f, true);
            Button closeButton = closeRect.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            RectTransform closeTextRect = CreateRect("Text", closeRect);
            Stretch(closeTextRect, Vector2.zero, new Vector2(0, 2));
            AddText(closeTextRect, "×", 32, Color.white, font);

            // ---- 名前（掘るまでは「？？？」）----
            RectTransform titleRect = CreateRect("Title", body);
            SetRect(titleRect, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(-40, 76));
            TMP_Text titleText = AddText(titleRect, "？？？", 54, TitleColor, font);
            titleText.characterSpacing = 20;
            // 長い名前でもはみ出さないように、自動で文字を小さくする
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 24;
            titleText.fontSizeMax = 54;

            // ---- サイズと難しさのチップ（2つ並べる）----
            const float chipTop = -170f;
            const float sizeChipWidth = 200f;
            const float starChipWidth = 176f;
            const float chipGap = 12f;
            float chipStartX = -(sizeChipWidth + chipGap + starChipWidth) * 0.5f;

            RectTransform sizeChip = CreateRect("SizeChip", body);
            SetRect(sizeChip, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(chipStartX, chipTop), new Vector2(sizeChipWidth, 46));
            AddImage(sizeChip, rounded, ChipColor, SpriteRadius / 16f, false);
            RectTransform sizeTextRect = CreateRect("SizeText", sizeChip);
            Stretch(sizeTextRect, Vector2.zero, Vector2.zero);
            TMP_Text sizeText = AddText(sizeTextRect, "40×39×40", 24, ChipTextColor, font);

            RectTransform starChip = CreateRect("StarChip", body);
            SetRect(starChip, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 1),
                new Vector2(chipStartX + sizeChipWidth + chipGap, chipTop), new Vector2(starChipWidth, 46));
            AddImage(starChip, rounded, ChipColor, SpriteRadius / 16f, false);

            var stars = new Image[5];
            for (int i = 0; i < stars.Length; i++)
            {
                RectTransform starRect = CreateRect($"Star{i + 1}", starChip);
                // 5個を 30 間隔で、チップの中央に並べる
                SetRect(starRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((i - 2) * 30f, 1f), new Vector2(28, 28));
                stars[i] = AddImage(starRect, star, Hex("F3B72B"), 1f, false);
                stars[i].type = Image.Type.Simple;
            }

            // ---- 「掘る」ボタン（下に濃い色を敷いて、厚みのあるボタンに見せる）----
            RectTransform buttonEdge = CreateRect("DigButtonEdge", body);
            SetRect(buttonEdge, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(392, 84));
            AddImage(buttonEdge, rounded, ButtonEdgeColor, SpriteRadius / 42f, false);

            RectTransform digRect = CreateRect("DigButton", body);
            SetRect(digRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 64), new Vector2(392, 84));
            Image digImage = AddImage(digRect, rounded, ButtonColor, SpriteRadius / 42f, true);
            Button digButton = digRect.gameObject.AddComponent<Button>();
            digButton.targetGraphic = digImage;
            ColorBlock colors = digButton.colors;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f); // マウスを乗せると少し明るく
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);     // 押すと少し暗く
            digButton.colors = colors;

            RectTransform digTextRect = CreateRect("Text", digRect);
            Stretch(digTextRect, Vector2.zero, Vector2.zero);
            TMP_Text digText = AddText(digTextRect, "掘る", 40, Color.white, font);
            digText.characterSpacing = 30;

            // ---- 操作ヒント ----
            RectTransform hintRect = CreateRect("Hint", body);
            SetRect(hintRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(300, 30));
            TMP_Text hint = AddText(hintRect, "Esc で閉じる", 20, HintColor, font);
            hint.fontStyle = FontStyles.Normal;

            // ---- DigSiteCard に部品を設定する ----
            var so = new SerializedObject(card);
            so.FindProperty("targetCamera").objectReferenceValue = Camera.main;
            so.FindProperty("headerImage").objectReferenceValue = headerImage;
            so.FindProperty("headerFillImage").objectReferenceValue = headerFillImage;
            so.FindProperty("numberText").objectReferenceValue = numberText;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("sizeText").objectReferenceValue = sizeText;
            so.FindProperty("digButton").objectReferenceValue = digButton;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("tail").objectReferenceValue = tailRect;
            so.FindProperty("digButtonText").objectReferenceValue = digText;
            SerializedProperty starsProp = so.FindProperty("starImages");
            starsProp.arraySize = stars.Length;
            for (int i = 0; i < stars.Length; i++)
            {
                starsProp.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            }
            so.ApplyModifiedProperties();

            return card;
        }

        /// <summary>画面全体を覆う黒い画面（シーン切り替えの暗転用）</summary>
        private static CanvasGroup BuildFadeOverlay(Transform parent)
        {
            RectTransform rect = CreateRect("FadeOverlay", parent);
            Stretch(rect, Vector2.zero, Vector2.zero);
            AddImage(rect, null, Color.black, 1f, true).type = Image.Type.Simple;

            // 編集中は邪魔にならないよう透明にしておく（再生するとスクリプトが暗転させる）
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            rect.SetAsLastSibling(); // 一番手前に描く
            return group;
        }

        // ===================================================================
        // UI を作る小さな便利関数
        // ===================================================================

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>アンカー・ピボット・位置・大きさをまとめて設定する</summary>
        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
        }

        /// <summary>親いっぱいに広げる（offsetMin = 左下のずれ、offsetMax = 右上のずれ）</summary>
        private static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        /// <summary>
        /// Image を付ける
        /// 角丸スプライトは Sliced（9スライス）にして、pixelsPerUnitMultiplier で角の大きさを調整する
        /// （値が大きいほど角が小さくなる。角の半径 = 48 ÷ この値）
        /// </summary>
        private static Image AddImage(RectTransform rt, Sprite sprite, Color color, float pixelsPerUnitMultiplier, bool raycastTarget)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            if (sprite != null)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            }
            return image;
        }

        private static TMP_Text AddText(RectTransform rt, string text, float size, Color color, TMP_FontAsset font)
        {
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false; // 文字はクリックを受け取らない（ボタン本体が受け取る）
            return tmp;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }

        // ===================================================================
        // スプライト（画像）をコードで描いて保存する
        // ===================================================================

        /// <summary>白い角丸四角（9スライス用。どんな大きさにも伸ばせる）</summary>
        private static Sprite CreateRoundedRectSprite(string fileName)
        {
            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedRectDistance(x + 0.5f, y + 0.5f, size, 0f, SpriteRadius);
                // 境界から 0.5 ピクセルの範囲でなめらかに透明にする（ギザギザ防止）
                float a = Mathf.Clamp01(0.5f - d);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            return SaveSprite(fileName, size, pixels, SpriteRadius);
        }

        /// <summary>ふんわりした影（角丸四角をぼかしたもの）</summary>
        private static Sprite CreateShadowSprite(string fileName)
        {
            const int size = 128;
            const float inset = 20f; // ぼかしの分だけ内側に描く
            const float blur = 18f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedRectDistance(x + 0.5f, y + 0.5f, size, inset, SpriteRadius - inset * 0.5f);
                // 形の内側は濃く、外へ向かって blur の距離で薄くなる
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-blur * 0.5f, blur, d));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            return SaveSprite(fileName, size, pixels, 60f);
        }

        /// <summary>5つの角がある★</summary>
        private static Sprite CreateStarSprite(string fileName)
        {
            const int size = 128;
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                // 外側と内側の点を交互に置く（上向きの★）
                float radius = i % 2 == 0 ? 60f : 26f;
                float angle = Mathf.Deg2Rad * (90f + i * 36f);
                points[i] = new Vector2(64f + Mathf.Cos(angle) * radius, 66f + Mathf.Sin(angle) * radius);
            }
            return SavePolygonSprite(fileName, size, points);
        }

        /// <summary>吹き出しのしっぽ（右向きの三角形）</summary>
        private static Sprite CreateTailSprite(string fileName)
        {
            const int size = 64;
            var points = new[] { new Vector2(0, 2), new Vector2(0, 62), new Vector2(60, 32) };
            return SavePolygonSprite(fileName, size, points);
        }

        /// <summary>
        /// 角丸四角の「縁からの距離」を求める（中なら マイナス、外なら プラス）
        /// </summary>
        private static float RoundedRectDistance(float px, float py, int size, float inset, float radius)
        {
            float half = size * 0.5f - inset;
            float qx = Mathf.Abs(px - size * 0.5f) - (half - radius);
            float qy = Mathf.Abs(py - size * 0.5f) - (half - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - radius;
        }

        /// <summary>多角形を塗りつぶした画像を作る（1ピクセルを 4×4 に分けて調べ、縁をなめらかにする）</summary>
        private static Sprite SavePolygonSprite(string fileName, int size, Vector2[] polygon)
        {
            const int samples = 4;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    var p = new Vector2(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples);
                    if (IsInsidePolygon(p, polygon)) hit++;
                }
                pixels[y * size + x] = new Color(1f, 1f, 1f, hit / (float)(samples * samples));
            }
            return SaveSprite(fileName, size, pixels, 0f);
        }

        /// <summary>点が多角形の中にあるか（交差回数で判定する定番の方法）</summary>
        private static bool IsInsidePolygon(Vector2 p, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) &&
                    p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// ピクセルを PNG に保存して、スプライトとして読み込む
        /// </summary>
        /// <param name="border">9スライスの境界（ピクセル）。0 なら 9スライスしない</param>
        private static Sprite SaveSprite(string fileName, int size, Color32[] pixels, float border)
        {
            if (!Directory.Exists(SpriteFolder)) Directory.CreateDirectory(SpriteFolder);
            string path = $"{SpriteFolder}/{fileName}";

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);

            // スプライトとして読み込む設定
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
