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
    var rends = go.GetComponentsInChildren<Renderer>(true);
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
    d *= 0.45f + 0.95f * Mathf.PerlinNoise(x * 0.026f + 4.1f, z * 0.026f + 2.7f);   // 疎密の塊

    // 開けておく場所
    d *= 1f - 0.95f * Circle(x, z, 100f, -97f, 32f);   // 遺跡広場
    d *= 1f - 0.95f * Circle(x, z, 97f, -155f, 30f);   // キャンプ
    d *= 1f - 0.9f * Circle(x, z, 115f, -45f, 34f);    // 教会テラス
    d *= 1f - 0.9f * Circle(x, z, 150f, -58f, 22f);    // 墓地
    d *= 1f - 0.95f * Circle(x, z, 46f, -40f, 30f);    // 風車の丘（ピクセルアートの木を置く区画）
    d *= 1f - Circle(x, z, 32f, -128f, 24f);           // 池A
    d *= 1f - Circle(x, z, 46f, -82f, 22f);            // 池B
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


int cWater=0,cSlope=0,cDens=0,cPath=0,cObs=0,cOk=0;
string worst="";
for (int i=0;i<8000;i++){
  float x = Rnd(4f,60f), z = Rnd(-160f,-50f);
  if (Ground(x,z) < WATER + 1.2f) { cWater++; continue; }
  if (Normal(x,z).y < 0.82f) { cSlope++; continue; }
  if (rng.NextDouble() > ForestDensity(x,z)) { cDens++; continue; }
  if (PathDist(x,z) < 6.5f) { cPath++; continue; }
  bool hit=false;
  foreach (var o in obstacles) if (x > o.x-2.7f && x < o.z+2.7f && z > o.y-2.7f && z < o.w+2.7f) { hit=true; worst=o.ToString(); break; }
  if (hit) { cObs++; continue; }
  cOk++;
}
return $"water={cWater} slope={cSlope} dens={cDens} path={cPath} obstacle={cObs} ok={cOk} lastObs={worst} obsCount={obstacles.Count}";
