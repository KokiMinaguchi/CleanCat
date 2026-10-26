using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

var terrainGo = GameObject.Find("Terrain_(0.00, 0.00, -200.00)");
var terrain = terrainGo.GetComponent<Terrain>();
var td = terrain.terrainData;
Vector3 origin = terrainGo.transform.position;      // (0,0,-200)
Vector3 tsize = td.size;                            // (200,100,200)
int hres = td.heightmapResolution;                  // 513

// ---- 地形の定義 ---------------------------------------------------------
// 世界座標 y をそのまま返す解析関数。ノイズと平坦化はこの後で足す。
float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

float Circle(float x, float z, float cx, float cz, float r)
{
    float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
    return Smooth(1f - d / r);
}

float RoundedBox(float x, float z, float cx, float cz, float hx, float hz, float feather)
{
    float dx = Mathf.Abs(x - cx) - hx;
    float dz = Mathf.Abs(z - cz) - hz;
    float d = Mathf.Max(dx, dz);
    return Smooth(1f - d / feather);
}

float H0(float x, float z)
{
    float h = 6.0f;                                                  // 基準地面

    // 外周を持ち上げてステージを囲む（自然な壁）
    float border = Mathf.Min(Mathf.Min(x, 200f - x), Mathf.Min(z + 200f, -z));
    h += 13f * Mathf.Pow(Mathf.Clamp01((30f - border) / 30f), 2f);

    // 北西の風車の丘
    h += 11f * Circle(x, z, 46f, -40f, 34f);

    // 東の森の尾根
    h += 7f * Circle(x, z, 180f, -100f, 52f);

    // 北の教会テラス
    h += 3.4f * RoundedBox(x, z, 115f, -45f, 26f, 24f, 9f);

    // 湖（ひょうたん型。二つの池を細い水路がつなぐ）
    // pow で底を広く・岸を急にして、水面がくぼみをしっかり埋めるようにする
    float lake = Mathf.Max(Circle(x, z, 32f, -128f, 24f), Circle(x, z, 46f, -82f, 22f));
    h -= 5.2f * Mathf.Pow(lake, 0.55f);

    // 池をつなぐ水路。ここに板橋を架ける
    var ca = new Vector2(34f, -122f); var cb = new Vector2(44f, -90f);
    var ab = cb - ca;
    float ct = Mathf.Clamp01(Vector2.Dot(new Vector2(x, z) - ca, ab) / ab.sqrMagnitude);
    float cd = Vector2.Distance(new Vector2(x, z), ca + ab * ct);
    h -= 2.9f * Smooth(1f - cd / 7.5f);

    return h;
}

// ---- 平坦化ゾーン -------------------------------------------------------
var flatCircles = new List<Vector4>   // x, z, 半径, ぼかし
{
    new Vector4(100f, -97f, 22f, 10f),   // 遺跡広場
    new Vector4( 97f, -155f, 22f, 10f),  // キャンプ
    new Vector4( 48f, -42f, 13f, 8f),    // 風車の丘の頂上
    new Vector4(150f, -58f, 16f, 9f),    // 墓地
};

var paths = new List<Vector2[]>
{
    // メイン動線：キャンプ → 広場 → 教会
    new [] { new Vector2(108,-178), new Vector2(103,-158), new Vector2(100,-140),
             new Vector2(100,-120), new Vector2(100,-104), new Vector2(101,-88),
             new Vector2(106,-72),  new Vector2(112,-58),  new Vector2(115,-50) },
    // 湖への枝道
    new [] { new Vector2(92,-110), new Vector2(76,-112), new Vector2(64,-106), new Vector2(56,-101) },
    // 風車の丘への枝道
    new [] { new Vector2(96,-88), new Vector2(80,-74), new Vector2(66,-62),
             new Vector2(56,-52), new Vector2(50,-44) },
    // 墓地への枝道
    new [] { new Vector2(118,-56), new Vector2(132,-56), new Vector2(145,-57) },
};
float pathHalf = 5.5f, pathFeather = 5f;

// 点 p から折れ線への最近点と距離
Vector3 NearestOnPath(Vector2[] pts, float x, float z)
{
    var p = new Vector2(x, z);
    float best = float.MaxValue; Vector2 bestPt = pts[0];
    for (int i = 0; i < pts.Length - 1; i++)
    {
        Vector2 a = pts[i], b = pts[i + 1];
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
        Vector2 q = a + ab * t;
        float d = Vector2.Distance(p, q);
        if (d < best) { best = d; bestPt = q; }
    }
    return new Vector3(bestPt.x, bestPt.y, best);
}

// ---- ハイトマップ生成 ---------------------------------------------------
var heights = new float[hres, hres];
var dirtMask = new float[hres, hres];   // 後でテクスチャ塗りに使う

for (int j = 0; j < hres; j++)
{
    float z = origin.z + tsize.z * j / (hres - 1);
    for (int i = 0; i < hres; i++)
    {
        float x = origin.x + tsize.x * i / (hres - 1);

        float h = H0(x, z);

        // 自然な起伏
        float n = (Mathf.PerlinNoise(x * 0.035f + 11.3f, z * 0.035f + 7.1f) - 0.5f) * 1.6f
                + (Mathf.PerlinNoise(x * 0.11f + 3.7f, z * 0.11f + 19.4f) - 0.5f) * 0.5f;

        // 平坦化
        float flatW = 0f; float target = h;
        foreach (var c in flatCircles)
        {
            float d = Mathf.Sqrt((x - c.x) * (x - c.x) + (z - c.y) * (z - c.y));
            float w = Smooth((c.z + c.w - d) / c.w);
            if (w > flatW) { flatW = w; target = H0(c.x, c.y); }
        }

        float pathW = 0f;
        foreach (var pts in paths)
        {
            var near = NearestOnPath(pts, x, z);
            float w = Smooth((pathHalf + pathFeather - near.z) / pathFeather);
            if (w > pathW) { pathW = w; }
            if (w > flatW) { flatW = w; target = H0(near.x, near.y); }
        }

        h = Mathf.Lerp(h + n, target, flatW);

        heights[j, i] = Mathf.Clamp01(h / tsize.y);
        dirtMask[j, i] = pathW;
    }
}

td.SetHeights(0, 0, heights);

// ---- テクスチャ（草／土）------------------------------------------------
var grassLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/3DPixelArtEnvironment/Demo/Demo_Grass.terrainlayer");
var dirtLayer  = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/3DPixelArtEnvironment/Demo/Demo_Dirt.terrainlayer");
td.terrainLayers = new[] { grassLayer, dirtLayer };

int ares = td.alphamapResolution;
var alpha = new float[ares, ares, 2];
for (int j = 0; j < ares; j++)
{
    float z = origin.z + tsize.z * j / (float)(ares - 1);
    for (int i = 0; i < ares; i++)
    {
        float x = origin.x + tsize.x * i / (float)(ares - 1);

        // 道
        float dirt = 0f;
        foreach (var pts in paths)
        {
            var near = NearestOnPath(pts, x, z);
            dirt = Mathf.Max(dirt, Smooth((pathHalf + 2.5f - near.z) / 3.5f));
        }
        // 広場・キャンプ・墓地は踏み固められた土
        dirt = Mathf.Max(dirt, 0.85f * Circle(x, z, 100f, -97f, 26f));
        dirt = Mathf.Max(dirt, 0.65f * Circle(x, z, 97f, -155f, 24f));
        dirt = Mathf.Max(dirt, 0.55f * Circle(x, z, 150f, -58f, 17f));
        dirt = Mathf.Max(dirt, 0.60f * Circle(x, z, 115f, -45f, 26f));
        // 湖の岸辺
        float lakeT = Mathf.Max(Circle(x, z, 32f, -128f, 28f), Circle(x, z, 46f, -82f, 26f));
        float lakeIn = Mathf.Max(Circle(x, z, 32f, -128f, 21f), Circle(x, z, 46f, -82f, 19f));
        dirt = Mathf.Max(dirt, Mathf.Clamp01((lakeT - lakeIn) * 3f));

        // 縁を少しぼかす
        dirt = Mathf.Clamp01(dirt + (Mathf.PerlinNoise(x * 0.4f, z * 0.4f) - 0.5f) * 0.25f);

        alpha[j, i, 0] = 1f - dirt;
        alpha[j, i, 1] = dirt;
    }
}
td.SetAlphamaps(0, 0, alpha);
EditorUtility.SetDirty(td);

return $"sculpt done. sample heights: camp={terrain.SampleHeight(new Vector3(100,0,-155)):F2} " +
       $"plaza={terrain.SampleHeight(new Vector3(100,0,-97)):F2} " +
       $"church={terrain.SampleHeight(new Vector3(115,0,-45)):F2} " +
       $"hill={terrain.SampleHeight(new Vector3(48,0,-42)):F2} " +
       $"lakeA={terrain.SampleHeight(new Vector3(32,0,-128)):F2} " +
       $"lakeB={terrain.SampleHeight(new Vector3(46,0,-82)):F2} " +
       $"waist={terrain.SampleHeight(new Vector3(39,0,-105)):F2} " +
       $"spawn={terrain.SampleHeight(new Vector3(108.8f,0,-168.8f)):F2}";
