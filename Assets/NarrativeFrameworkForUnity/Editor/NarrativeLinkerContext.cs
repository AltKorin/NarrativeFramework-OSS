#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SimulationGame.Editor
{
    /// <summary>
    /// Single source of truth for the active GameData in editor tools.
    /// - Stores current selection
    /// - Notifies listeners on change
    /// - Can resolve from EditorPrefs when not explicitly set
    /// </summary>
    [InitializeOnLoad]
    public static class NarrativeLinkerContext
    {
        private const string PREF_GAMEDATA_PATH = "NarrativeLinker.PreferredGameDataPath";

        public static GameData Active { get; private set; }
        public static event System.Action<GameData> Changed;

        static NarrativeLinkerContext()
        {
            // Lazy resolve once on domain load (doesn't ping AssetDatabase repeatedly)
            ResolveFromPrefsIfNull();
        }

        public static void SetActive(GameData gd)
        {
            if (Active == gd) return;
            Active = gd;

            // Persist the path for other tools / after domain reload
            if (gd != null)
            {
                var path = AssetDatabase.GetAssetPath(gd);
                if (!string.IsNullOrEmpty(path))
                    EditorPrefs.SetString(PREF_GAMEDATA_PATH, path);
            }

            Changed?.Invoke(Active);
        }

        /// <summary>Return Active; if null, try to resolve from EditorPrefs → canonical → any.</summary>
        public static GameData GetOrResolve()
        {
            if (Active != null) return Active;
            ResolveFromPrefsIfNull();
            return Active;
        }

        private static void ResolveFromPrefsIfNull()
        {
            if (Active != null) return;

            var prefPath = EditorPrefs.GetString(PREF_GAMEDATA_PATH, null);
            if (!string.IsNullOrEmpty(prefPath))
            {
                var pref = AssetDatabase.LoadAssetAtPath<GameData>(prefPath);
                if (pref != null) { Active = pref; return; }
            }

            var canonical = AssetDatabase.LoadAssetAtPath<GameData>("Assets/SimulationGame/Resources/GameData.asset");
            if (canonical != null) { Active = canonical; return; }

            foreach (var guid in AssetDatabase.FindAssets("t:GameData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var gd = AssetDatabase.LoadAssetAtPath<GameData>(path);
                if (gd != null) { Active = gd; return; }
            }
        }
    }
}
#endif
