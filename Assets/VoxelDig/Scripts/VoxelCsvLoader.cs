using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace VoxelDig
{
    /// <summary>
    /// CSV から読み込んだボクセル1個分のデータ
    /// </summary>
    public struct VoxelPoint
    {
        public Vector3Int position; // グリッド上の座標
        public Color32 color;       // 色（RRGGBBAA）
    }

    /// <summary>
    /// CSV から読み込んだボクセルモデル全体
    /// </summary>
    public class VoxelModel
    {
        public Vector3Int size;                       // モデルの大きさ（size 行の値）
        public readonly List<VoxelPoint> points = new List<VoxelPoint>();
    }

    /// <summary>
    /// 「VoxelCSV v1」形式のテキストを読み込むクラス
    ///
    /// 形式：
    ///   # VoxelCSV v1             ← # で始まる行はコメントとして無視
    ///   size,40,39,40             ← モデルのサイズ（X,Y,Z）
    ///   19,20,12,F2E8D8FF         ← x,y,z,色(RRGGBBAA) を1行に1ボクセル
    /// </summary>
    public static class VoxelCsvLoader
    {
        public static VoxelModel Load(string csvText)
        {
            var model = new VoxelModel();
            // 改行コード（\r\n / \n）のどちらにも対応する
            string[] lines = csvText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            Vector3Int max = Vector3Int.zero; // size 行が無かったときのために最大座標も記録しておく

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                // 空行とコメント行はスキップ
                if (line.Length == 0 || line[0] == '#') continue;

                string[] cols = line.Split(',');

                // サイズ指定の行
                if (cols[0] == "size")
                {
                    model.size = new Vector3Int(int.Parse(cols[1]), int.Parse(cols[2]), int.Parse(cols[3]));
                    continue;
                }

                // ボクセルの行（列が足りない行は壊れているので無視）
                if (cols.Length < 4) continue;

                var p = new VoxelPoint
                {
                    position = new Vector3Int(int.Parse(cols[0]), int.Parse(cols[1]), int.Parse(cols[2])),
                    color = ParseColor(cols[3])
                };

                // アルファが0のボクセルは「無い」扱いにする
                if (p.color.a == 0) continue;

                model.points.Add(p);
                max = Vector3Int.Max(max, p.position + Vector3Int.one);
            }

            // size 行が無い、または実データの方が大きい場合は実データに合わせる
            model.size = Vector3Int.Max(model.size, max);
            return model;
        }

        /// <summary>
        /// CSV の「size,X,Y,Z」行だけを読んで、モデルのサイズを返す（全行を読まないので速い）
        /// UI にサイズを表示するときなどに使う
        /// </summary>
        public static bool TryReadSize(string csvText, out Vector3Int size)
        {
            size = Vector3Int.zero;
            if (string.IsNullOrEmpty(csvText)) return false;

            // size 行はファイルの先頭付近にあるので、最初の数行だけ調べる
            int lineStart = 0;
            for (int lineCount = 0; lineCount < 20 && lineStart < csvText.Length; lineCount++)
            {
                int lineEnd = csvText.IndexOf('\n', lineStart);
                if (lineEnd < 0) lineEnd = csvText.Length;

                string line = csvText.Substring(lineStart, lineEnd - lineStart).Trim();
                if (line.StartsWith("size,"))
                {
                    string[] cols = line.Split(',');
                    if (cols.Length >= 4 &&
                        int.TryParse(cols[1], out int x) &&
                        int.TryParse(cols[2], out int y) &&
                        int.TryParse(cols[3], out int z))
                    {
                        size = new Vector3Int(x, y, z);
                        return true;
                    }
                    return false;
                }
                lineStart = lineEnd + 1;
            }
            return false;
        }

        /// <summary>
        /// "RRGGBBAA" または "RRGGBB" の16進数文字列を Color32 に変換する
        /// </summary>
        private static Color32 ParseColor(string hex)
        {
            hex = hex.Trim();
            byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            byte a = hex.Length >= 8 ? byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber) : (byte)255;
            return new Color32(r, g, b, a);
        }
    }
}
