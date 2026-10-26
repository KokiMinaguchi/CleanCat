using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VoxelDig
{
    /// <summary>
    /// 強化パネル（画面）
    /// ・「強化」ボタンでパネルを開き、「閉じる」ボタン（または Esc）で閉じる
    /// ・強化の項目の数だけ、行（UpgradeRowView）をお手本から複製して並べる
    /// ・持っているブロックの数を表示する（パネルを閉じていても HUD に出しておける）
    ///
    /// パネルの後ろには、画面全体を覆う半透明の幕（Raycast Target ON）を置いておく。
    /// 開いている間は幕がクリックを受け止めるので、パネルの外をクリックしてもブロックを掘らない。
    /// </summary>
    public class UpgradePanel : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private ToolUpgradeSystem system;

        [Header("パネル")]
        [Tooltip("パネル全体（後ろの幕も含める）。開くと表示、閉じると非表示になる")]
        [SerializeField] private CanvasGroup panelGroup;

        [Tooltip("パネルを開くボタン（HUD の「強化」）")]
        [SerializeField] private Button openButton;

        [Tooltip("パネルを閉じるボタン")]
        [SerializeField] private Button closeButton;

        [Header("行")]
        [Tooltip("行のお手本（パネルの中に置いて非表示にしておく。これを複製して並べる）")]
        [SerializeField] private UpgradeRowView rowTemplate;

        [Tooltip("行を並べる親（Vertical Layout Group を付けたもの）")]
        [SerializeField] private Transform rowContainer;

        [Header("素材の表示")]
        [Tooltip("ブロックの数を出すテキスト（HUD 用とパネル用など、いくつでも登録できる）")]
        [SerializeField] private TMP_Text[] materialTexts;

        [Tooltip("開く・閉じるアニメーションの時間（秒）")]
        [SerializeField] private float fadeDuration = 0.15f;

        private readonly List<UpgradeRowView> rows = new List<UpgradeRowView>();
        private bool isOpen;

        /// <summary>パネルが開いているか</summary>
        public bool IsOpen => isOpen;

        private void Start()
        {
            // 最初は閉じておく
            panelGroup.SetVisible(false, 0f);

            if (openButton != null) openButton.onClick.AddListener(Toggle);
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            if (system == null)
            {
                Debug.LogError("[UpgradePanel] System（ToolUpgradeSystem）が設定されていません", this);
                return;
            }

            CreateRows();

            // ブロックの数が変わったら：数の表示を更新し、開いていれば行（費用の色・ボタン）も更新する
            system.Blocks
                .Subscribe(count =>
                {
                    UpdateMaterialTexts(count);
                    if (isOpen) RefreshRows();
                })
                .AddTo(this);

            // 強化したら、全部の行を更新する（同じ道具のほかの行の「今 → 次」の値も変わることがあるため）
            system.Upgraded
                .Subscribe(_ => RefreshRows())
                .AddTo(this);
        }

        // ---------------------------------------------------------------
        // 開く・閉じる
        // ---------------------------------------------------------------

        public void Toggle()
        {
            if (isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (isOpen) return;
            isOpen = true;

            // 開く前に、最新の状態にしておく
            RefreshRows();
            panelGroup.SetVisible(true, fadeDuration).Forget();
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            panelGroup.SetVisible(false, fadeDuration).Forget();
        }

        // ---------------------------------------------------------------
        // 行
        // ---------------------------------------------------------------

        /// <summary>強化の項目の数だけ、お手本を複製して並べる</summary>
        private void CreateRows()
        {
            if (rowTemplate == null || rowContainer == null)
            {
                Debug.LogError("[UpgradePanel] Row Template と Row Container を設定してください", this);
                return;
            }

            // お手本自体は表示しない
            rowTemplate.gameObject.SetActive(false);

            for (int i = 0; i < system.Upgrades.Count; i++)
            {
                UpgradeRowView row = Instantiate(rowTemplate, rowContainer);
                row.name = $"UpgradeRow_{system.Upgrades[i].id}";
                row.gameObject.SetActive(true);
                row.Setup(system, i);
                rows.Add(row);
            }
        }

        private void RefreshRows()
        {
            foreach (UpgradeRowView row in rows)
            {
                row.Refresh();
            }
        }

        private void UpdateMaterialTexts(int count)
        {
            foreach (TMP_Text text in materialTexts)
            {
                // SetText の {0} を使うと、文字列を毎回作らないので軽い（掘るたびに呼ばれるため）
                if (text != null) text.SetText("ブロック {0}", count);
            }
        }
    }
}
