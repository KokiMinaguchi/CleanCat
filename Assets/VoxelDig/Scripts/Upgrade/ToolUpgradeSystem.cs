using System.Collections.Generic;
using R3;
using UnityEngine;

namespace VoxelDig
{
    /// <summary>
    /// 壊したブロックを素材にして、道具を強化するシステム
    ///
    /// ・掘って壊したマスの数だけ「ブロック」が貯まる（土1マス = 1個、石1マス = 2個 など、Inspector で変えられる）
    /// ・素材を使って、道具の範囲・速さ・威力・ドリルの深さを強化できる
    /// ・素材とレベルは PlayerPrefs に保存されるので、シーンを移動しても、ゲームを終了しても残る
    ///
    /// 使い方：
    ///   発掘シーンの空の GameObject に付けて、Controller と Block を設定する
    ///   強化の項目は Upgrades の一覧で増やしたり、費用を変えたりできる
    ///   画面は UpgradePanel が担当する（Tools > VoxelDig > 強化パネルを作成 で自動で作れる）
    /// </summary>
    public class ToolUpgradeSystem : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("強化した性能を反映する DigController")]
        [SerializeField] private DigController controller;

        [Tooltip("クリア演出で崩れた土をボーナスとして数えるときに使う（Reveal Reward Rate が 0 なら不要）")]
        [SerializeField] private DigBlock block;

        [Header("素材の獲得")]
        [Tooltip("土を1マス壊したときにもらえるブロックの数")]
        [Min(0)]
        [SerializeField] private int blocksPerDirt = 1;

        [Tooltip("石を1マス壊したときにもらえるブロックの数（石は硬くて手間がかかるので、多めにしている）")]
        [Min(0)]
        [SerializeField] private int blocksPerStone = 2;

        [Tooltip("クリア演出で自動で崩れた土・石を、何割もらえるか（0 = もらえない、0.5 = 半分、1 = 全部）")]
        [Range(0f, 1f)]
        [SerializeField] private float revealRewardRate = 0.3f;

        [Header("強化の一覧")]
        [SerializeField] private ToolUpgradeDefinition[] upgrades =
        {
            // ---- ハンマー（0番）----
            new ToolUpgradeDefinition { id = "hammer_radius", displayName = "ハンマー：範囲", toolIndex = 0, stat = UpgradeStat.Radius, amountPerLevel = 0.25f, maxLevel = 5, cost = 150, costGrowth = 1.6f },
            new ToolUpgradeDefinition { id = "hammer_speed", displayName = "ハンマー：連打の速さ", toolIndex = 0, stat = UpgradeStat.Speed, amountPerLevel = 0.85f, maxLevel = 5, cost = 140, costGrowth = 1.6f },
            new ToolUpgradeDefinition { id = "hammer_power", displayName = "ハンマー：威力", toolIndex = 0, stat = UpgradeStat.Power, amountPerLevel = 1f, maxLevel = 2, cost = 280, costGrowth = 2f },

            // ---- ドリル（2番）----
            new ToolUpgradeDefinition { id = "drill_radius", displayName = "ドリル：太さ", toolIndex = 2, stat = UpgradeStat.Radius, amountPerLevel = 0.2f, maxLevel = 4, cost = 220, costGrowth = 1.7f },
            new ToolUpgradeDefinition { id = "drill_speed", displayName = "ドリル：回転の速さ", toolIndex = 2, stat = UpgradeStat.Speed, amountPerLevel = 0.85f, maxLevel = 5, cost = 210, costGrowth = 1.6f },
            new ToolUpgradeDefinition { id = "drill_depth", displayName = "ドリル：掘る深さ", toolIndex = 2, stat = UpgradeStat.Depth, amountPerLevel = 1f, maxLevel = 4, cost = 290, costGrowth = 1.7f },
        };

        // ---- 状態（UI が購読する）----
        private readonly ReactiveProperty<int> blocks = new ReactiveProperty<int>(0);
        private readonly Subject<int> upgraded = new Subject<int>();

        /// <summary>持っているブロックの数（強化の素材）</summary>
        public ReadOnlyReactiveProperty<int> Blocks => blocks;

        /// <summary>強化したときに、強化した項目の番号が通知される</summary>
        public Observable<int> Upgraded => upgraded;

        /// <summary>強化の項目の一覧</summary>
        public IReadOnlyList<ToolUpgradeDefinition> Upgrades => upgrades;

        public DigController Controller => controller;

        private UpgradeSaveData saveData;
        private bool isDirty; // 保存していない変更があるか

        private void Awake()
        {
            // 保存してあるデータを読み込む
            saveData = UpgradeSaveData.Load();
            blocks.Value = saveData.blocks;
        }

        private void Start()
        {
            if (controller == null)
            {
                Debug.LogError("[ToolUpgradeSystem] Controller が設定されていません", this);
                return;
            }

            // 保存してあるレベルを、道具に反映する
            ApplyAll();

            // 掘るたびに、壊した土・石の数だけ素材を増やす
            controller.Dug
                .Subscribe(result => AddMaterials(result.broken, 1f))
                .AddTo(this);

            // クリア演出で崩れた土・石も、一部をボーナスとしてもらう
            if (block != null && revealRewardRate > 0f)
            {
                block.RevealBroken
                    .Subscribe(list => AddMaterials(list, revealRewardRate))
                    .AddTo(this);
            }
        }

        private void OnDisable()
        {
            // シーンを移動するとき・再生を止めるときに保存する
            SaveIfDirty(true);
        }

        private void OnApplicationPause(bool pause)
        {
            // スマホなどでアプリが裏に回ったときも保存しておく
            if (pause) SaveIfDirty(true);
        }

        private void OnDestroy()
        {
            blocks.Dispose();
            upgraded.Dispose();
        }

        // ---------------------------------------------------------------
        // 素材
        // ---------------------------------------------------------------

        /// <summary>
        /// 壊れたボクセルの一覧から、土・石の数を数えて、ブロックの数に足す
        /// </summary>
        /// <param name="rate">もらえる割合（クリア演出のボーナスのときに 1 より小さくする）</param>
        private void AddMaterials(IReadOnlyList<BrokenVoxel> broken, float rate)
        {
            int dirtCount = 0;
            int stoneCount = 0;
            for (int i = 0; i < broken.Count; i++)
            {
                if (broken[i].type == VoxelType.Dirt) dirtCount++;
                else if (broken[i].type == VoxelType.Stone) stoneCount++;
            }

            // 土と石をまとめて「ブロック」に換算し、割合を掛けて、端数は切り捨てる
            int add = Mathf.FloorToInt((dirtCount * blocksPerDirt + stoneCount * blocksPerStone) * rate);
            if (add <= 0) return;

            blocks.Value += add;
            isDirty = true;
        }

        // ---------------------------------------------------------------
        // 強化
        // ---------------------------------------------------------------

        /// <summary>今のレベル</summary>
        public int GetLevel(int upgradeIndex) => saveData.GetLevel(upgrades[upgradeIndex].id);

        /// <summary>最大レベルまで強化したか</summary>
        public bool IsMaxLevel(int upgradeIndex) => GetLevel(upgradeIndex) >= upgrades[upgradeIndex].maxLevel;

        /// <summary>次のレベルに上げるのに必要なブロックの数</summary>
        public int GetCost(int upgradeIndex) => upgrades[upgradeIndex].GetCost(GetLevel(upgradeIndex));

        /// <summary>強化できるか（最大レベルではなく、素材が足りている）</summary>
        public bool CanUpgrade(int upgradeIndex)
        {
            if (!IsValidTool(upgrades[upgradeIndex])) return false;
            if (IsMaxLevel(upgradeIndex)) return false;
            return blocks.Value >= GetCost(upgradeIndex);
        }

        /// <summary>
        /// 強化する（素材を使ってレベルを1上げる）
        /// </summary>
        /// <returns>強化できたら true</returns>
        public bool TryUpgrade(int upgradeIndex)
        {
            if (upgradeIndex < 0 || upgradeIndex >= upgrades.Length) return false;
            if (!CanUpgrade(upgradeIndex)) return false;

            // 素材を払う（レベルを上げる前の費用で計算する）
            blocks.Value -= GetCost(upgradeIndex);

            // レベルを上げる
            ToolUpgradeDefinition definition = upgrades[upgradeIndex];
            saveData.SetLevel(definition.id, GetLevel(upgradeIndex) + 1);

            // 道具に反映する（カーソルの大きさなども、次のフレームから変わる）
            ApplyTool(definition.toolIndex);

            // 強化は大事な変化なので、すぐにファイルへ保存する
            isDirty = true;
            SaveIfDirty(true);

            upgraded.OnNext(upgradeIndex);
            return true;
        }

        /// <summary>
        /// 強化の値を「今 → 次」の文字にする（UI 用。例：「範囲 1.6 → 1.9」）
        /// </summary>
        public string GetValueText(int upgradeIndex)
        {
            ToolUpgradeDefinition definition = upgrades[upgradeIndex];
            if (!IsValidTool(definition)) return "道具が見つかりません";

            int level = GetLevel(upgradeIndex);
            string current = FormatValue(definition, level);
            if (IsMaxLevel(upgradeIndex)) return current;
            return $"{current} → {FormatValue(definition, level + 1)}";
        }

        private string FormatValue(ToolUpgradeDefinition definition, int level)
        {
            DigTool baseTool = controller.GetBaseTool(definition.toolIndex);
            switch (definition.stat)
            {
                case UpgradeStat.Radius:
                    return $"範囲 {baseTool.radius + definition.amountPerLevel * level:0.##}";

                case UpgradeStat.Speed:
                    // 間隔が短くなるほど速い → 「何倍速いか」で表示する（間隔 85% なら 約1.18倍）
                    float speed = 1f / Mathf.Pow(definition.amountPerLevel, level);
                    return $"速さ ×{speed:0.00}";

                case UpgradeStat.Power:
                    return $"威力 {baseTool.power + Mathf.RoundToInt(definition.amountPerLevel * level)}";

                case UpgradeStat.Depth:
                    return $"深さ {baseTool.depth + Mathf.RoundToInt(definition.amountPerLevel * level)}マス";

                default:
                    return "";
            }
        }

        // ---------------------------------------------------------------
        // 道具への反映
        // ---------------------------------------------------------------

        /// <summary>全部の道具に、今のレベルの強化を反映する</summary>
        public void ApplyAll()
        {
            if (controller == null) return;
            for (int i = 0; i < controller.Tools.Length; i++)
            {
                ApplyTool(i);
            }
        }

        /// <summary>
        /// 1つの道具に強化を反映する
        /// Inspector の値（強化前）をコピーして、その道具に関係する強化を全部足し直す
        /// </summary>
        private void ApplyTool(int toolIndex)
        {
            if (toolIndex < 0 || toolIndex >= controller.Tools.Length) return;

            // 強化前の値から作り直すので、何度呼んでも強化が重なりすぎることはない
            DigTool tool = controller.GetBaseTool(toolIndex).Clone();

            for (int i = 0; i < upgrades.Length; i++)
            {
                ToolUpgradeDefinition definition = upgrades[i];
                if (definition.toolIndex != toolIndex) continue;

                int level = GetLevel(i);
                if (level <= 0) continue;

                switch (definition.stat)
                {
                    case UpgradeStat.Radius:
                        tool.radius += definition.amountPerLevel * level;
                        break;

                    case UpgradeStat.Speed:
                        // 道具ごとの間隔が 0 のときは、DigController の共通の間隔を元にする
                        float baseInterval = tool.holdInterval > 0f ? tool.holdInterval : controller.DefaultHoldInterval;
                        // 間隔 × 倍率のレベル乗（0.85 なら 0.85 → 0.72 → 0.61 …と短くなる）
                        // 短くしすぎると毎フレーム掘ることになるので、0.02 秒より短くはしない
                        tool.holdInterval = Mathf.Max(0.02f, baseInterval * Mathf.Pow(definition.amountPerLevel, level));
                        break;

                    case UpgradeStat.Power:
                        tool.power += Mathf.RoundToInt(definition.amountPerLevel * level);
                        break;

                    case UpgradeStat.Depth:
                        tool.depth += Mathf.RoundToInt(definition.amountPerLevel * level);
                        break;
                }
            }

            controller.SetRuntimeTool(toolIndex, tool);
        }

        /// <summary>強化の項目が、ちゃんとある道具を指しているか</summary>
        private bool IsValidTool(ToolUpgradeDefinition definition)
        {
            return controller != null && definition.toolIndex >= 0 && definition.toolIndex < controller.Tools.Length;
        }

        // ---------------------------------------------------------------
        // 保存
        // ---------------------------------------------------------------

        private void SaveIfDirty(bool writeToDisk)
        {
            if (!isDirty || saveData == null) return;

            saveData.blocks = blocks.Value;
            saveData.Save(writeToDisk);
            isDirty = false;
        }

        // ---------------------------------------------------------------
        // デバッグ（Inspector の右クリックメニュー）
        // ---------------------------------------------------------------

        [ContextMenu("Debug/ブロックを +1000")]
        private void DebugAddMaterials()
        {
            if (!Application.isPlaying) return;
            blocks.Value += 1000;
            isDirty = true;
        }

        [ContextMenu("Debug/強化と素材をリセット")]
        private void DebugReset()
        {
            UpgradeSaveData.Delete();
            if (!Application.isPlaying) return;

            saveData = new UpgradeSaveData();
            blocks.Value = 0;
            isDirty = false;
            ApplyAll();
            upgraded.OnNext(-1); // UI を全部更新してもらう
        }
    }
}
