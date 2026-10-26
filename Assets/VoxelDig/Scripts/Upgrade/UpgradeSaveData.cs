using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelDig
{
    /// <summary>
    /// 強化の素材（壊したブロックの数）と、各強化のレベルを保存する
    ///
    /// PlayerPrefs に JSON の文字列として保存するので、ゲームを終了しても残る。
    /// ・シーンを移動しても、ToolUpgradeSystem が Awake で読み込み直すので引き継がれる
    /// ・ジオラマ（探索）シーンで素材の数を表示したいときも、UpgradeSaveData.Load() で読める
    /// </summary>
    [Serializable]
    public class UpgradeSaveData
    {
        // PlayerPrefs に保存するときの名前
        private const string PrefsKey = "VoxelDig.Upgrade";

        // ---- 保存する中身（JsonUtility で保存できるよう public フィールドにする）----

        /// <summary>持っているブロックの数（強化の素材）</summary>
        public int blocks;

        /// <summary>各強化のレベル（id とレベルの組）</summary>
        public List<LevelEntry> levels = new List<LevelEntry>();

        /// <summary>
        /// 強化1項目のレベル
        /// （JsonUtility は Dictionary を保存できないので、リストで持つ）
        /// </summary>
        [Serializable]
        public class LevelEntry
        {
            public string id;
            public int level;
        }

        // ---------------------------------------------------------------
        // レベルの読み書き
        // ---------------------------------------------------------------

        /// <summary>強化のレベルを取り出す（まだ強化していなければ 0）</summary>
        public int GetLevel(string id)
        {
            foreach (LevelEntry entry in levels)
            {
                if (entry.id == id) return entry.level;
            }
            return 0;
        }

        /// <summary>強化のレベルを書き込む</summary>
        public void SetLevel(string id, int level)
        {
            foreach (LevelEntry entry in levels)
            {
                if (entry.id == id)
                {
                    entry.level = level;
                    return;
                }
            }
            levels.Add(new LevelEntry { id = id, level = level });
        }

        // ---------------------------------------------------------------
        // 保存・読み込み
        // ---------------------------------------------------------------

        /// <summary>
        /// 保存してあるデータを読み込む（無ければ空のデータ）
        /// </summary>
        public static UpgradeSaveData Load()
        {
            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(json)) return new UpgradeSaveData();

            try
            {
                return JsonUtility.FromJson<UpgradeSaveData>(json) ?? new UpgradeSaveData();
            }
            catch (Exception e)
            {
                // 壊れたデータが入っていたときは、最初からやり直す
                Debug.LogWarning($"[UpgradeSaveData] 保存データを読み込めなかったので、最初からにします：{e.Message}");
                return new UpgradeSaveData();
            }
        }

        /// <summary>
        /// データを保存する
        /// </summary>
        /// <param name="writeToDisk">true なら、すぐにファイルへ書き出す（強化したときなど、大事なときだけ true にする）</param>
        public void Save(bool writeToDisk)
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));

            // PlayerPrefs.Save はファイルへの書き込みで少し重いので、毎回は呼ばない
            // （呼ばなくても、ゲームを普通に終了したときには Unity が自動で書き出す）
            if (writeToDisk) PlayerPrefs.Save();
        }

        /// <summary>
        /// 保存データを消す（デバッグ用）
        /// </summary>
        public static void Delete()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }
    }
}
