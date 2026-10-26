using UnityEngine;
using UnityEditor;

// パックの水マテリアルは彩度が高すぎて、この森の色味から浮く。
// パック側を書き換えると他のデモシーンに影響するので、コピーを作って差し替える。
const string dst = "Assets/Project/Field/M_StageWater.mat";
var mat = AssetDatabase.LoadAssetAtPath<Material>(dst);
if (mat == null)
{
    AssetDatabase.CopyAsset("Assets/3DPixelArtEnvironment/Materials/Water.mat", dst);
    AssetDatabase.ImportAsset(dst);
    mat = AssetDatabase.LoadAssetAtPath<Material>(dst);
}

mat.SetColor("_Color", new Color(0.46f, 0.70f, 0.74f, 0.55f));  // 落ち着いた青緑
mat.SetFloat("_FoamAmount", 0.30f);
mat.SetFloat("_FoamScale", 220f);      // 面を拡大して置くので、泡の幅を細くする
mat.SetFloat("_TintStrength", 0.02f);   // 深さで暗くしない（平坦な塗りに合わせる）
mat.SetFloat("_MinimumTint", 0.75f);   // 深いところが黒く潰れないようにする
mat.SetFloat("_Brightness", 0.40f);
EditorUtility.SetDirty(mat);

int n = 0;
var lake = GameObject.Find("Stage_ForestRuins").transform.Find("06_Lake");
foreach (Transform t in lake)
{
    if (!t.name.StartsWith("Water_Plane")) continue;
    foreach (var r in t.GetComponentsInChildren<Renderer>(true)) { r.sharedMaterial = mat; n++; }
}
AssetDatabase.SaveAssets();
return $"water material applied to {n} renderers";
