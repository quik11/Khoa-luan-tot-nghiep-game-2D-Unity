using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Vẽ Inspector như mặc định, chỉ thay tên ô bằng tên trong [TenHienThi("...")] nếu có.
// Script nào có Editor riêng (của Unity hoặc gói khác) thì Unity vẫn ưu tiên Editor riêng đó.
[CustomEditor(typeof(MonoBehaviour), true)]
[CanEditMultipleObjects]
public class TenHienThiEditor : Editor
{
    private static readonly Dictionary<Type, Dictionary<string, string>> cache = new Dictionary<Type, Dictionary<string, string>>();

    public override void OnInspectorGUI()
    {
        Dictionary<string, string> names = GetNames(target.GetType());
        if (names.Count == 0)
        {
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();
        SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (prop.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(prop);
                continue;
            }

            if (names.TryGetValue(prop.name, out string label))
                EditorGUILayout.PropertyField(prop, new GUIContent(label, prop.tooltip), true);
            else
                EditorGUILayout.PropertyField(prop, true);
        }
        serializedObject.ApplyModifiedProperties();
    }

    // đọc 1 lần cho mỗi loại script rồi nhớ lại, khỏi dò lại mỗi lần vẽ
    public static Dictionary<string, string> GetNames(Type type)
    {
        if (cache.TryGetValue(type, out Dictionary<string, string> names)) return names;

        names = new Dictionary<string, string>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = type; t != null && t != typeof(MonoBehaviour) && t != typeof(ScriptableObject) && t != typeof(StateMachineBehaviour); t = t.BaseType)
        {
            foreach (FieldInfo field in t.GetFields(flags))
            {
                TenHienThiAttribute attr = field.GetCustomAttribute<TenHienThiAttribute>();
                if (attr != null && !names.ContainsKey(field.Name)) names[field.Name] = attr.ten;
            }
        }
        cache[type] = names;
        return names;
    }
}

// Cùng cách vẽ cho các script gắn vào state trong Animator
[CustomEditor(typeof(StateMachineBehaviour), true)]
[CanEditMultipleObjects]
public class TenHienThiStateEditor : TenHienThiEditor { }

// và cho bộ nền từng màn (ScriptableObject)
[CustomEditor(typeof(BackgroundTheme))]
public class TenHienThiNenEditor : TenHienThiEditor { }
