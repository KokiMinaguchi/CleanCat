using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;


public class ChangePixelizeShader : EditorWindow
{
    private string _path = "";
    private Material _targetMaterial;
    private Shader _newShader;

    private string _resultStr;

    [MenuItem("MyEditorExtension/ChangeShader")]
    // 静的にするのを忘れない
    private static void ShowWindow()
    {
        ChangePixelizeShader window = GetWindow<ChangePixelizeShader>();
    }

    private void OnGUI()
    {
        _path = EditorGUILayout.TextField("path", _path);
        _targetMaterial = (Material)EditorGUILayout.ObjectField("検索対象のMaterial", _targetMaterial, typeof(Material), false);
        _newShader = (Shader)EditorGUILayout.ObjectField("変更後のシェーダー", _newShader, typeof(Shader), false);

        if (GUILayout.Button("ChangeShader"))
        {
            _resultStr = "";
            ChangeShader();
        }

        GUILayout.Label(_resultStr);
    }

    private void ChangeShader()
    {
        // 名前か置き換え先がnullならログに出して終了
        if (string.IsNullOrEmpty(_path) || (System.IO.Directory.Exists(_path) == false))
        {
            Debug.LogWarning("フォルダパスが存在していないかフォルダパスの指定が不適切です");
            return;
        }

        // 変更前のシェーダーに設定されているテクスチャを保存
        var oldShader = _targetMaterial.shader;
        var savedTextures = new Dictionary<string, Texture>();

        int oldPropCount = ShaderUtil.GetPropertyCount(oldShader);
        for (int i = 0; i < oldPropCount; i++)
        {
            if (ShaderUtil.GetPropertyType(oldShader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
            {
                string propName = ShaderUtil.GetPropertyName(oldShader, i);
                if (_targetMaterial.HasProperty(propName))
                {
                    savedTextures[propName] = _targetMaterial.GetTexture(propName);

                    Debug.Log($"プロパティ名: {propName}, テクスチャ: {savedTextures[propName]}");
                }
            }
        }

        // シェーダーを変更
        _targetMaterial.shader = _newShader;

        // 新しいシェーダーに対応するプロパティにテクスチャを再設定(ProPixelizer用に設定している)
        int newPropCount = ShaderUtil.GetPropertyCount(_newShader);
        for (int i = 0; i < newPropCount; i++)
        {
            if (ShaderUtil.GetPropertyType(_newShader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
            {
                // プロパティ再設定（今回はテクスチャのみ）
                string propName = ShaderUtil.GetPropertyName(_newShader, i);
                if(propName == "_Albedo")
                {
                    _targetMaterial.SetTexture(propName, savedTextures["_BaseMap"]);
                    Debug.Log($"テクスチャ再設定: {propName}");
                }
                else if(propName == "_NormalMap")
                {
                    _targetMaterial.SetTexture(propName, savedTextures["_BumpMap"]);
                    Debug.Log($"テクスチャ再設定: {propName}");
                }
                //else if(propName == "_EmissionMap")
                //{
                //    _targetMaterial.SetTexture(propName, savedTextures["_EmissionMap"]);
                //}
            }
        }

        // 変更内容を保存する
        EditorUtility.SetDirty(_targetMaterial);

        _resultStr = "処理が完了しました";
    }
}
