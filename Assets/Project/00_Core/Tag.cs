using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SleepingAnimals.Core
{
    /// <summary>
    /// タグ
    /// </summary>
    [Serializable]
    public struct Tag
    {
        // タグ名
        [SerializeField] private string _tagName;

        // タグ名のプロパティ
        public string Name
        {
            get => _tagName;
            set => _tagName = value;
        }

        // タグ付けされているかどうか
        public bool IsTagged => !string.IsNullOrEmpty(_tagName) && _tagName != "Untagged";

        // タグ名の比較
        public static bool operator ==(Tag tag, string tagName) => tag._tagName == tagName;
        public static bool operator !=(Tag tag, string tagName) => tag._tagName != tagName;
        public bool Equals(Tag other) => _tagName == other._tagName;
        public override bool Equals(object obj) => obj is Tag other && Equals(other);

        // ハッシュコードの取得
        public override int GetHashCode() => (_tagName != null ? _tagName.GetHashCode() : 0);

        // 文字列への変換
        public override string ToString() => _tagName;

#if UNITY_EDITOR
        // タグ名のプロパティを表示するためのPropertyDrawer
        [CustomPropertyDrawer(typeof(Tag))]
        public class TagDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                var tagNameProperty = property.FindPropertyRelative("_tagName");

                // タグフィールドを表示
                var tag = EditorGUI.TagField(position, label, tagNameProperty.stringValue);

                // タグ名を反映
                tagNameProperty.stringValue = tag;
            }
        }
#endif
    }
}
