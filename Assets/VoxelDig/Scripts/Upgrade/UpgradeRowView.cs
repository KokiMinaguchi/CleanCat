using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VoxelDig
{
    /// <summary>
    /// 強化パネルの1行分（「ハンマー：範囲 Lv2  範囲 2.1 → 2.35  ブロック 384  [強化]」）
    /// UpgradePanel が、強化の項目の数だけ複製して使う
    /// </summary>
    public class UpgradeRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TMP_Text upgradeButtonText;

        [Header("費用の文字の色")]
        [SerializeField] private Color enoughColor = new Color(0.29f, 0.27f, 0.25f);
        [SerializeField] private Color notEnoughColor = new Color(0.9f, 0.3f, 0.25f);

        private ToolUpgradeSystem system;
        private int upgradeIndex;
        private Tween punch;

        /// <summary>
        /// どの強化の行かを設定する（UpgradePanel から呼ばれる）
        /// </summary>
        public void Setup(ToolUpgradeSystem upgradeSystem, int index)
        {
            system = upgradeSystem;
            upgradeIndex = index;

            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(OnClickUpgrade);

            Refresh();
        }

        private void OnDestroy()
        {
            punch?.Kill();
        }

        /// <summary>表示を今の状態に合わせる</summary>
        public void Refresh()
        {
            if (system == null) return;

            ToolUpgradeDefinition definition = system.Upgrades[upgradeIndex];
            int level = system.GetLevel(upgradeIndex);
            bool isMax = system.IsMaxLevel(upgradeIndex);

            if (nameText != null) nameText.SetText(definition.displayName);
            if (levelText != null) levelText.SetText(isMax ? "Lv MAX" : $"Lv {level}/{definition.maxLevel}");
            if (valueText != null) valueText.SetText(system.GetValueText(upgradeIndex));

            if (costText != null)
            {
                if (isMax)
                {
                    costText.SetText("");
                }
                else
                {
                    // 足りないときは赤くする
                    int cost = system.GetCost(upgradeIndex);
                    costText.color = system.Blocks.CurrentValue >= cost ? enoughColor : notEnoughColor;
                    costText.SetText("ブロック {0}", cost);
                }
            }

            upgradeButton.interactable = system.CanUpgrade(upgradeIndex);
            if (upgradeButtonText != null) upgradeButtonText.SetText(isMax ? "MAX" : "強化");
        }

        private void OnClickUpgrade()
        {
            if (!system.TryUpgrade(upgradeIndex)) return;

            // 強化できたら、行をぷるっと揺らす
            punch?.Kill(true);
            punch = transform.DOPunchScale(Vector3.one * 0.06f, 0.25f, 8).SetUpdate(true);
        }
    }
}
