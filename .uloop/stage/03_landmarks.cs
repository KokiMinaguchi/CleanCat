using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// ===== 共通ヘルパー =====================================================
var terrainGo = GameObject.Find("Terrain_(0.00, 0.00, -200.00)");
var terrain = terrainGo.GetComponent<Terrain>();
var root = GameObject.Find("Stage_ForestRuins").transform;
const float WATER = 4.5f;      // 水面の高さ
var rng = new System.Random(20260910);

// 手置きグループだけ作り直す（自動散布は別スクリプト）
var handMade = new[] { "01_Camp", "02_Gate", "03_Plaza", "04_Church", "05_Graveyard",
                       "06_Lake", "07_WindmillHill", "08_EastForest", "09_Aqueduct" };
foreach (var g in handMade)
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

var minYCache = new Dictionary<string, float>();
float MinY(GameObject prefab, string path)
{
    if (minYCache.ContainsKey(path)) return minYCache[path];
    float m = 0f;
    var rends = prefab.GetComponentsInChildren<Renderer>(true);
    if (rends.Length > 0)
    {
        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        m = b.min.y;
    }
    minYCache[path] = m;
    return m;
}

float Ground(float x, float z) { return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y; }

string PathOf(string tag, string name)
{
    if (tag == "X") return "Assets/3DPixelArtEnvironment/Prefabs/" + name + ".prefab";
    if (tag == "R") return "Assets/Forgotten Ruins - Nimble Fox/Prefabs/" + name + ".prefab";
    return "Assets/Meadow Forest Props/Prefabs/" + name + ".prefab";
}

int placed = 0;
var missing = new List<string>();

GameObject Spawn(string group, string tag, string name, float x, float z, float rotY, float scale, float sink, bool useY, float y)
{
    string path = PathOf(tag, name);
    var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    if (src == null) { if (!missing.Contains(name)) missing.Add(name); return null; }

    var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, G(group));
    float baseY = useY ? y : Ground(x, z);
    inst.transform.position = new Vector3(x, baseY - MinY(src, path) * scale - sink, z);
    inst.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
    inst.transform.localScale = Vector3.one * scale;
    placed++;
    return inst;
}

// 地面に置く
GameObject P(string group, string tag, string name, float x, float z, float rotY, float scale)
{ return Spawn(group, tag, name, x, z, rotY, scale, 0f, false, 0f); }
// 少し埋める（岩・切り株など）
GameObject PS(string group, string tag, string name, float x, float z, float rotY, float scale, float sink)
{ return Spawn(group, tag, name, x, z, rotY, scale, sink, false, 0f); }
// 高さを指定する（水辺）
GameObject PY(string group, string tag, string name, float x, float z, float rotY, float scale, float y)
{ return Spawn(group, tag, name, x, z, rotY, scale, 0f, true, y); }

float Rnd(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

// =======================================================================
// 01_Camp  ねこのキャンプ（スタート地点・南）
// =======================================================================
P("01_Camp", "M", "NF_Struct_Cabin_01", 76f, -152f, 105f, 2.0f);
P("01_Camp", "M", "NF_Struct_Cabin_02", 118f, -150f, 250f, 2.0f);
P("01_Camp", "M", "NF_Struct_Watchtower_01", 126f, -167f, 20f, 1.8f);
P("01_Camp", "M", "NF_Prop_WoodenCart_01", 106f, -149f, 35f, 3.0f);
P("01_Camp", "M", "NF_Prop_Barrel_01", 110.5f, -154.5f, 0f, 3.0f);
P("01_Camp", "M", "NF_Prop_Barrel_02", 112.5f, -153.0f, 40f, 3.0f);
P("01_Camp", "M", "NF_Prop_Barrel_01", 111.0f, -151.5f, 70f, 2.8f);
P("01_Camp", "M", "NF_Prop_WoodenBox_01", 82f, -146f, 15f, 2.6f);
P("01_Camp", "M", "NF_Prop_WoodenBox_01", 83.5f, -147.5f, 55f, 2.6f);
P("01_Camp", "M", "NF_Prop_Chest_01", 80f, -158f, 130f, 3.0f);
P("01_Camp", "M", "NF_Prop_Bucket_01", 87.5f, -161f, 0f, 3.0f);
P("01_Camp", "M", "NF_Prop_CookingGear_01", 88.5f, -162.5f, 20f, 3.5f);
P("01_Camp", "M", "NF_Prop_CookingGear_02", 92.5f, -165.5f, 250f, 3.5f);
P("01_Camp", "M", "NF_Prop_CookingGear_03", 88f, -166.5f, 60f, 3.5f);
P("01_Camp", "M", "NF_Prop_Bedroll_01", 94f, -160.5f, 100f, 3.5f);
P("01_Camp", "M", "NF_Prop_Bedroll_01", 103f, -163f, 280f, 3.5f);
P("01_Camp", "M", "NF_Prop_Lantern_01", 99f, -158f, 0f, 4.0f);
P("01_Camp", "M", "NF_Prop_Lantern_01", 90f, -150f, 0f, 4.0f);
P("01_Camp", "M", "NF_Prop_RopeBundle_01", 107.5f, -147f, 30f, 3.0f);
P("01_Camp", "M", "NF_Prop_Branch_01", 86f, -167f, 25f, 2.5f);
P("01_Camp", "M", "NF_Prop_Branch_02", 84f, -163f, 200f, 2.5f);
P("01_Camp", "M", "NF_Prop_Book_02", 103.5f, -162.5f, 15f, 4.0f);
P("01_Camp", "M", "NF_Prop_WoodenSign_04", 104f, -173f, 8f, 3.0f);
P("01_Camp", "M", "NF_Prop_WoodenSign_02", 92f, -142f, 190f, 3.0f);
PS("01_Camp", "X", "Stone_1", 118f, -160f, 40f, 2.5f, 0.4f);
PS("01_Camp", "X", "Stone_2", 74f, -164f, 120f, 2.2f, 0.5f);

// =======================================================================
// 02_Gate  遺跡への門（キャンプと広場の間）
// =======================================================================
P("02_Gate", "R", "NF_Struc_Arch_02A", 100f, -126f, 0f, 2.0f);
P("02_Gate", "R", "NF_Prop_StonePillar_02A", 87f, -126f, 20f, 2.0f);
P("02_Gate", "R", "NF_Prop_StonePillar_04A", 113f, -126f, -15f, 2.0f);
P("02_Gate", "R", "NF_Prop_StonePillar_03", 116f, -131f, 40f, 2.0f);
P("02_Gate", "R", "NF_Prop_BrokenWall_07", 78f, -124f, 12f, 2.0f);
P("02_Gate", "R", "NF_Prop_BrokenWall_07", 124f, -122f, -18f, 2.0f);
P("02_Gate", "R", "NF_Prop_Beam_01", 106f, -130f, 60f, 2.0f);
P("02_Gate", "M", "NF_Prop_WoodenSign_03", 94f, -118f, 165f, 3.0f);
PS("02_Gate", "R", "NF_Prop_Slab_03", 97f, -121f, 25f, 2.2f, 0.15f);
PS("02_Gate", "R", "NF_Prop_Slab_05", 104f, -119f, 300f, 2.0f, 0.2f);
PS("02_Gate", "X", "Slab", 100f, -133f, 5f, 2.2f, 0.3f);

// =======================================================================
// 03_Plaza  遺跡広場（ステージの中心）
// =======================================================================
P("03_Plaza", "R", "NF_Prop_Fountain_01B", 100f, -97f, 15f, 1.6f);

// 噴水を囲む列柱。立っている柱と倒れた柱を交互に
string[] standing = { "NF_Prop_StonePillar_01A", "NF_Prop_StonePillar_02A", "NF_Prop_StonePillar_03A",
                      "NF_Prop_StonePillar_04A", "NF_Prop_StonePillar_05A" };
string[] fallen = { "NF_Prop_StonePillar_01", "NF_Prop_StonePillar_02", "NF_Prop_StonePillar_05" };
for (int i = 0; i < 10; i++)
{
    float a = i * 36f + 12f;
    float rad = 19f;
    float px = 100f + Mathf.Sin(a * Mathf.Deg2Rad) * rad;
    float pz = -97f + Mathf.Cos(a * Mathf.Deg2Rad) * rad;
    bool up = (i % 3) != 2;
    string nm = up ? standing[i % standing.Length] : fallen[i % fallen.Length];
    PS("03_Plaza", "R", nm, px, pz, Rnd(0f, 360f), Rnd(1.9f, 2.15f), up ? 0.05f : 0.2f);
}

P("03_Plaza", "R", "NF_Prop_Altar_01B", 100f, -78f, 180f, 2.0f);
P("03_Plaza", "R", "NF_Prop_AncientScroll_01", 97f, -80f, 40f, 3.0f);
P("03_Plaza", "R", "NF_Prop_Chest_01", 103.5f, -80f, 210f, 2.2f);
P("03_Plaza", "R", "NF_Prop_Statue_01B", 87f, -81f, 150f, 2.0f);
P("03_Plaza", "R", "NF_Prop_Statue_01", 113f, -81f, 205f, 2.0f);
P("03_Plaza", "R", "NF_Prop_SwordinStone_01", 91f, -114f, 25f, 2.4f);
P("03_Plaza", "R", "NF_Prop_BrokenWall_02", 74f, -95f, 25f, 1.8f);
P("03_Plaza", "R", "NF_Prop_BrokenWall_08", 126f, -93f, -20f, 1.8f);
P("03_Plaza", "R", "NF_Prop_BrokenWall_01", 120f, -112f, 200f, 1.8f);
P("03_Plaza", "R", "NF_Prop_BrokenWall_04", 78f, -110f, 150f, 1.8f);
P("03_Plaza", "R", "NF_Prop_WoodenBench_01", 84f, -104f, 65f, 2.0f);
P("03_Plaza", "R", "NF_Prop_WoodenBench_01B", 116f, -103f, -55f, 2.0f);
P("03_Plaza", "R", "NF_Prop_Beam02A", 108f, -87f, 30f, 1.8f);
P("03_Plaza", "R", "NF_Prop_Beam_01A", 110f, -90f, 75f, 1.8f);
P("03_Plaza", "R", "NF_Prop_WoodenBox_01", 82f, -91f, 20f, 3.0f);
P("03_Plaza", "M", "NF_Prop_Crystals_03", 94f, -105f, 0f, 5.0f);
P("03_Plaza", "M", "NF_Prop_Crystals_01", 107f, -104f, 40f, 5.0f);
P("03_Plaza", "M", "NF_Prop_Lantern_01", 100f, -117f, 0f, 4.0f);
// 敷石
for (int i = 0; i < 9; i++)
{
    float a = Rnd(0f, 360f), rad = Rnd(14f, 27f);
    string nm = (i % 3 == 0) ? "NF_Prop_Slab_03" : (i % 3 == 1 ? "NF_Prop_Slab_05" : "NF_Prop_Slab_04");
    PS("03_Plaza", "R", nm, 100f + Mathf.Sin(a * Mathf.Deg2Rad) * rad, -97f + Mathf.Cos(a * Mathf.Deg2Rad) * rad,
       Rnd(0f, 360f), Rnd(1.7f, 2.3f), 0.2f);
}

// =======================================================================
// 04_Church  丘の上の教会跡（北のランドマーク）
// =======================================================================
P("04_Church", "R", "NF_Struc_Church01B", 117f, -36f, 180f, 1.2f);
P("04_Church", "R", "NF_Struc_Arch_01A", 115f, -66f, 0f, 2.0f);
P("04_Church", "R", "NF_Prop_Statue_01B", 105f, -57f, 155f, 2.0f);
P("04_Church", "R", "NF_Prop_Statue_01B", 125f, -57f, 205f, 2.0f);
for (int i = 0; i < 4; i++)
{
    float pz = -63f + i * 5.5f;
    P("04_Church", "R", "NF_Prop_StonePillar_04A", 104f, pz, 0f, 1.9f);
    P("04_Church", "R", "NF_Prop_StonePillar_04A", 126f, pz, 0f, 1.9f);
}
PS("04_Church", "X", "Slab", 115f, -56f, 0f, 2.6f, 0.35f);
PS("04_Church", "X", "Slab", 115f, -52f, 0f, 2.6f, 0.3f);
PS("04_Church", "X", "Slab", 115f, -48f, 0f, 2.6f, 0.3f);
P("04_Church", "R", "NF_Prop_BrokenWall_05", 95f, -32f, 30f, 1.8f);
P("04_Church", "R", "NF_Prop_BrokenWall_03", 141f, -30f, -25f, 1.8f);
P("04_Church", "R", "NF_Prop_Altar_01", 117f, -60f, 0f, 1.8f);
P("04_Church", "M", "NF_Prop_Lantern_01", 108f, -62f, 0f, 4.0f);
P("04_Church", "M", "NF_Prop_Lantern_01", 122f, -62f, 0f, 4.0f);
P("04_Church", "R", "NF_Struc_Obelisk_01b", 92f, -68f, 0f, 1.25f);
P("04_Church", "R", "NF_Struc_Obelisk_01", 84f, -56f, 70f, 1.4f);

// =======================================================================
// 05_Graveyard  教会の東の墓地
// =======================================================================
string[] tombs = { "NF_Prop_Tombstone_01", "NF_Prop_Tombstone_01A", "NF_Prop_Tombstone_02", "NF_Prop_Tombstone_02A" };
for (int r = 0; r < 3; r++)
{
    for (int c = 0; c < 4; c++)
    {
        float gx = 141f + c * 6.5f + Rnd(-1.2f, 1.2f);
        float gz = -68f + r * 7f + Rnd(-1.2f, 1.2f);
        PS("05_Graveyard", "R", tombs[(r + c) % tombs.Length], gx, gz, Rnd(-14f, 14f), Rnd(1.8f, 2.2f), 0.1f);
    }
}
string[] gstones = { "NF_Prop_Gravestone_01", "NF_Prop_Gravestone_02", "NF_Prop_Gravestone_03", "NF_Prop_Gravestone_04" };
for (int i = 0; i < 8; i++)
{
    PS("05_Graveyard", "M", gstones[i % 4], Rnd(138f, 166f), Rnd(-72f, -44f), Rnd(-20f, 20f), Rnd(3.0f, 3.8f), 0.05f);
}
P("05_Graveyard", "R", "NF_Prop_Statue_01", 152f, -44f, 175f, 2.0f);
P("05_Graveyard", "R", "NF_Prop_BrokenWall_05", 166f, -62f, 35f, 1.7f);
P("05_Graveyard", "R", "NF_Prop_Tombstone_01A", 135f, -60f, 8f, 2.0f);
P("05_Graveyard", "M", "NF_Prop_Lantern_01", 148f, -63f, 0f, 4.0f);
P("05_Graveyard", "M", "NF_Prop_Crystals_02", 160f, -50f, 30f, 4.5f);

// =======================================================================
// 06_Lake  ふたつの池・桟橋・小舟
// =======================================================================
// 水面。面を分けると重なった部分が濃くなるので、一枚で両方の池を覆う。
// 岸より高い地面が勝手に隠してくれるので、はみ出しは気にしなくてよい
var water = PY("06_Lake", "X", "Water_Plane", 39f, -105f, 0f, 1f, WATER - 1f);
if (water != null) water.transform.localScale = new Vector3(6.6f, 1f, 10.2f);

// 桟橋と舟（池B・東岸）
PY("06_Lake", "M", "NF_Prop_WoodenPier_01", 60f, -82f, 90f, 2.5f, WATER + 0.6f);
PY("06_Lake", "M", "NF_Prop_Boat_01", 52f, -88f, 55f, 2.6f, WATER - 0.15f);
PY("06_Lake", "M", "NF_Prop_BoatOar_01", 59f, -79f, 20f, 3.0f, WATER + 2.0f);
P("06_Lake", "M", "NF_Prop_Barrel_02", 63f, -79f, 0f, 2.6f);
P("06_Lake", "M", "NF_Prop_WoodenBox_01", 64.5f, -85f, 25f, 2.4f);
P("06_Lake", "M", "NF_Prop_RopeBundle_01", 62f, -85.5f, 10f, 3.0f);
P("06_Lake", "M", "NF_Prop_Lantern_01", 63.5f, -82f, 0f, 4.0f);
P("06_Lake", "M", "NF_Prop_WoodenSign_05", 68f, -88f, 120f, 3.0f);

// 池A・南岸に打ち上げられた舟
PY("06_Lake", "M", "NF_Prop_CrashedBoat_01", 30f, -145f, 12f, 2.8f, WATER + 0.1f);
P("06_Lake", "M", "NF_Prop_BoatOar_01", 35f, -147f, 70f, 3.0f);

// 水路に架かる板橋
P("06_Lake", "M", "NF_Prop_Bridge_01", 39f, -105f, 107f, 3.9f);

// 壊れた石橋（池Aを跨ぐ）
P("06_Lake", "R", "NF_Struc_Bridge_01", 30f, -137f, 90f, 1.25f);

// 岸辺の岩
string[] flatRocks = { "NF_Prop_Rock_16", "NF_Prop_Rock_17", "NF_Prop_Rock_19", "NF_Prop_Rock_20", "NF_Prop_Rock_09" };
for (int i = 0; i < 16; i++)
{
    float a = Rnd(0f, 360f);
    bool poolA = i % 2 == 0;
    float cx = poolA ? 32f : 46f, cz = poolA ? -128f : -82f;
    float rad = (poolA ? 17f : 15.5f) + Rnd(-1.5f, 3.5f);
    float px = cx + Mathf.Sin(a * Mathf.Deg2Rad) * rad;
    float pz = cz + Mathf.Cos(a * Mathf.Deg2Rad) * rad;
    PS("06_Lake", "M", flatRocks[i % flatRocks.Length], px, pz, Rnd(0f, 360f), Rnd(1.8f, 2.6f), Rnd(0.2f, 0.7f));
}
// 北岸のアーチ岩（ランドマーク）
PS("06_Lake", "M", "NF_Prop_Rock_22", 44f, -62f, 105f, 2.0f, 0.4f);
PS("06_Lake", "M", "NF_Prop_Rock_23", 24f, -100f, 25f, 1.8f, 0.5f);
PS("06_Lake", "M", "NF_Prop_Rock_12", 18f, -140f, 60f, 2.2f, 0.6f);

// =======================================================================
// 07_WindmillHill  風車の丘（3D Pixel Art の区画）
// =======================================================================
P("07_WindmillHill", "X", "Windmill", 48f, -40f, 200f, 5.0f);
P("07_WindmillHill", "X", "Windmill_Broken", 28f, -30f, 340f, 4.2f);
P("07_WindmillHill", "X", "House", 61f, -50f, 205f, 5.0f);
P("07_WindmillHill", "X", "House", 34f, -50f, 145f, 4.6f);
P("07_WindmillHill", "X", "Light_Post", 54f, -46f, 0f, 0.6f);
P("07_WindmillHill", "X", "Arch_Large", 79f, -66f, -35f, 1.3f);
P("07_WindmillHill", "X", "Arch_Small", 27f, -57f, 25f, 1.2f);
PS("07_WindmillHill", "X", "Slab", 60f, -57f, 15f, 2.4f, 0.35f);
PS("07_WindmillHill", "X", "Slab", 55f, -52f, -10f, 2.4f, 0.35f);
PS("07_WindmillHill", "X", "Stone_1", 41f, -33f, 30f, 2.6f, 0.4f);
PS("07_WindmillHill", "X", "Stone_2", 57f, -33f, 200f, 2.4f, 0.5f);
PS("07_WindmillHill", "X", "Stone_1", 38f, -58f, 100f, 2.2f, 0.4f);
for (int i = 0; i < 7; i++)
{
    float a = i * 51f + 20f, rad = Rnd(16f, 30f);
    P("07_WindmillHill", "X", "Tree", 46f + Mathf.Sin(a * Mathf.Deg2Rad) * rad,
      -40f + Mathf.Cos(a * Mathf.Deg2Rad) * rad, Rnd(0f, 360f), Rnd(1.2f, 1.7f));
}

// =======================================================================
// 08_EastForest  東の森に埋もれた大聖堂跡
// =======================================================================
P("08_EastForest", "R", "NF_Struc_Church01", 163f, -140f, 25f, 1.1f);
PS("08_EastForest", "M", "NF_Prop_Rock_24", 180f, -163f, 40f, 1.4f, 1.0f);
P("08_EastForest", "M", "NF_Prop_Cage", 150f, -122f, 20f, 2.5f);
P("08_EastForest", "M", "NF_Prop_WoodenBox_01", 153f, -124f, 40f, 2.4f);
P("08_EastForest", "M", "NF_Prop_Chest_01", 171f, -117f, 200f, 3.0f);
P("08_EastForest", "M", "NF_Prop_CoinPouch_01", 173f, -119f, 30f, 4.0f);
P("08_EastForest", "M", "NF_Prop_Crown_02", 169f, -119f, 15f, 4.0f);
P("08_EastForest", "M", "NF_Prop_Crystals_01", 176f, -104f, 0f, 5.0f);
P("08_EastForest", "M", "NF_Prop_Crystals_02", 178f, -106f, 60f, 5.0f);
P("08_EastForest", "M", "NF_Prop_Crystals_03", 174f, -107f, 130f, 5.0f);
P("08_EastForest", "M", "NF_Prop_Scroll_01", 160f, -136f, 20f, 4.0f);
P("08_EastForest", "R", "NF_Prop_BrokenWall_06", 148f, -150f, 200f, 1.5f);
P("08_EastForest", "R", "NF_Prop_StonePillar_03A", 156f, -160f, 0f, 2.0f);
P("08_EastForest", "R", "NF_Prop_StonePillar_05", 162f, -156f, 40f, 2.0f);
P("08_EastForest", "M", "NF_Prop_WoodenSign_01", 140f, -132f, 250f, 3.0f);

// =======================================================================
// 09_Aqueduct  広場の東を走る水道橋
// =======================================================================
P("09_Aqueduct", "R", "NF_Struc_Aqueduct_01", 146f, -95f, 0f, 1.5f);
P("09_Aqueduct", "R", "NF_Prop_BrokenWall_04", 143f, -122f, 15f, 1.7f);
P("09_Aqueduct", "R", "NF_Prop_BrokenWall_03", 150f, -70f, 200f, 1.7f);
P("09_Aqueduct", "R", "NF_Prop_StonePillar_01A", 137f, -108f, 0f, 2.0f);
P("09_Aqueduct", "R", "NF_Prop_StonePillar_02", 139f, -84f, 60f, 2.0f);
P("09_Aqueduct", "R", "NF_Prop_Slab_01", 141f, -100f, 20f, 2.0f);

return $"placed={placed} missing=[{string.Join(",", missing)}]";
