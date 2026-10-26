using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelDig
{
    /// <summary>
    /// ブロックを 16×16×16 などの「チャンク」に分けて、1チャンクを1つのメッシュとして描画する
    /// 1ボクセル = 1 GameObject にすると数万個になって重いので、まとめてメッシュ化している
    /// 掘ったときは、そのチャンクのメッシュだけを作り直す
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class VoxelChunk : MonoBehaviour
    {
        // 各面の4頂点（キューブの角の位置）。並び順は VoxelGrid.Directions と同じ（+X, -X, +Y, -Y, +Z, -Z）
        // 表から見て時計回りになる順番にしてある（Unity は時計回りが表面）
        internal static readonly Vector3[][] FaceCorners =
        {
            new[] { new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1) }, // +X
            new[] { new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0) }, // -X
            new[] { new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) }, // +Y
            new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1) }, // -Y
            new[] { new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1) }, // +Z
            new[] { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0) }, // -Z
        };

        // メッシュ作成用の作業リスト（全チャンクで使い回して GC を減らす）
        private static readonly List<Vector3> Vertices = new List<Vector3>();
        private static readonly List<Vector3> Normals = new List<Vector3>();
        private static readonly List<Color32> Colors = new List<Color32>();
        private static readonly List<int> Triangles = new List<int>();

        private VoxelGrid grid;
        private Vector3Int origin;   // このチャンクが担当する範囲の開始位置（グリッド座標）
        private int chunkSize;
        private float voxelSize;

        private Mesh mesh;
        private MeshFilter meshFilter;
        private MeshCollider meshCollider;

        /// <summary>作り直しが必要かどうか（DigBlock がまとめて処理する）</summary>
        public bool IsDirty { get; set; }

        public void Initialize(VoxelGrid grid, Vector3Int origin, int chunkSize, float voxelSize, Material material)
        {
            this.grid = grid;
            this.origin = origin;
            this.chunkSize = chunkSize;
            this.voxelSize = voxelSize;

            meshFilter = GetComponent<MeshFilter>();
            meshCollider = GetComponent<MeshCollider>();
            GetComponent<MeshRenderer>().sharedMaterial = material;

            if (mesh == null)
            {
                mesh = new Mesh { name = $"VoxelChunk {origin}" };
                // 頂点数が 65535 を超えても大丈夫なように 32bit インデックスにする
                mesh.indexFormat = IndexFormat.UInt32;
                // 何度も書き換えるメッシュであることを Unity に伝える
                mesh.MarkDynamic();
            }

            IsDirty = true;
        }

        /// <summary>
        /// 担当範囲のボクセルからメッシュを作り直す
        /// 「隣が空気の面」だけを作ることで、見えない面を省いて軽くしている
        /// </summary>
        public void Rebuild()
        {
            IsDirty = false;

            Vertices.Clear();
            Normals.Clear();
            Colors.Clear();
            Triangles.Clear();

            Vector3Int end = Vector3Int.Min(origin + Vector3Int.one * chunkSize, grid.Size);

            for (int z = origin.z; z < end.z; z++)
            for (int y = origin.y; y < end.y; y++)
            for (int x = origin.x; x < end.x; x++)
            {
                int i = grid.Index(x, y, z);
                VoxelType type = grid.types[i];

                // 空気と空洞は描画しない
                if (type == VoxelType.Empty || type == VoxelType.Cavity) continue;

                Color32 color = grid.colors[i];
                var basePos = new Vector3(x, y, z);

                for (int f = 0; f < 6; f++)
                {
                    Vector3Int d = VoxelGrid.Directions[f];

                    // 隣が埋まっているなら、この面は見えないので作らない
                    if (grid.IsOccluder(x + d.x, y + d.y, z + d.z)) continue;

                    AddFace(basePos, f, d, color);
                }
            }

            mesh.Clear();
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateBounds();

            meshFilter.sharedMesh = mesh;

            // MeshCollider は一度 null にしてから入れ直さないと形が更新されない
            meshCollider.sharedMesh = null;
            if (Vertices.Count > 0) meshCollider.sharedMesh = mesh;
        }

        /// <summary>四角形の面（三角形2枚）を1つ追加する</summary>
        private void AddFace(Vector3 basePos, int faceIndex, Vector3Int dir, Color32 color)
        {
            int start = Vertices.Count;
            Vector3[] corners = FaceCorners[faceIndex];
            var normal = new Vector3(dir.x, dir.y, dir.z);

            for (int c = 0; c < 4; c++)
            {
                Vertices.Add((basePos + corners[c]) * voxelSize);
                Normals.Add(normal);
                Colors.Add(color);
            }

            // 0-1-2 と 0-2-3 の三角形2枚で四角形を作る
            Triangles.Add(start);
            Triangles.Add(start + 1);
            Triangles.Add(start + 2);
            Triangles.Add(start);
            Triangles.Add(start + 2);
            Triangles.Add(start + 3);
        }

        private void OnDestroy()
        {
            // 自分で new した Mesh は自分で破棄する（メモリリーク防止）
            if (mesh != null) Destroy(mesh);
        }
    }
}
