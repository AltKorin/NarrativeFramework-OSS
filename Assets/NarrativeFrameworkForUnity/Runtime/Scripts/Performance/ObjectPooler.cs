using System.Collections.Generic;
using UnityEngine;

namespace SimulationGame.Performance
{
    public class ObjectPooler : MonoBehaviour
    {
        [System.Serializable]
        public class Pool
        {
            public string tag;
            public GameObject prefab;
            public int size = 1;
        }

        public List<Pool> pools;
        public Dictionary<string, Queue<GameObject>> poolDictionary;

        public static ObjectPooler Instance;

        private void Awake()
        {
            Instance = this;
            BuildPoolsIfNeeded();
        }

        private void BuildPoolsIfNeeded()
        {
            if (poolDictionary != null) return;

            poolDictionary = new Dictionary<string, Queue<GameObject>>();

            if (pools == null)
            {
                Debug.LogWarning("[ObjectPooler] No pools configured.");
                return;
            }

            foreach (var pool in pools)
            {
                if (pool == null || pool.prefab == null || string.IsNullOrEmpty(pool.tag))
                {
                    Debug.LogWarning("[ObjectPooler] Skipping invalid pool entry.");
                    continue;
                }

                var q = new Queue<GameObject>();
                int count = Mathf.Max(1, pool.size);

                for (int i = 0; i < count; i++)
                {
                    var obj = Instantiate(pool.prefab);
                    obj.SetActive(false);
                    q.Enqueue(obj);
                }

                poolDictionary[pool.tag] = q;
            }
        }

        public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
        {
            if (poolDictionary == null)
                BuildPoolsIfNeeded();

            if (poolDictionary == null || !poolDictionary.ContainsKey(tag))
            {
                Debug.LogWarning($"[ObjectPooler] Pool with tag '{tag}' not found.");
                return null;
            }

            GameObject objectToSpawn =
                poolDictionary[tag].Count > 0
                    ? poolDictionary[tag].Dequeue()
                    : null;

            if (objectToSpawn == null)
            {
                // Spróbuj znaleźć definicję, by dorobić sztukę „na żądanie”
                var def = pools?.Find(p => p != null && p.tag == tag && p.prefab != null);
                if (def != null)
                {
                    objectToSpawn = Instantiate(def.prefab);
                }
                else
                {
                    Debug.LogWarning($"[ObjectPooler] Pool '{tag}' is empty and prefab not found.");
                    return null;
                }
            }

            objectToSpawn.transform.position = position;
            objectToSpawn.transform.rotation = rotation;
            objectToSpawn.SetActive(true);
            return objectToSpawn;
        }

        public void ReturnToPool(string tag, GameObject objectToReturn)
        {
            if (objectToReturn == null) return;

            if (poolDictionary == null || !poolDictionary.ContainsKey(tag))
            {
                // Brak puli – bezpiecznie niszczymy, żeby nie zaśmiecać sceny
                Destroy(objectToReturn);
                return;
            }

            objectToReturn.SetActive(false);
            poolDictionary[tag].Enqueue(objectToReturn);
        }
    }
}
