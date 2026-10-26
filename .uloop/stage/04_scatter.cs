using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

var terrainGo = GameObject.Find("Terrain_(0.00, 0.00, -200.00)");
var terrain = terrainGo.GetComponent<Terrain>();
var td = terrain.terrainData;
var root = GameObject.Find("Stage_ForestRuins").transform;
const float WATER = 4.5f;
var rng = new System.Random(77123);

// 散布グループだけ作り直す
foreach (var g in new[] { "10_Forest", "11_Rocks", "12_Undergrowth", "13_Overgrowth" })
{
    var old = root.Find(g);
    if (old != null) Object.DestroyImmediate(old.gameObject);
}

var groups = new Dictionary<string, Transform>();
Transform G(string name)
{
    if (!groups.ContainsKey(name))
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        groups[name] = go.transform;
    }
    return groups[name];
}

float Rnd(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
float Ground(float x, float z) { return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y; }

Vector3 Normal(float x, float z)
{
    return td.GetInterpolatedNormal((x - terrain.transform.position.x) / td.size.x,
                                    (z - terrain.transform.position.z) / td.size.z);
}

// ---- 既に置いてあるものを避けるための障害物リスト -----------------------
// 円だと水道橋のような細長い建物が広く場所を取りすぎるので、XZ の矩形で判定する
var obstacles = new List<Vector4>();   // xmin, zmin, xmax, zmax（pad 込み）
void AddObstacle(GameObject go, float pad)
{
    if (go.name.StartsWith("Water_Plane")) return;   // 水面は高さ判定で避けるので対象外
    // パーティクルの Renderer は境界が巨大なので、メッシュだけで大きさを測る
    var rends = go.GetComponentsInChildren<MeshRenderer>(true);
    if (rends.Length == 0) return;
    var b = rends[0].bounds;
    foreach (var r in rends) b.Encapsulate(r.bounds);
    obstacles.Add(new Vector4(b.min.x - pad, b.min.z - pad, b.max.x + pad, b.max.z + pad));
}

foreach (Transform grp in root)
{
    if (grp.name.StartsWith("1")) continue;          // 散布グループ自身は無視
    foreach (Transform o in grp) AddObstacle(o.gameObject, 1.0f);
}
foreach (Transform o in terrainGo.transform) AddObstacle(o.gameObject, 1.5f);   // ユーザーが置いたテント等
var bedding = GameObject.Find("NF_Prop_Bedding_001");
if (bedding != null) AddObstacle(bedding, 1.5f);
obstacles.Add(new Vector4(102.8f, -174.8f, 114.8f, -162.8f));   // プレイヤーのスポーン地点

// ---- 道（ここは必ず空けておく）-----------------------------------------
var paths = new List<Vector2[]>
{
    new [] { new Vector2(108,-178), new Vector2(103,-158), new Vector2(100,-140),
             new Vector2(100,-120), new Vector2(100,-104), new Vector2(101,-88),
             new Vector2(106,-72),  new Vector2(112,-58),  new Vector2(115,-50) },
    new [] { new Vector2(92,-110), new Vector2(76,-112), new Vector2(64,-106), new Vector2(56,-101) },
    new [] { new Vector2(96,-88), new Vector2(80,-74), new Vector2(66,-62),
             new Vector2(56,-52), new Vector2(50,-44) },
    new [] { new Vector2(118,-56), new Vector2(132,-56), new Vector2(145,-57) },
};

float PathDist(float x, float z)
{
    float best = 999f;
    var p = new Vector2(x, z);
    foreach (var pts in paths)
    {
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector2 a = pts[i], b = pts[i + 1], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
    }
    return best;
}

float Circle(float x, float z, float cx, float cz, float r)
{ return Smooth(1f - Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) / r); }

// ---- 森の密度 -----------------------------------------------------------
// 外周ほど濃く、中央の遺跡・広場は開ける。Perlin で疎密の塊を作る
float ForestDensity(float x, float z)
{
    float border = Mathf.Min(Mathf.Min(x, 200f - x), Mathf.Min(z + 200f, -z));
    float d = Mathf.Clamp01((78f - border) / 58f);            // 外周に向かって濃くなる
    d = Mathf.Max(d, 0.6f * Circle(x, z, 175f, -125f, 62f));  // 東の森
    d = Mathf.Max(d, 0.55f * Circle(x, z, 120f, -18f, 58f));  // 北の森
    d = Mathf.Max(d, 0.5f * Circle(x, z, 20f, -60f, 40f));    // 西の森
    d = Mathf.Max(d, 0.45f * Circle(x, z, 70f, -180f, 45f));  // 南の森
    d = Mathf.Max(d, 0.38f);                                  // 平地にもぽつぽつ立たせる
    // キャンプと遺跡広場の間に木立の帯を作り、門をくぐるまで広場が見えないようにする
    d = Mathf.Max(d, 0.95f * Smooth(1f - Mathf.Abs(z + 136f) / 9f) * Smooth((Mathf.Abs(x - 100f) - 8f) / 12f));
    d *= 0.45f + 0.95f * Mathf.PerlinNoise(x * 0.026f + 4.1f, z * 0.026f + 2.7f);   // 疎密の塊

    // 開けておく場所
    d *= 1f - 0.95f * Circle(x, z, 100f, -97f, 32f);   // 遺跡広場
    d *= 1f - 0.95f * Circle(x, z, 97f, -155f, 30f);   // キャンプ
    d *= 1f - 0.9f * Circle(x, z, 115f, -45f, 34f);    // 教会テラス
    // 教会の参道は完全に空ける（正面に木が生えると台無しになる）
    if (x > 98f && x < 134f && z > -70f && z < -18f) d = 0f;
    // 南から池を見たときの抜け。手前に木が並ぶと水面が隠れる
    d *= 1f - 0.75f * Smooth(1f - Mathf.Abs(x - 50f) / 20f) * Smooth(1f - Mathf.Abs(z + 108f) / 24f);
    d *= 1f - 0.9f * Circle(x, z, 150f, -58f, 22f);    // 墓地
    d *= 1f - 0.95f * Circle(x, z, 46f, -40f, 30f);    // 風車の丘（ピクセルアートの木を置く区画）
    d *= 1f - Circle(x, z, 32f, -128f, 19f);           // 池A
    d *= 1f - Circle(x, z, 46f, -82f, 17f);            // 池B
    d *= 1f - 0.85f * Circle(x, z, 146f, -95f, 20f);   // 水道橋
    return Mathf.Clamp01(d);
}

// ---- 配置の共通処理 -----------------------------------------------------
var placedPts = new List<Vector3>();   // x, z, 半径

bool Blocked(float x, float z, float rad, float pathClear)
{
    if (PathDist(x, z) < pathClear) return true;
    foreach (var o in obstacles)
    {
        if (x > o.x - rad && x < o.z + rad && z > o.y - rad && z < o.w + rad) return true;
    }
    foreach (var p in placedPts)
    {
        float dx = x - p.x, dz = z - p.y;
        if (dx * dx + dz * dz < (p.z + rad) * (p.z + rad)) return true;
    }
    return false;
}

var cache = new Dictionary<string, float>();
float MinY(GameObject prefab, string path)
{
    if (cache.ContainsKey(path)) return cache[path];
    float m = 0f;
    var rends = prefab.GetComponentsInChildren<Renderer>(true);
    if (rends.Length > 0)
    {
        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        m = b.min.y;
    }
    cache[path] = m;
    return m;
}

int total = 0;
void Put(string group, string folder, string name, float x, float z, float scale,
         float sink, float radius, bool tilt)
{
    string path = "Assets/" + folder + "/Prefabs/" + name + ".prefab";
    var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    if (src == null) return;
    var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, G(group));
    inst.transform.position = new Vector3(x, Ground(x, z) - MinY(src, path) * scale - sink, z);
    Quaternion rot = Quaternion.Euler(0f, Rnd(0f, 360f), 0f);
    if (tilt) rot = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, Normal(x, z)), 0.8f) * rot;
    inst.transform.rotation = rot;
    inst.transform.localScale = Vector3.one * scale;
    placedPts.Add(new Vector3(x, z, radius));
    total++;
}

// =======================================================================
// 10_Forest  森
// =======================================================================
string MF = "Meadow Forest Props";
string[] pines = { "NF_Prop_Pine_01", "NF_Prop_Pine_02", "NF_Prop_Pine_03" };
string[] broad = { "NF_Prop_Oak_02", "NF_Prop_Tree_01", "NF_Prop_Tree_02" };

int tries = 70000, trees = 0;
for (int i = 0; i < tries && trees < 340; i++)
{
    float x = Rnd(4f, 196f), z = Rnd(-196f, -4f);
    if (Ground(x, z) < WATER + 0.5f) continue;                 // 水中には置かない
    if (Normal(x, z).y < 0.82f) continue;                      // 急斜面は避ける
    if (rng.NextDouble() > ForestDensity(x, z)) continue;
    if (Blocked(x, z, 2.7f, 6.5f)) continue;

    bool pine = rng.NextDouble() < 0.62;
    string nm = pine ? pines[rng.Next(pines.Length)] : broad[rng.Next(broad.Length)];
    float sc = pine ? Rnd(2.2f, 3.4f) : Rnd(1.9f, 2.8f);
    if (nm == "NF_Prop_Oak_02") sc = Rnd(1.5f, 2.1f);
    Put("10_Forest", MF, nm, x, z, sc, 0.1f, 2.7f, false);
    trees++;
}

// =======================================================================
// 11_Rocks  岩（斜面・湖畔・尾根）
// =======================================================================
string[] rocksBig = { "NF_Prop_Rock_05", "NF_Prop_Rock_07", "NF_Prop_Rock_12", "NF_Prop_Rock_13",
                      "NF_Prop_Rock_14", "NF_Prop_Rock_15", "NF_Prop_Rock_21" };
string[] rocksSmall = { "NF_Prop_Rock_01", "NF_Prop_Rock_02", "NF_Prop_Rock_03", "NF_Prop_Rock_04",
                        "NF_Prop_Rock_16", "NF_Prop_Rock_18", "NF_Prop_Rock_19" };
int rocks = 0;
for (int i = 0; i < 40000 && rocks < 120; i++)
{
    float x = Rnd(4f, 196f), z = Rnd(-196f, -4f);
    float g = Ground(x, z);
    if (g < WATER + 0.2f) continue;

    var n = Normal(x, z);
    float slope = 1f - n.y;
    // 傾斜地、湖畔、外周の丘に岩が集まる
    float w = Mathf.Clamp01(slope * 9f + 0.13f);
    w = Mathf.Max(w, 0.5f * Circle(x, z, 32f, -128f, 30f));
    w = Mathf.Max(w, 0.5f * Circle(x, z, 46f, -82f, 28f));
    w = Mathf.Max(w, 0.35f * Circle(x, z, 180f, -100f, 55f));
    w *= 0.5f + Mathf.PerlinNoise(x * 0.05f + 9f, z * 0.05f + 1f);
    if (rng.NextDouble() > w * 0.75f) continue;
    if (Blocked(x, z, 2.2f, 7f)) continue;

    bool big = rng.NextDouble() < 0.35;
    string nm = big ? rocksBig[rng.Next(rocksBig.Length)] : rocksSmall[rng.Next(rocksSmall.Length)];
    float sc = big ? Rnd(1.5f, 2.6f) : Rnd(1.6f, 3.0f);
    Put("11_Rocks", MF, nm, x, z, sc, Rnd(0.2f, 0.8f), big ? 3.0f : 1.6f, true);
    rocks++;
}

// =======================================================================
// 12_Undergrowth  茂み・切り株・枝・キノコ
// =======================================================================
string[] bushes = { "NF_Prop_Bush_01", "NF_Prop_Bush_02", "NF_Prop_Bush_03", "NF_Prop_Bush_04" };
int bush = 0;
for (int i = 0; i < 45000 && bush < 210; i++)
{
    float x = Rnd(4f, 196f), z = Rnd(-196f, -4f);
    float g = Ground(x, z);
    if (g < WATER + 0.4f) continue;
    float w = ForestDensity(x, z) * 0.9f + 0.12f;
    w = Mathf.Max(w, 0.55f * Circle(x, z, 32f, -128f, 27f));    // 岸辺の茂み
    w = Mathf.Max(w, 0.55f * Circle(x, z, 46f, -82f, 25f));
    if (rng.NextDouble() > w) continue;
    if (Blocked(x, z, 1.6f, 6.5f)) continue;
    Put("12_Undergrowth", MF, bushes[rng.Next(bushes.Length)], x, z, Rnd(1.7f, 2.8f), 0.25f, 1.6f, false);
    bush++;
}

string[] stumps = { "NF_Prop_Stump_01", "NF_Prop_Stump_02", "NF_Prop_Stump_03", "NF_Prop_Stump_04",
                    "NF_Prop_TreeStump_01", "NF_Prop_TreeStump_02" };
string[] branches = { "NF_Prop_Branch_01", "NF_Prop_Branch_02", "NF_Prop_Branch_03" };
int deco = 0;
for (int i = 0; i < 40000 && deco < 95; i++)
{
    float x = Rnd(6f, 194f), z = Rnd(-194f, -6f);
    if (Ground(x, z) < WATER + 0.4f) continue;
    if (rng.NextDouble() > ForestDensity(x, z) * 0.8f + 0.06f) continue;
    if (Blocked(x, z, 1.3f, 6f)) continue;
    bool st = rng.NextDouble() < 0.55;
    Put("12_Undergrowth", MF, st ? stumps[rng.Next(stumps.Length)] : branches[rng.Next(branches.Length)],
        x, z, st ? Rnd(2.0f, 3.0f) : Rnd(2.0f, 2.8f), 0.15f, 1.8f, !st);
    deco++;
}

// キノコは切り株や木の根元にまとまって生える
string[] mush = { "NF_Prop_Mushrooms_01", "NF_Prop_Mushrooms_02", "NF_Prop_Mushrooms_03", "NF_Prop_Mushrooms_04",
                  "NF_Prop_Mushrooms_05", "NF_Prop_Mushrooms_06", "NF_Prop_Mushrooms_07", "NF_Prop_Mushrooms_08" };
int mushrooms = 0;
var anchors = placedPts.Where(p => p.z > 1.7f).ToList();
for (int i = 0; i < 2500 && mushrooms < 130; i++)
{
    var a = anchors[rng.Next(anchors.Count)];
    float ang = Rnd(0f, 360f), rad = Rnd(a.z * 0.7f, a.z * 1.6f);
    float x = a.x + Mathf.Sin(ang * Mathf.Deg2Rad) * rad;
    float z = a.y + Mathf.Cos(ang * Mathf.Deg2Rad) * rad;
    if (x < 5f || x > 195f || z > -5f || z < -195f) continue;
    if (Ground(x, z) < WATER + 0.5f) continue;
    if (Blocked(x, z, 0.5f, 5.5f)) continue;
    Put("12_Undergrowth", MF, mush[rng.Next(mush.Length)], x, z, Rnd(3.5f, 6.0f), 0.05f, 0.5f, false);
    mushrooms++;
}

// 外周の壁になる木。カメラが地形の端を映しても、木立が向こう側を隠すようにする
// 木同士は重なってよいので、他の散布物とは独立に間隔を見る
var wallPts = new List<Vector2>();
int wall = 0;
for (int i = 0; i < 120000 && wall < 700; i++)
{
    float x = Rnd(1f, 199f), z = Rnd(-199f, -1f);
    float bd = Mathf.Min(Mathf.Min(x, 200f - x), Mathf.Min(z + 200f, -z));
    if (bd > 30f) continue;
    if (Ground(x, z) < WATER + 0.5f) continue;
    if (PathDist(x, z) < 6f) continue;

    bool hit = false;
    foreach (var o in obstacles)
        if (x > o.x - 2f && x < o.z + 2f && z > o.y - 2f && z < o.w + 2f) { hit = true; break; }
    if (hit) continue;
    foreach (var p in wallPts)
        if ((x - p.x) * (x - p.x) + (z - p.y) * (z - p.y) < 13f) { hit = true; break; }
    if (hit) continue;

    // 端に近いほど大きくして、地形の切れ目を隠す壁にする
    float big = Mathf.Lerp(4.2f, 2.6f, Mathf.Clamp01(bd / 30f));
    Put("10_Forest", MF, pines[rng.Next(pines.Length)], x, z, big * Rnd(0.85f, 1.1f), 0.1f, 0.1f, false);
    wallPts.Add(new Vector2(x, z));
    wall++;
}


// =======================================================================
// 13_Overgrowth  遺跡にまとわりつく草・苔・キノコ
// 障害物の余白を無視して、崩れた壁や柱の足元に寄せて置く
// =======================================================================
var ruinAnchors = new List<Vector3>();
foreach (var gname in new[] { "02_Gate", "03_Plaza", "04_Church", "05_Graveyard", "08_EastForest", "09_Aqueduct" })
{
    var g = root.Find(gname);
    if (g == null) continue;
    foreach (Transform o in g)
    {
        var mr = o.GetComponentsInChildren<MeshRenderer>(true);
        if (mr.Length == 0) continue;
        var b = mr[0].bounds;
        foreach (var r in mr) b.Encapsulate(r.bounds);
        if (b.size.y < 1.2f) continue;                    // 平たい敷石は対象外
        ruinAnchors.Add(new Vector3(b.center.x, b.center.z, Mathf.Max(b.size.x, b.size.z) * 0.5f));
    }
}

string[] small = { "NF_Prop_Bush_01", "NF_Prop_Bush_02", "NF_Prop_Bush_03",
                   "NF_Prop_Mushrooms_06", "NF_Prop_Mushrooms_07", "NF_Prop_Rock_01",
                   "NF_Prop_Rock_03", "NF_Prop_Rock_18" };
int moss = 0;
for (int i = 0; i < 4000 && moss < 150; i++)
{
    var a = ruinAnchors[rng.Next(ruinAnchors.Count)];
    float ang = Rnd(0f, 360f), rad = a.z * Rnd(0.85f, 1.35f);
    float x = a.x + Mathf.Sin(ang * Mathf.Deg2Rad) * rad;
    float z = a.y + Mathf.Cos(ang * Mathf.Deg2Rad) * rad;
    if (x < 5f || x > 195f || z > -5f || z < -195f) continue;
    if (Ground(x, z) < WATER + 0.5f) continue;
    if (PathDist(x, z) < 4.5f) continue;

    string nm = small[rng.Next(small.Length)];
    bool isBush = nm.Contains("Bush");
    float sc = isBush ? Rnd(1.3f, 2.2f) : (nm.Contains("Mush") ? Rnd(3.5f, 5.5f) : Rnd(1.6f, 2.6f));
    Put("13_Overgrowth", MF, nm, x, z, sc, isBush ? 0.35f : 0.25f, 0.6f, !isBush);
    moss++;
}

return $"trees={trees} wall={wall} rocks={rocks} bushes={bush} deco={deco} mushrooms={mushrooms} moss={moss} total={total}";
