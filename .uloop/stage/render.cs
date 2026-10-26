using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

// param0: "top" | "iso" | "cam"  param1: 出力パス
// iso/cam 用: param2=注視点x, param3=注視点z, param4=ortho size
string mode = parameters["param0"].ToString();
string outPath = parameters["param1"].ToString();
float fx = parameters.ContainsKey("param2") ? System.Convert.ToSingle(parameters["param2"]) : 100f;
float fz = parameters.ContainsKey("param3") ? System.Convert.ToSingle(parameters["param3"]) : -100f;
float size = parameters.ContainsKey("param4") ? System.Convert.ToSingle(parameters["param4"]) : 100f;

int w = 1280, h = mode == "top" ? 1280 : 720;

// PixelCamera の表示用クアッドがカメラの手前に立っていて視界を塞ぐので、撮影の間だけ隠す
var display = GameObject.Find("PixelCameraSystem/PixelCamera/UpscaledDisplay");
bool displayWas = display != null && display.activeSelf;
if (display != null) display.SetActive(false);

var camGo = new GameObject("__stage_render_cam");
camGo.hideFlags = HideFlags.HideAndDontSave;
var cam = camGo.AddComponent<Camera>();
cam.orthographic = true;
cam.orthographicSize = size;
cam.clearFlags = CameraClearFlags.SolidColor;
cam.backgroundColor = new Color(0.35f, 0.45f, 0.55f);
cam.nearClipPlane = 1f;
cam.farClipPlane = 1200f;

if (mode == "top")
{
    camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    camGo.transform.position = new Vector3(fx, 300f, fz);
}
else if (mode == "game")
{
    // ゲーム中のピクセルカメラとほぼ同じ画角。fx,fz はプレイヤーの位置
    var t = Terrain.activeTerrain;
    float gy = t != null ? t.SampleHeight(new Vector3(fx, 0f, fz)) + t.transform.position.y : 6f;
    camGo.transform.rotation = Quaternion.Euler(27.5f, 0f, 0f);
    camGo.transform.position = new Vector3(fx, gy + 26f, fz - 35f);
    cam.orthographicSize = 28f;
}
else
{
    camGo.transform.rotation = Quaternion.Euler(27.5f, 0f, 0f);
    camGo.transform.position = new Vector3(fx, 0f, fz) - camGo.transform.forward * 300f + Vector3.up * 0f;
}

var rt = new RenderTexture(w, h, 24);
cam.targetTexture = rt;
cam.Render();
var prev = RenderTexture.active;
RenderTexture.active = rt;
var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
tex.Apply();
RenderTexture.active = prev;
cam.targetTexture = null;
System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(camGo);
if (display != null) display.SetActive(displayWas);
return "rendered " + outPath;
