// Assets/SimulationGame/Runtime/Scripts/DontDestroyOnLoad.cs
using UnityEngine;

namespace SimulationGame
{
    /// <summary>
    /// Ensures the root GameObject survives scene loads.
    /// Unity requires DontDestroyOnLoad to be called on a root object.
    /// </summary>
    public class DontDestroyOnLoadTag : MonoBehaviour
    {
        private void Awake()
        {
            // Always apply on root to avoid Unity warning.
            var root = transform.root;
#if UNITY_EDITOR
            if (root != transform)
                Debug.LogWarning("[DontDestroyOnLoadTag] Attached to a child; applying to root object.", root.gameObject);
#endif
            DontDestroyOnLoad(root.gameObject);
        }
    }
}
