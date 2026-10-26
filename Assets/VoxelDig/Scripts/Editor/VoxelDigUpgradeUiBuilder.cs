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
    /// 強化パネルの UI を自動で作るエディタ拡張（VoxelDigUiBuilder の続き）
    ///
    /// 発掘シーン（Test シーン）を開いて、メニュー「Tools > VoxelDig > 強化パネルを作成」を実行すると：
    ///   1. Canvas（UpgradeCanvas）に、素材の表示・「強化」ボタン・強化パネルを作る
    ///   2. ToolUpgradeSystem が無ければ作って、DigController と DigBlock を設定する
    ///   3. UpgradePanel の参照と、DigSceneController の Upgrade Panel を自動で設定する
    ///
    /// ※ partial なので、VoxelDigUiBuilder.cs の便利関数（CreateRect / AddImage / AddText など）をそのまま使える
    /// ※ Project ウィンドウで日本語の TMP フォントアセットを選んでから実行すると、そのフォントを使う
    /// </summary>
    public static partial class VoxelDigUiBuilder
    {
        // 強化パネルの色
        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.45f); // 後ろの暗い幕
        private static readonly Color RowColor = Hex("EFECE6");                     // 行の背景
        private static readonly Color UpgradeButtonColor = Hex("F39A2B");           // 「強化」ボタンのオレンジ

        [MenuItem("Tools/VoxelDig/強化パネルを作成")]
        private static void CreateUpgradePanel()
        {
            Sprite rounded = CreateRoundedRectSprite("RoundedRect.png");

            TMP_FontAsset font = Selection.activeObject as TMP_FontAsset;
            if (font == null) font = TMP_Settings.defaultFontAsset;

            // ---- 1. Canvas ----
            var canvasGo = new GameObject("UpgradeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0; // 暗転（RuleTransition）より奥に描く。暗転の上に出てしまうときは小さくする

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Upgrade Panel");
            Transform root = canvasGo.transform;

            // ---- 2. HUD：素材の数（右上）----
            RectTransform hudChip = CreateRect("MaterialHud", root);
            SetRect(hudChip, Vector2.one, Vector2.one, Vector2.one, new Vector2(-40, -40), new Vector2(340, 64));
            AddImage(hudChip, rounded, CardColor, 2f, false);
            RectTransform hudTextRect = CreateRect("Text", hudChip);
            Stretch(hudTextRect, Vector2.zero, Vector2.zero);
            TMP_Text hudText = AddText(hudTextRect, "ブロック 0", 30, TitleColor, font);

            // ---- 3. HUD：「強化」ボタン（右下）----
            Button openButton = CreateButton("OpenUpgradeButton", root, rounded, UpgradeButtonColor, "強化", 36, font, out _);
            SetRect((RectTransform)openButton.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(200, 84));

            // ---- 4. パネル全体（後ろの幕 + ウィンドウ）----
            RectTransform panelRect = CreateRect("UpgradePanel", root);
            Stretch(panelRect, Vector2.zero, Vector2.zero);
            // 画面全体を覆う暗い幕。Raycast Target を ON にして、パネルの外のクリックでブロックを掘らないようにする
            AddImage(panelRect, null, BackdropColor, 1f, true);
            var panelGroup = panelRect.gameObject.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;

            RectTransform window = CreateRect("Window", panelRect);
            SetRect(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 780));
            AddImage(window, rounded, CardColor, 1f, true);

            // タイトル
            RectTransform titleRect = CreateRect("Title", window);
            SetRect(titleRect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -28), new Vector2(400, 64));
            TMP_Text title = AddText(titleRect, "道具の強化", 44, TitleColor, font);
            title.alignment = TextAlignmentOptions.MidlineLeft;

            // パネルの中の素材の数（タイトルの右）
            RectTransform panelMaterialRect = CreateRect("Materials", window);
            SetRect(panelMaterialRect, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-120, -28), new Vector2(360, 64));
            TMP_Text panelMaterialText = AddText(panelMaterialRect, "ブロック 0", 30, ChipTextColor, font);
            panelMaterialText.alignment = TextAlignmentOptions.MidlineRight;

            // 閉じるボタン（右上の ×）
            Button closeButton = CreateButton("CloseButton", window, rounded, ChipColor, "×", 40, font, out TMP_Text closeText);
            closeText.color = ChipTextColor;
            SetRect((RectTransform)closeButton.transform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-28, -28), new Vector2(64, 64));

            // ---- 5. 行を並べるスクロールの一覧 ----
            RectTransform scrollRect = CreateRect("List", window);
            Stretch(scrollRect, new Vector2(32, 32), new Vector2(-32, -120));
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            RectTransform viewport = CreateRect("Viewport", scrollRect);
            Stretch(viewport, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>(); // はみ出した行を隠す
            // スクロール操作を受け取るための透明な Image
            AddImage(viewport, null, new Color(1f, 1f, 1f, 0f), 1f, true);

            RectTransform content = CreateRect("Content", viewport);
            // 上端に合わせて、横いっぱい・高さは中身に合わせる
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;

            // ---- 6. 行のお手本 ----
            UpgradeRowView rowTemplate = BuildUpgradeRow(content, rounded, font);

            // ---- 7. EventSystem が無ければ作る ----
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }

            // ---- 8. ToolUpgradeSystem（無ければ作る）----
            var system = Object.FindFirstObjectByType<ToolUpgradeSystem>();
            if (system == null)
            {
                var systemGo = new GameObject("ToolUpgradeSystem", typeof(ToolUpgradeSystem));
                Undo.RegisterCreatedObjectUndo(systemGo, "Create ToolUpgradeSystem");
                system = systemGo.GetComponent<ToolUpgradeSystem>();
            }

            var systemSo = new SerializedObject(system);
            if (systemSo.FindProperty("controller").objectReferenceValue == null)
                systemSo.FindProperty("controller").objectReferenceValue = Object.FindFirstObjectByType<DigController>();
            if (systemSo.FindProperty("block").objectReferenceValue == null)
                systemSo.FindProperty("block").objectReferenceValue = Object.FindFirstObjectByType<DigBlock>();
            systemSo.ApplyModifiedProperties();

            // ---- 9. UpgradePanel ----
            var upgradePanel = canvasGo.AddComponent<UpgradePanel>();
            var panelSo = new SerializedObject(upgradePanel);
            panelSo.FindProperty("system").objectReferenceValue = system;
            panelSo.FindProperty("panelGroup").objectReferenceValue = panelGroup;
            panelSo.FindProperty("openButton").objectReferenceValue = openButton;
            panelSo.FindProperty("closeButton").objectReferenceValue = closeButton;
            panelSo.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
            panelSo.FindProperty("rowContainer").objectReferenceValue = content;
            SerializedProperty texts = panelSo.FindProperty("materialTexts");
            texts.arraySize = 2;
            texts.GetArrayElementAtIndex(0).objectReferenceValue = hudText;
            texts.GetArrayElementAtIndex(1).objectReferenceValue = panelMaterialText;
            panelSo.ApplyModifiedProperties();

            // ---- 10. DigSceneController に設定（Esc でパネルを閉じるため）----
            var sceneController = Object.FindFirstObjectByType<DigSceneController>();
            if (sceneController != null)
            {
                var so = new SerializedObject(sceneController);
                so.FindProperty("upgradePanel").objectReferenceValue = upgradePanel;
                so.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Selection.activeGameObject = system.gameObject;
            Debug.Log("[VoxelDigUiBuilder] 強化パネルを作成しました。ToolUpgradeSystem の Controller / Block が正しいか確認してください");
        }

        /// <summary>
        /// 強化の1行（お手本）を作る
        /// ［ハンマー：範囲  Lv 0/5］                ［ブロック 150］［強化］
        /// ［範囲 1.6 → 1.85      ］
        /// </summary>
        private static UpgradeRowView BuildUpgradeRow(Transform parent, Sprite rounded, TMP_FontAsset font)
        {
            RectTransform row = CreateRect("UpgradeRowTemplate", parent);
            AddImage(row, rounded, RowColor, 2f, false);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 104;

            // 名前（左上）
            RectTransform nameRect = CreateRect("Name", row);
            SetRect(nameRect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -12), new Vector2(330, 44));
            TMP_Text nameText = AddText(nameRect, "ハンマー：範囲", 30, TitleColor, font);
            nameText.alignment = TextAlignmentOptions.MidlineLeft;

            // レベル（名前の右）
            RectTransform levelRect = CreateRect("Level", row);
            SetRect(levelRect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(370, -12), new Vector2(150, 44));
            TMP_Text levelText = AddText(levelRect, "Lv 0/5", 26, ThemeColor, font);
            levelText.alignment = TextAlignmentOptions.MidlineLeft;

            // 今 → 次 の値（左下）
            RectTransform valueRect = CreateRect("Value", row);
            SetRect(valueRect, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 12), new Vector2(480, 40));
            TMP_Text valueText = AddText(valueRect, "範囲 1.6 → 1.85", 24, ChipTextColor, font);
            valueText.alignment = TextAlignmentOptions.MidlineLeft;
            valueText.fontStyle = FontStyles.Normal;

            // 強化ボタン（右）
            Button button = CreateButton("UpgradeButton", row, rounded, ButtonColor, "強化", 30, font, out TMP_Text buttonText);
            SetRect((RectTransform)button.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(150, 68));

            // 費用（ボタンの左）
            RectTransform costRect = CreateRect("Cost", row);
            SetRect(costRect, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-190, 0), new Vector2(240, 56));
            TMP_Text costText = AddText(costRect, "ブロック 150", 26, TitleColor, font);
            costText.alignment = TextAlignmentOptions.MidlineRight;

            // UpgradeRowView に参照を設定する
            var view = row.gameObject.AddComponent<UpgradeRowView>();
            var so = new SerializedObject(view);
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("levelText").objectReferenceValue = levelText;
            so.FindProperty("valueText").objectReferenceValue = valueText;
            so.FindProperty("costText").objectReferenceValue = costText;
            so.FindProperty("upgradeButton").objectReferenceValue = button;
            so.FindProperty("upgradeButtonText").objectReferenceValue = buttonText;
            so.ApplyModifiedProperties();

            return view;
        }

        /// <summary>角丸のボタン（文字つき）を作る</summary>
        private static Button CreateButton(string name, Transform parent, Sprite rounded, Color color, string label, float fontSize, TMP_FontAsset font, out TMP_Text labelText)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = AddImage(rect, rounded, color, 2f, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            // 押せないとき（素材が足りないとき）は、はっきり薄くする
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
            button.colors = colors;

            RectTransform textRect = CreateRect("Text", rect);
            Stretch(textRect, Vector2.zero, Vector2.zero);
            labelText = AddText(textRect, label, fontSize, Color.white, font);
            return button;
        }
    }
}
