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
    // �ÓI�ɂ���̂�Y��Ȃ�
    private static void ShowWindow()
    {
        ChangePixelizeShader window = GetWindow<ChangePixelizeShader>();
    }

    private void OnGUI()
    {
        _path = EditorGUILayout.TextField("path", _path);
        _targetMaterial = (Material)EditorGUILayout.ObjectField("�����Ώۂ�Material", _targetMaterial, typeof(Material), false);
        _newShader = (Shader)EditorGUILayout.ObjectField("�ύX��̃V�F�[�_�[", _newShader, typeof(Shader), false);

        if (GUILayout.Button("ChangeShader"))
        {
            _resultStr = "";
            ChangeShader();
        }

        GUILayout.Label(_resultStr);
    }

    private void ChangeShader()
    {
        // ���O���u�������悪null�Ȃ烍�O�ɏo���ďI��
        if (string.IsNullOrEmpty(_path) || (System.IO.Directory.Exists(_path) == false))
        {
            Debug.LogWarning("�t�H���_�p�X�����݂��Ă��Ȃ����t�H���_�p�X�̎w�肪�s�K�؂ł�");
            return;
        }

        // �ύX�O�̃V�F�[�_�[�ɐݒ肳��Ă���e�N�X�`����ۑ�
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

                    Debug.Log($"�v���p�e�B��: {propName}, �e�N�X�`��: {savedTextures[propName]}");
                }
            }
        }

        // �V�F�[�_�[��ύX
        _targetMaterial.shader = _newShader;

        // �V�����V�F�[�_�[�ɑΉ�����v���p�e�B�Ƀe�N�X�`�����Đݒ�(ProPixelizer�p�ɐݒ肵�Ă���)
        int newPropCount = ShaderUtil.GetPropertyCount(_newShader);
        for (int i = 0; i < newPropCount; i++)
        {
            if (ShaderUtil.GetPropertyType(_newShader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
            {
                // �v���p�e�B�Đݒ�i����̓e�N�X�`���̂݁j
                string propName = ShaderUtil.GetPropertyName(_newShader, i);
                if(propName == "_Albedo")
                {
                    _targetMaterial.SetTexture(propName, savedTextures["_BaseMap"]);
                    Debug.Log($"�e�N�X�`���Đݒ�: {propName}");
                }
                else if(propName == "_NormalMap")
                {
                    _targetMaterial.SetTexture(propName, savedTextures["_BumpMap"]);
                    Debug.Log($"�e�N�X�`���Đݒ�: {propName}");
                }
                //else if(propName == "_EmissionMap")
                //{
                //    _targetMaterial.SetTexture(propName, savedTextures["_EmissionMap"]);
                //}
            }
        }

        // �ύX���e��ۑ�����
        EditorUtility.SetDirty(_targetMaterial);

        _resultStr = "�������������܂���";
    }
}
