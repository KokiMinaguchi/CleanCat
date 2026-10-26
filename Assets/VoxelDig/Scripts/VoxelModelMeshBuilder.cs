using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelDig
{
    /// <summary>
    /// CSV のボクセルモデル（土なし）を1つのメッシュにするクラス
    /// 掘り出したアイテムを飾るときなどに使う
    ///
    /// メッシュは「1ボクセル = 1」の大きさで作る。表示する大きさは Transform の scale で調整する
    /// 原点は「モデルの底の中心」にしてあるので、床の上に置きやすい
    /// </summary>
    public static class VoxelModelMeshBuilder
    {
        public static Mesh Build(VoxelModel model)
        {
            // 「その座標にボクセルがあるか」をすぐ調べられるように HashSet に入れる
            var occupied = new HashSet<Vector3Int>();
            Vector3Int min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            Vector3Int max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            foreach (var p in model.points)
            {
                occupied.Add(p.position);
                min = Vector3Int.Min(min, p.position);
                max = Vector3Int.Max(max, p.position);
            }

            // 原点を「底の中心」にするためのずらし量（X・Z は中央、Y は一番下）
            var pivot = new Vector3(
                (min.x + max.x + 1) * 0.5f,
                min.y,
                (min.z + max.z + 1) * 0.5f);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color32>();
            var triangles = new List<int>();

            foreach (var p in model.points)
            {
                Vector3 basePos = (Vector3)p.position - pivot;

                for (int f = 0; f < 6; f++)
                {
                    Vector3Int d = VoxelGrid.Directions[f];

                    // 隣にもボクセルがある面は見えないので作らない
                    if (occupied.Contains(p.position + d)) continue;

                    int start = vertices.Count;
                    Vector3[] corners = VoxelChunk.FaceCorners[f];
                    for (int c = 0; c < 4; c++)
                    {
                        vertices.Add(basePos + corners[c]);
                        normals.Add(new Vector3(d.x, d.y, d.z));
                        colors.Add(p.color);
                    }

                    // 四角形を三角形2枚で作る
                    triangles.Add(start);
                    triangles.Add(start + 1);
                    triangles.Add(start + 2);
                    triangles.Add(start);
                    triangles.Add(start + 2);
                    triangles.Add(start + 3);
                }
            }

            var mesh = new Mesh { name = "VoxelModel" };
            // 頂点数が 65535 を超えても大丈夫なように 32bit インデックスにする
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>モデルの一番長い辺のボクセル数（大きさを合わせるときに使う）</summary>
        public static int GetMaxDimension(VoxelModel model)
        {
            if (model.points.Count == 0) return 1;

            Vector3Int min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            Vector3Int max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            foreach (var p in model.points)
            {
                min = Vector3Int.Min(min, p.position);
                max = Vector3Int.Max(max, p.position);
            }
            Vector3Int size = max - min + Vector3Int.one;
            return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        }
    }
}
