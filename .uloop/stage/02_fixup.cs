using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Text;

var terrainGo = GameObject.Find("Terrain_(0.00, 0.00, -200.00)");
var terrain = terrainGo.GetComponent<Terrain>();
var sb = new StringBuilder();

float Ground(float x, float z)
{
    return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
}

// 地形を持ち上げたので、既存オブジェクトを新しい地面の高さに載せ直す
void Snap(GameObject go, float lift)
{
    if (go == null) return;
    var p = go.transform.position;
    float y = Ground(p.x, p.z) + lift;
    go.transform.position = new Vector3(p.x, y, p.z);
    sb.AppendLine($"snap {go.name} -> y={y:F2}");
}

Snap(GameObject.Find("Player"), 1.6f);
Snap(GameObject.Find("NF_Prop_Bedding_001"), 0f);

var tp = terrainGo.transform;
for (int i = 0; i < tp.childCount; i++) Snap(tp.GetChild(i).gameObject, 0f);

// ステージのルート
var root = GameObject.Find("Stage_ForestRuins");
if (root == null)
{
    root = new GameObject("Stage_ForestRuins");
    Undo.RegisterCreatedObjectUndo(root, "Stage");
    sb.AppendLine("created Stage_ForestRuins");
}
root.transform.position = Vector3.zero;

return sb.ToString();
