using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelDig
{
    /// <summary>
    /// ボクセル1マスの種類
    /// </summary>
    public enum VoxelType : byte
    {
        Empty = 0,    // 何もない（空気）
        Dirt = 1,     // 土（柔らかい）
        Stone = 2,    // 石（硬い）
        Treasure = 3, // 埋まっているお宝（CSV のモデル）
        Cavity = 4,   // お宝の内部にある閉じた空洞（見えないので描画しない）
    }

    /// <summary>
    /// ブロック生成時の設定（Inspector から調整できるように Serializable にしている）
    /// </summary>
    [Serializable]
    public class BlockGenerationSettings
    {
        [Tooltip("お宝のまわりに付ける土の最大の厚さ（マス）")]
        public int padding = 5;

        [Tooltip("お宝のまわりに必ず付ける土の最小の厚さ（マス）")]
        public float minCover = 2.5f;

        [Tooltip("外形のデコボコの細かさ（小さいほど大きなうねり）")]
        public float shapeNoiseScale = 0.12f;

        [Tooltip("石が混ざる模様の細かさ")]
        public float stoneNoiseScale = 0.18f;

        [Tooltip("この値より大きいノイズの場所が石になる（0〜1、大きいほど石が少ない）")]
        [Range(0f, 1f)] public float stoneThreshold = 0.62f;

        [Tooltip("土を壊すのに必要な回数")]
        public int dirtHp = 1;

        [Tooltip("石を壊すのに必要な回数")]
        public int stoneHp = 3;

        [Header("色")]
        public Color dirtColorA = new Color(0.55f, 0.40f, 0.27f);
        public Color dirtColorB = new Color(0.42f, 0.30f, 0.20f);
        public Color stoneColor = new Color(0.52f, 0.52f, 0.55f);

        [Tooltip("1マスごとの色のバラつき（0で均一）")]
        [Range(0f, 0.3f)] public float colorJitter = 0.06f;
    }

    /// <summary>
    /// ブロック全体のボクセルデータを持つクラス（MonoBehaviour ではない普通の C# クラス）
    /// 3次元の情報を1次元配列で持ち、Index(x, y, z) で位置を計算する
    /// </summary>
    public class VoxelGrid
    {
        public Vector3Int Size { get; }

        public readonly VoxelType[] types;   // マスの種類
        public readonly Color32[] colors;    // 今の表示色（ダメージで暗くなる）
        public readonly Color32[] baseColors;// 元の色（暗くする計算の基準）
        public readonly byte[] hp;           // 残り耐久値（土・石用）
        public readonly byte[] treasureHits; // お宝が叩かれた回数（見た目用）
        public readonly bool[] contact;      // お宝に接している土・石かどうか

        public int TreasureCount { get; private set; }     // お宝ボクセルの数
        public int ContactTotal { get; private set; }      // お宝に接している土の総数
        public int ContactRemaining { get; set; }          // まだ残っている「接している土」の数

        // 6方向の隣（±X, ±Y, ±Z）
        public static readonly Vector3Int[] Directions =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        private VoxelGrid(Vector3Int size)
        {
            Size = size;
            int count = size.x * size.y * size.z;
            types = new VoxelType[count];
            colors = new Color32[count];
            baseColors = new Color32[count];
            hp = new byte[count];
            treasureHits = new byte[count];
            contact = new bool[count];
        }

        // ---------------------------------------------------------------
        // 座標まわりの便利関数
        // ---------------------------------------------------------------

        /// <summary>3次元座標 → 1次元配列の番号</summary>
        public int Index(int x, int y, int z) => x + Size.x * (y + Size.y * z);

        public bool InBounds(int x, int y, int z) =>
            x >= 0 && y >= 0 && z >= 0 && x < Size.x && y < Size.y && z < Size.z;

        public bool InBounds(Vector3Int p) => InBounds(p.x, p.y, p.z);

        /// <summary>範囲外は「空気」として返す</summary>
        public VoxelType GetVoxel(int x, int y, int z) =>
            InBounds(x, y, z) ? types[Index(x, y, z)] : VoxelType.Empty;

        /// <summary>
        /// 面を描画するときに「隣が埋まっているか」の判定に使う
        /// 空洞（Cavity）も埋まっている扱いにして、見えない内側の面を描かないようにする
        /// </summary>
        public bool IsOccluder(int x, int y, int z) => GetVoxel(x, y, z) != VoxelType.Empty;

        /// <summary>そのお宝ボクセルが空気に触れている（露出している）か</summary>
        public bool IsExposed(int x, int y, int z)
        {
            foreach (var d in Directions)
            {
                if (GetVoxel(x + d.x, y + d.y, z + d.z) == VoxelType.Empty) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------
        // 生成
        // ---------------------------------------------------------------

        /// <summary>
        /// CSV のモデルを土の塊に埋めたグリッドを作る
        /// </summary>
        public static VoxelGrid Create(VoxelModel model, BlockGenerationSettings s, int seed)
        {
            // まわりに土を付ける分 + 一番外側は必ず空気にする分（+1）だけ大きくする
            int margin = s.padding + 1;
            var grid = new VoxelGrid(model.size + Vector3Int.one * margin * 2);
            var offset = Vector3Int.one * margin; // モデルを置く位置
            var random = new System.Random(seed);

            // シードごとに違う模様になるよう、ノイズの読み取り位置をずらす
            float noiseOffsetA = random.Next(0, 10000) * 0.37f;
            float noiseOffsetB = random.Next(0, 10000) * 0.37f;

            // 1) お宝（CSV のボクセル）を置く
            foreach (var p in model.points)
            {
                Vector3Int g = p.position + offset;
                if (!grid.InBounds(g)) continue;

                int i = grid.Index(g.x, g.y, g.z);
                grid.types[i] = VoxelType.Treasure;
                grid.colors[i] = p.color;
                grid.baseColors[i] = p.color;
                grid.TreasureCount++;
            }

            // 2) 外側から塗りつぶし（フラッドフィル）をして、外とつながっている場所を調べる
            //    つながっていない場所 = お宝の中に閉じ込められた空洞
            bool[] outside = FloodFillFromBorder(grid);

            // モデルの箱（AABB）の最小・最大（マスの中心との距離計算に使う）
            Vector3 boxMin = offset;
            Vector3 boxMax = offset + model.size;

            // 3) 土と石を詰める
            for (int z = 0; z < grid.Size.z; z++)
            for (int y = 0; y < grid.Size.y; y++)
            for (int x = 0; x < grid.Size.x; x++)
            {
                int i = grid.Index(x, y, z);
                if (grid.types[i] == VoxelType.Treasure) continue;

                // 外とつながっていない＝お宝内部の空洞
                if (!outside[i])
                {
                    grid.types[i] = VoxelType.Cavity;
                    continue;
                }

                // マスの中心からモデルの箱までの距離（箱の中なら 0）
                var center = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                Vector3 outDist = Vector3.Max(Vector3.Max(boxMin - center, center - boxMax), Vector3.zero);
                float dist = outDist.magnitude;

                // ノイズで「この場所まで土を付ける」厚さを変え、岩っぽいデコボコの外形にする
                float shapeNoise = Noise01(x, y, z, s.shapeNoiseScale, noiseOffsetA);
                float coverLimit = Mathf.Lerp(s.minCover, s.padding, shapeNoise);
                if (dist > coverLimit) continue; // 外形より外は空気のまま

                // 石にするかどうか
                float stoneNoise = Noise01(x, y, z, s.stoneNoiseScale, noiseOffsetB);
                bool isStone = stoneNoise > s.stoneThreshold;

                Color c;
                if (isStone)
                {
                    grid.types[i] = VoxelType.Stone;
                    grid.hp[i] = (byte)Mathf.Clamp(s.stoneHp, 1, 255);
                    c = s.stoneColor;
                }
                else
                {
                    grid.types[i] = VoxelType.Dirt;
                    grid.hp[i] = (byte)Mathf.Clamp(s.dirtHp, 1, 255);
                    // 高さ方向に少し色を変えて地層っぽくする
                    float layer = Mathf.PerlinNoise(y * 0.25f + noiseOffsetA, 0.5f);
                    c = Color.Lerp(s.dirtColorA, s.dirtColorB, layer);
                }

                // 1マスごとに少しだけ明るさをバラつかせる
                float jitter = 1f + ((float)random.NextDouble() * 2f - 1f) * s.colorJitter;
                c = new Color(c.r * jitter, c.g * jitter, c.b * jitter, 1f);

                grid.colors[i] = c;
                grid.baseColors[i] = c;
            }

            // 4) お宝に接している土・石に印を付ける（これを全部掘ったらクリア）
            for (int z = 0; z < grid.Size.z; z++)
            for (int y = 0; y < grid.Size.y; y++)
            for (int x = 0; x < grid.Size.x; x++)
            {
                int i = grid.Index(x, y, z);
                if (grid.types[i] != VoxelType.Dirt && grid.types[i] != VoxelType.Stone) continue;

                foreach (var d in Directions)
                {
                    if (grid.GetVoxel(x + d.x, y + d.y, z + d.z) == VoxelType.Treasure)
                    {
                        grid.contact[i] = true;
                        grid.ContactTotal++;
                        break;
                    }
                }
            }
            grid.ContactRemaining = grid.ContactTotal;

            return grid;
        }

        /// <summary>
        /// グリッドの一番外側から、お宝以外のマスを伝って届く場所を調べる
        /// </summary>
        private static bool[] FloodFillFromBorder(VoxelGrid grid)
        {
            var size = grid.Size;
            var visited = new bool[grid.types.Length];
            var queue = new Queue<Vector3Int>();

            // 外周のマスをすべてスタート地点にする
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                bool isBorder = x == 0 || y == 0 || z == 0 || x == size.x - 1 || y == size.y - 1 || z == size.z - 1;
                if (!isBorder) continue;

                int i = grid.Index(x, y, z);
                if (grid.types[i] == VoxelType.Treasure) continue;
                visited[i] = true;
                queue.Enqueue(new Vector3Int(x, y, z));
            }

            // 幅優先探索で広げていく
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in Directions)
                {
                    var n = p + d;
                    if (!grid.InBounds(n)) continue;

                    int ni = grid.Index(n.x, n.y, n.z);
                    if (visited[ni] || grid.types[ni] == VoxelType.Treasure) continue;

                    visited[ni] = true;
                    queue.Enqueue(n);
                }
            }
            return visited;
        }

        /// <summary>
        /// 簡易的な3Dノイズ（2Dパーリンノイズを3面分平均して 0〜1 に引き伸ばす）
        /// </summary>
        private static float Noise01(int x, int y, int z, float scale, float offset)
        {
            float a = Mathf.PerlinNoise(x * scale + offset, y * scale + offset);
            float b = Mathf.PerlinNoise(y * scale + offset, z * scale + offset);
            float c = Mathf.PerlinNoise(z * scale + offset, x * scale + offset);
            float n = (a + b + c) / 3f;
            // 平均を取ると 0.5 付近に集まるので、0〜1 に広げる
            return Mathf.Clamp01((n - 0.3f) / 0.4f);
        }
    }
}
