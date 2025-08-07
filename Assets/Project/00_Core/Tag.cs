using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SleepingAnimals.Core
{
    /// <summary>
    /// �^�O
    /// </summary>
    [Serializable]
    public struct Tag
    {
        // �^�O��
        [SerializeField] private string _tagName;

        // �^�O���̃v���p�e�B
        public string Name
        {
            get => _tagName;
            set => _tagName = value;
        }

        // �^�O�t������Ă��邩�ǂ���
        public bool IsTagged => !string.IsNullOrEmpty(_tagName) && _tagName != "Untagged";

        // �^�O���̔�r
        public static bool operator ==(Tag tag, string tagName) => tag._tagName == tagName;
        public static bool operator !=(Tag tag, string tagName) => tag._tagName != tagName;
        public bool Equals(Tag other) => _tagName == other._tagName;
        public override bool Equals(object obj) => obj is Tag other && Equals(other);

        // �n�b�V���R�[�h�̎擾
        public override int GetHashCode() => (_tagName != null ? _tagName.GetHashCode() : 0);

        // ������ւ̕ϊ�
        public override string ToString() => _tagName;

#if UNITY_EDITOR
        // �^�O���̃v���p�e�B��\�����邽�߂�PropertyDrawer
        [CustomPropertyDrawer(typeof(Tag))]
        public class TagDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                var tagNameProperty = property.FindPropertyRelative("_tagName");

                // �^�O�t�B�[���h��\��
                var tag = EditorGUI.TagField(position, label, tagNameProperty.stringValue);

                // �^�O���𔽉f
                tagNameProperty.stringValue = tag;
            }
        }
#endif
    }
}
