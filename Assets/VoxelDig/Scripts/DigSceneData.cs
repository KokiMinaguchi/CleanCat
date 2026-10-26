using System.Collections.Generic;
using UnityEngine;

namespace VoxelDig
{
    /// <summary>
    /// シーンをまたいでデータを受け渡すための static クラス
    ///
    /// static な変数はシーンを切り替えても消えないので、
    ///   探索シーン →（どの CSV を掘るか）→ 発掘シーン
    ///   発掘シーン →（クリアしたか）→ 探索シーン
    /// の受け渡しに使える
    ///
    /// ※ ゲームを終了すると消えるので、発掘済みの記録を残したい場合はセーブデータに書き出す
    /// </summary>
    public static class DigSceneData
    {
        // ---------------------------------------------------------------
        // 行き：探索シーン → 発掘シーン
        // ---------------------------------------------------------------

        /// <summary>掘るボクセルモデルの CSV</summary>
        public static TextAsset VoxelCsv { get; private set; }

        /// <summary>どの発掘ポイントから来たか（クリアしたときの記録に使う）</summary>
        public static string SiteId { get; private set; }

        /// <summary>UI に表示する名前</summary>
        public static string SiteName { get; private set; }

        /// <summary>発掘が終わったら戻るシーンの名前</summary>
        public static string ReturnSceneName { get; private set; }

        /// <summary>探索シーンから発掘の依頼が来ているか（発掘シーンを単体で再生したときは false）</summary>
        public static bool HasRequest => VoxelCsv != null;

        /// <summary>
        /// ゲームを始めてから、一度でも発掘シーンへ行ったか
        /// true なら「探索シーンには発掘シーンから戻ってきた」ということなので、タイトルなどを出さないのに使う
        /// </summary>
        public static bool HasEnteredDigScene { get; private set; }

        // ---------------------------------------------------------------
        // 帰り：発掘済みの発掘ポイント
        // ---------------------------------------------------------------

        // HashSet は「同じものを2回入れない」リスト。含まれているかの確認が速い
        private static readonly HashSet<string> CompletedSiteIds = new HashSet<string>();

        // ついさっきクリアした発掘ポイント（探索シーンに戻ったとき、出現演出を1回だけ再生するため）
        private static string justCompletedSiteId;

        /// <summary>発掘シーンへ渡す情報を設定する</summary>
        public static void SetRequest(TextAsset voxelCsv, string siteId, string siteName, string returnSceneName)
        {
            VoxelCsv = voxelCsv;
            SiteId = siteId;
            SiteName = siteName;
            ReturnSceneName = returnSceneName;
            HasEnteredDigScene = true;
        }

        /// <summary>依頼を消す（発掘シーンから戻るときに呼ぶ）</summary>
        public static void ClearRequest()
        {
            VoxelCsv = null;
            SiteId = null;
            SiteName = null;
            ReturnSceneName = null;
        }

        public static void MarkCompleted(string siteId)
        {
            if (string.IsNullOrEmpty(siteId)) return;
            CompletedSiteIds.Add(siteId);
            justCompletedSiteId = siteId;
        }

        /// <summary>
        /// その発掘ポイントを「ついさっきクリアした」か調べる
        /// 1回調べると記録は消える（シーンに戻るたびに演出が再生されないように）
        /// </summary>
        public static bool ConsumeJustCompleted(string siteId)
        {
            if (string.IsNullOrEmpty(siteId) || justCompletedSiteId != siteId) return false;
            justCompletedSiteId = null;
            return true;
        }

        public static bool IsCompleted(string siteId)
        {
            return !string.IsNullOrEmpty(siteId) && CompletedSiteIds.Contains(siteId);
        }

        /// <summary>
        /// Play 開始時に中身をリセットする
        /// Project Settings の「Enter Play Mode Options」でドメインリロードを切っていると、
        /// static 変数が前回の Play の値のまま残ってしまうため
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            ClearRequest();
            CompletedSiteIds.Clear();
            justCompletedSiteId = null;
            HasEnteredDigScene = false;
        }
    }
}