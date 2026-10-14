using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Shared IMGUI helpers for Narrative Linker views.
    /// Centralizes safe Begin/End wrappers, toolbars, lists, asset utils,
    /// tolerant property lookup and safe object labels.
    /// </summary>
    internal static class NarrativeLinkerGUI
    {
        // ---------- Styles ----------
        private static GUIStyle _header;
        public static GUIStyle Header => _header ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };

        // ---------- Safe vertical ----------
        // Bezpieczny box z opcjami layoutu (np. GUILayout.Width)
        public static void VerticalBox(Action body, params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginVertical("box", options);
            try { body?.Invoke(); }
            finally { EditorGUILayout.EndVertical(); }
        }


        // ---------- Safe horizontal ----------
        public static void Horizontal(Action body, GUIStyle style = null)
        {
            if (style == null) EditorGUILayout.BeginHorizontal();
            else EditorGUILayout.BeginHorizontal(style);
            try { body?.Invoke(); }
            finally { EditorGUILayout.EndHorizontal(); }
        }

        // ---------- Safe scroll ----------
        public static void Scroll(ref Vector2 scroll, Action body)
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            try { body?.Invoke(); }
            finally { EditorGUILayout.EndScrollView(); }
        }

        // ---------- Foldout ----------
        public static bool Foldout(ref bool state, string title)
        {
            state = EditorGUILayout.Foldout(state, title, true);
            return state;
        }

        // ---------- Toolbar row ----------
        public static void Toolbar(params (string label, Action onClick)[] buttons)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            try
            {
                foreach (var (label, onClick) in buttons)
                {
                    if (GUILayout.Toggle(false, label, EditorStyles.toolbarButton))
                        onClick?.Invoke();
                }
                GUILayout.FlexibleSpace();
            }
            finally { EditorGUILayout.EndHorizontal(); }
        }

        // ---------- Reorderable list: objects ----------
        public static ReorderableList ObjList(SerializedObject so, SerializedProperty prop, string label, Type elemType)
        {
            var rl = new ReorderableList(so, prop, true, true, true, true);
            rl.drawHeaderCallback = r => EditorGUI.LabelField(r, label);
            rl.drawElementCallback = (r, i, a, f) =>
            {
                var el = prop.GetArrayElementAtIndex(i);
                el.objectReferenceValue = EditorGUI.ObjectField(r, el.objectReferenceValue, elemType, false);
            };
            return rl;
        }

        // ---------- Reorderable list: strings ----------
        public static ReorderableList StringList(SerializedObject so, SerializedProperty prop, string label)
        {
            var rl = new ReorderableList(so, prop, true, true, true, true);
            rl.drawHeaderCallback = r => EditorGUI.LabelField(r, label);
            rl.drawElementCallback = (r, i, a, f) =>
            {
                var el = prop.GetArrayElementAtIndex(i);
                el.stringValue = EditorGUI.TextField(r, el.stringValue);
            };
            return rl;
        }

        // ---------- SerializedProperty helpers ----------
        public static SerializedProperty FindPropAny(SerializedObject so, params string[] names)
        {
            if (so == null) return null;
            foreach (var n in names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                var p = so.FindProperty(n);
                if (p != null) return p;
            }
            return null;
        }

        public static void DrawPropIfPresent(SerializedObject so, string prop, string label = null, bool includeChildren = false)
        {
            var p = so.FindProperty(prop);
            if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label ?? p.displayName), includeChildren);
        }

        public static void DrawPropIfPresent(SerializedProperty holder, string prop, string label = null, bool includeChildren = false)
        {
            var p = holder.FindPropertyRelative(prop);
            if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label ?? p.displayName), includeChildren);
        }

        // ---------- Safe object label ----------
        public static string SafeName(UnityEngine.Object obj, string fallback = "(missing)")
        {
            return obj ? obj.name : fallback;
        }

        // ---------- Asset helpers ----------
        public static T CreateAsset<T>(string folder, string fileName) where T : ScriptableObject
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }
            var asset = ScriptableObject.CreateInstance<T>();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{Sanitize(fileName)}.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        public static void RenameAsset(UnityEngine.Object obj, string newName)
        {
            string p = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrWhiteSpace(p)) return;
            AssetDatabase.RenameAsset(p, Sanitize(newName));
            AssetDatabase.SaveAssets();
        }

        public static bool ConfirmDelete(UnityEngine.Object obj, string title = "Delete asset?")
        {
            if (!obj) return false;
            string path = AssetDatabase.GetAssetPath(obj);
            return EditorUtility.DisplayDialog(title, $"Delete '{obj.name}'?\n{path}", "Yes", "No");
        }

        public static void DeleteAsset(UnityEngine.Object obj)
        {
            if (!obj) return;
            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        public static string Sanitize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "New_Asset";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }
        // ---------- Safe vertical (no frame) ----------
        public static void Vertical(Action body)
        {
            EditorGUILayout.BeginVertical();
            try { body?.Invoke(); }
            finally { EditorGUILayout.EndVertical(); }
        }


    }
}
