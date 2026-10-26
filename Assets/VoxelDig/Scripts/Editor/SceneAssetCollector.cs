using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelDig.EditorTools
{
    /// <summary>
    /// 開いているシーンで使われているモデル・テクスチャ・マテリアル・プレハブを集めるツール
    ///
    /// メニュー「Tools > VoxelDig > シーンのアセットを収集」で開く
    ///
    /// できること：
    ///   1. スキャン：開いているシーン（複数開いていれば全部）から使われているアセットを集める
    ///   2. 重複削除：同じファイルは1つにまとめる。別の場所にある「中身が全く同じファイル」も重複として扱う
    ///   3. 一覧：ウィンドウに表示＋CSV で保存（Excel で開ける）
    ///   4. 収集：
    ///        ・プロジェクト内のフォルダにコピー
    ///        ・.unitypackage で書き出し（別プロジェクトへそのまま持っていける。おすすめ）
    ///        ・外部フォルダにコピー（.meta 付きなので、別プロジェクトの Assets に入れれば参照も保たれる）
    ///
    /// ※ Editor フォルダに入っているので、ビルドには含まれない
    /// </summary>
    public class SceneAssetCollector : EditorWindow
    {
        // ---------------------------------------------------------------
        // 集めたアセット1つ分の情報
        // ---------------------------------------------------------------
        private class AssetEntry
        {
            public string Category;          // 種類（Models / Textures / Materials / Prefabs）
            public string Path;              // アセットのパス（Assets/...）
            public Object Asset;             // アセット本体（一覧からクリックして場所を確認する用）
            public long Size;                // ファイルサイズ（バイト）
            public string Hash;              // ファイルの中身から計算した値（同じなら中身も同じ）
            public string DuplicateOf;       // 中身が同じ別ファイルがあれば、そのパス（重複なら収集しない）
            public readonly HashSet<string> Users = new HashSet<string>(); // 使っているオブジェクトの名前

            public bool IsDuplicate => !string.IsNullOrEmpty(DuplicateOf);
        }

        private static readonly string[] Categories = { "Models", "Textures", "Materials", "Prefabs" };

        // 見出しに表示する日本語名
        private static readonly Dictionary<string, string> CategoryLabels = new Dictionary<string, string>
        {
            { "Models", "モデル" },
            { "Textures", "テクスチャ" },
            { "Materials", "マテリアル" },
            { "Prefabs", "プレハブ" },
        };

        // ---- 設定 ----
        [SerializeField] private bool includeInactive = true;        // 非表示のオブジェクトも調べる
        [SerializeField] private bool includeMaterials = true;       // マテリアルも集める
        [SerializeField] private bool includePrefabs = false;        // プレハブも集める
        [SerializeField] private bool detectSameContent = true;      // 中身が同じ別ファイルも重複として扱う
        [SerializeField] private bool includeDependencies = true;    // .unitypackage にシェーダーなど関連アセットも含める
        [SerializeField] private string copyDestination = "Assets/CollectedAssets";

        // ---- 結果 ----
        // パス → 情報（同じパスは自動で1つにまとまる）
        private readonly Dictionary<string, AssetEntry> entries = new Dictionary<string, AssetEntry>();
        private readonly Dictionary<string, bool> foldouts = new Dictionary<string, bool>();
        private Vector2 scroll;
        private string lastScanInfo = "";

        [MenuItem("Tools/VoxelDig/シーンのアセットを収集")]
        private static void Open()
        {
            GetWindow<SceneAssetCollector>("アセット収集");
        }

        // ===================================================================
        // 画面
        // ===================================================================

        private void OnGUI()
        {
            EditorGUILayout.LabelField("設定", EditorStyles.boldLabel);
            includeInactive = EditorGUILayout.Toggle("非表示のオブジェクトも調べる", includeInactive);
            includeMaterials = EditorGUILayout.Toggle("マテリアルも集める", includeMaterials);
            includePrefabs = EditorGUILayout.Toggle("プレハブも集める", includePrefabs);
            detectSameContent = EditorGUILayout.Toggle("中身が同じ別ファイルも重複扱い", detectSameContent);

            EditorGUILayout.Space();
            if (GUILayout.Button("開いているシーンをスキャン", GUILayout.Height(30)))
            {
                Scan();
            }
            if (!string.IsNullOrEmpty(lastScanInfo)) EditorGUILayout.HelpBox(lastScanInfo, MessageType.Info);

            if (entries.Count == 0) return;

            // ---- 一覧 ----
            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string category in Categories)
            {
                DrawCategory(category);
            }
            EditorGUILayout.EndScrollView();

            // ---- 収集 ----
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("収集（重複は除いて1つだけ）", EditorStyles.boldLabel);

            if (GUILayout.Button("一覧を CSV で保存")) SaveCsv();

            EditorGUILayout.BeginHorizontal();
            copyDestination = EditorGUILayout.TextField("コピー先", copyDestination);
            if (GUILayout.Button("プロジェクト内にコピー", GUILayout.Width(160))) CopyIntoProject();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            includeDependencies = EditorGUILayout.ToggleLeft("シェーダーなど関連アセットも含める", includeDependencies);
            if (GUILayout.Button(".unitypackage で書き出し", GUILayout.Width(160))) ExportPackage();
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("外部フォルダにコピー（.meta 付き）")) CopyToExternalFolder();
        }

        /// <summary>種類ごとの一覧を描く</summary>
        private void DrawCategory(string category)
        {
            List<AssetEntry> list = GetEntries(category).ToList();
            if (list.Count == 0) return;

            int uniqueCount = list.Count(e => !e.IsDuplicate);
            int duplicateCount = list.Count - uniqueCount;
            long totalSize = list.Where(e => !e.IsDuplicate).Sum(e => e.Size);

            foldouts.TryGetValue(category, out bool open);
            string header = $"{CategoryLabels[category]}：{uniqueCount} 個（{FormatSize(totalSize)}）" +
                            (duplicateCount > 0 ? $"　重複 {duplicateCount} 個" : "");
            foldouts[category] = EditorGUILayout.Foldout(open, header, true);
            if (!foldouts[category]) return;

            EditorGUI.indentLevel++;
            foreach (AssetEntry entry in list)
            {
                EditorGUILayout.BeginHorizontal();

                // クリックすると Project ウィンドウでその場所が光る
                EditorGUILayout.ObjectField(entry.Asset, typeof(Object), false, GUILayout.Width(220));
                EditorGUILayout.LabelField($"{entry.Users.Count} 箇所", GUILayout.Width(60));
                EditorGUILayout.LabelField(FormatSize(entry.Size), GUILayout.Width(70));

                if (entry.IsDuplicate)
                {
                    // 重複は灰色の文字で、どれと同じかを表示する
                    var style = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.gray } };
                    EditorGUILayout.LabelField($"重複 → {Path.GetFileName(entry.DuplicateOf)}", style);
                }
                else
                {
                    EditorGUILayout.LabelField(entry.Path, EditorStyles.miniLabel);
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }

        // ===================================================================
        // スキャン
        // ===================================================================

        private void Scan()
        {
            entries.Clear();
            int sceneCount = 0;

            try
            {
                // 開いているシーンを全部調べる（シーンを複数まとめて調べたいときは、Additive で開いておく）
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    sceneCount++;

                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        ScanGameObjectTree(root);
                    }
                }

                // 中身が同じ別ファイルを探す
                if (detectSameContent) FindSameContent();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // 結果のまとめを作る
            var summary = new StringBuilder($"{sceneCount} シーンをスキャンしました\n");
            foreach (string category in Categories)
            {
                List<AssetEntry> list = GetEntries(category).ToList();
                if (list.Count == 0) continue;
                summary.Append($"{CategoryLabels[category]}：{list.Count(e => !e.IsDuplicate)} 個");
                int dup = list.Count(e => e.IsDuplicate);
                summary.Append(dup > 0 ? $"（重複 {dup} 個を除外）\n" : "\n");
            }
            lastScanInfo = summary.ToString().TrimEnd();
        }

        /// <summary>オブジェクトとその子をすべて調べる</summary>
        private void ScanGameObjectTree(GameObject root)
        {
            // ---- モデル（メッシュ）----
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(includeInactive))
            {
                AddAsset("Models", filter.sharedMesh, filter.gameObject);
            }
            foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive))
            {
                AddAsset("Models", skinned.sharedMesh, skinned.gameObject);
            }

            // ---- マテリアルと、そこに設定されているテクスチャ ----
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(includeInactive))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null) continue;

                    if (includeMaterials) AddAsset("Materials", material, renderer.gameObject);

                    // シェーダーが持っているテクスチャの枠（_BaseMap, _NormalMap など）を全部調べる
                    foreach (int propertyId in material.GetTexturePropertyNameIDs())
                    {
                        AddAsset("Textures", material.GetTexture(propertyId), renderer.gameObject);
                    }
                }
            }

            // ---- プレハブ ----
            if (includePrefabs)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(includeInactive))
                {
                    // プレハブの一番上のオブジェクトだけを数える（子の分まで数えないように）
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;

                    string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                    AddAssetByPath("Prefabs", prefabPath, t.gameObject);
                }
            }
        }

        /// <summary>
        /// アセットを一覧に追加する
        /// メッシュなら「メッシュが入っているモデルファイル（FBX など）」として追加するので、
        /// 1つの FBX に複数のメッシュが入っていても1つにまとまる
        /// </summary>
        private void AddAsset(string category, Object asset, GameObject user)
        {
            if (asset == null) return;
            AddAssetByPath(category, AssetDatabase.GetAssetPath(asset), user);
        }

        private void AddAssetByPath(string category, string path, GameObject user)
        {
            // Unity に最初から入っているもの（Cube など）や、シーンの中で作られたものは集めない
            if (!IsCollectablePath(path)) return;

            if (!entries.TryGetValue(path, out AssetEntry entry))
            {
                entry = new AssetEntry
                {
                    Category = category,
                    Path = path,
                    Asset = AssetDatabase.LoadMainAssetAtPath(path),
                    Size = File.Exists(path) ? new FileInfo(path).Length : 0,
                };
                entries.Add(path, entry);
            }

            entry.Users.Add(GetHierarchyPath(user.transform));
        }

        /// <summary>
        /// 中身が全く同じファイルを探して、2つ目以降を「重複」にする
        /// （同じテクスチャを別のフォルダにコピーして使っている場合など）
        /// </summary>
        private void FindSameContent()
        {
            List<AssetEntry> all = entries.Values.ToList();
            using (MD5 md5 = MD5.Create())
            {
                for (int i = 0; i < all.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("アセット収集", $"重複を調べています… {all[i].Path}", (float)i / all.Count);
                    if (!File.Exists(all[i].Path)) continue;

                    // ファイルの中身から計算した値。中身が1バイトでも違えば別の値になる
                    using (FileStream stream = File.OpenRead(all[i].Path))
                    {
                        all[i].Hash = BitConverter.ToString(md5.ComputeHash(stream));
                    }
                }
            }

            // 同じ種類で、同じハッシュのものをまとめる。パスが一番短いものを代表にする
            foreach (var group in all.Where(e => e.Hash != null).GroupBy(e => e.Category + e.Hash))
            {
                List<AssetEntry> sorted = group.OrderBy(e => e.Path.Length).ThenBy(e => e.Path).ToList();
                AssetEntry original = sorted[0];
                for (int i = 1; i < sorted.Count; i++)
                {
                    sorted[i].DuplicateOf = original.Path;

                    // 重複している方を使っているオブジェクトも、代表の「使用箇所」に数える
                    original.Users.UnionWith(sorted[i].Users);
                }
            }
        }

        // ===================================================================
        // 一覧の保存
        // ===================================================================

        private void SaveCsv()
        {
            string file = EditorUtility.SaveFilePanel("一覧を保存", "", "SceneAssets.csv", "csv");
            if (string.IsNullOrEmpty(file)) return;

            var csv = new StringBuilder();
            csv.AppendLine("種類,ファイル名,パス,サイズ(KB),使用箇所の数,重複元,使用しているオブジェクト（最大5件）");

            foreach (string category in Categories)
            {
                foreach (AssetEntry entry in GetEntries(category))
                {
                    string users = string.Join(" / ", entry.Users.Take(5));
                    csv.AppendLine(string.Join(",",
                        Escape(CategoryLabels[category]),
                        Escape(Path.GetFileName(entry.Path)),
                        Escape(entry.Path),
                        (entry.Size / 1024f).ToString("0.0"),
                        entry.Users.Count,
                        Escape(entry.DuplicateOf ?? ""),
                        Escape(users)));
                }
            }

            // Excel で日本語が文字化けしないよう、BOM 付きの UTF-8 で保存する
            File.WriteAllText(file, csv.ToString(), new UTF8Encoding(true));
            EditorUtility.RevealInFinder(file);
        }

        // ===================================================================
        // 収集
        // ===================================================================

        /// <summary>
        /// プロジェクト内のフォルダにコピーする（種類ごとにフォルダを分ける）
        /// ※ コピーしたマテリアルは、元のテクスチャを参照したままになる。
        ///   参照ごと持ち運びたい場合は .unitypackage の方を使う
        /// </summary>
        private void CopyIntoProject()
        {
            string destination = copyDestination.TrimEnd('/');
            if (!destination.StartsWith("Assets"))
            {
                EditorUtility.DisplayDialog("アセット収集", "コピー先は Assets から始まるフォルダにしてください", "OK");
                return;
            }

            List<AssetEntry> targets = GetUniqueEntries().ToList();
            if (!EditorUtility.DisplayDialog("アセット収集",
                    $"{targets.Count} 個のファイルを「{destination}」にコピーします", "コピー", "キャンセル"))
            {
                return;
            }

            int copied = 0;
            try
            {
                // ※ StartAssetEditing でまとめると速くなるが、作ったばかりのフォルダや
                //   コピーしたばかりのファイルが見えず、名前の重なりを正しく判定できないので使わない
                for (int i = 0; i < targets.Count; i++)
                {
                    AssetEntry entry = targets[i];
                    EditorUtility.DisplayProgressBar("アセット収集", $"コピー中… {entry.Path}", (float)i / targets.Count);

                    // 自分自身をコピー先に入れないように（2回目以降の収集でコピー済みのものを拾った場合）
                    if (entry.Path.StartsWith(destination + "/")) continue;

                    string folder = $"{destination}/{entry.Category}";
                    EnsureFolder(folder);

                    // 同じ名前のファイルがあれば「名前 1」のように自動で名前を変える
                    string newPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{Path.GetFileName(entry.Path)}");
                    if (AssetDatabase.CopyAsset(entry.Path, newPath)) copied++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[SceneAssetCollector] {copied} 個のファイルを {destination} にコピーしました");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(destination));
        }

        /// <summary>
        /// .unitypackage として書き出す
        /// マテリアルとテクスチャの参照が保たれるので、別のプロジェクトにそのまま読み込める
        /// </summary>
        private void ExportPackage()
        {
            string file = EditorUtility.SaveFilePanel("unitypackage で書き出し", "", "CollectedAssets.unitypackage", "unitypackage");
            if (string.IsNullOrEmpty(file)) return;

            string[] paths = GetUniqueEntries().Select(e => e.Path).ToArray();
            ExportPackageOptions options = includeDependencies
                ? ExportPackageOptions.IncludeDependencies
                : ExportPackageOptions.Default;

            AssetDatabase.ExportPackage(paths, file, options);
            EditorUtility.RevealInFinder(file);
        }

        /// <summary>
        /// プロジェクトの外のフォルダに、Assets/... のフォルダ構成のまま .meta ごとコピーする
        /// .meta に参照用の ID（GUID）が入っているので、別プロジェクトの Assets にフォルダごと入れれば参照が保たれる
        /// </summary>
        private void CopyToExternalFolder()
        {
            string folder = EditorUtility.SaveFolderPanel("コピー先のフォルダ", "", "");
            if (string.IsNullOrEmpty(folder)) return;

            List<AssetEntry> targets = GetUniqueEntries().ToList();
            int copied = 0;
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    string path = targets[i].Path;
                    EditorUtility.DisplayProgressBar("アセット収集", $"コピー中… {path}", (float)i / targets.Count);
                    if (!File.Exists(path)) continue;

                    string destination = Path.Combine(folder, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(path, destination, true);

                    // .meta（インポート設定と GUID）も一緒にコピーする
                    if (File.Exists(path + ".meta")) File.Copy(path + ".meta", destination + ".meta", true);
                    copied++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[SceneAssetCollector] {copied} 個のファイルを {folder} にコピーしました");
            EditorUtility.RevealInFinder(folder);
        }

        // ===================================================================
        // 便利関数
        // ===================================================================

        private IEnumerable<AssetEntry> GetEntries(string category)
        {
            return entries.Values
                .Where(e => e.Category == category)
                .OrderBy(e => e.IsDuplicate)      // 重複は下にまとめる
                .ThenBy(e => e.Path);
        }

        /// <summary>重複を除いたアセット（収集するもの）</summary>
        private IEnumerable<AssetEntry> GetUniqueEntries()
        {
            return entries.Values.Where(e => !e.IsDuplicate);
        }

        /// <summary>
        /// 集めてよいパスか
        /// Assets/ か Packages/ の中にあるファイルだけを対象にする
        /// （Unity の組み込み（Library/unity default resources など）や、シーンの中だけにあるものは除く）
        /// </summary>
        private static bool IsCollectablePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!path.StartsWith("Assets/") && !path.StartsWith("Packages/")) return false;
            return !path.EndsWith(".unity"); // シーンファイルの中に埋め込まれたものは除く
        }

        /// <summary>「親/子/孫」のような、Hierarchy 上の場所を表す文字列</summary>
        private static string GetHierarchyPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        /// <summary>フォルダが無ければ作る（途中のフォルダもまとめて作る）</summary>
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024) return $"{bytes / 1024f / 1024f:0.0} MB";
            return $"{bytes / 1024f:0} KB";
        }

        /// <summary>CSV 用：カンマや改行を含む文字列を " で囲む</summary>
        private static string Escape(string value)
        {
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}
