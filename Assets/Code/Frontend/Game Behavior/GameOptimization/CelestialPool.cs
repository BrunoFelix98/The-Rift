using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class CelestialPool : MonoBehaviour
{
    public static CelestialPool Instance { get; private set; }

    [System.Serializable]
    public struct PoolConfig
    {
        public CelestialType type;
        public GameObject prefab;
        public int initialSize;
    }

    [Header("Pool Settings")]
    public List<PoolConfig> poolConfigs = new List<PoolConfig>();
    
    private Dictionary<CelestialType, Queue<GameObject>> pools = new Dictionary<CelestialType, Queue<GameObject>>();
    private Dictionary<CelestialType, GameObject> prefabs = new Dictionary<CelestialType, GameObject>();
    private bool isShuttingDown = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        InitializePools();
    }

    private void OnApplicationQuit()
    {
        isShuttingDown = true;
    }

    private void InitializePools()
    {
        foreach (var config in poolConfigs)
        {
            if (config.prefab == null) continue;

            prefabs[config.type] = config.prefab;
            pools[config.type] = new Queue<GameObject>();

            for (int i = 0; i < config.initialSize; i++)
            {
                CreateNewInstance(config.type, config.prefab);
            }
        }
    }

    private GameObject CreateNewInstance(CelestialType type, GameObject prefab)
    {
        GameObject obj = Instantiate(prefab, transform);
        obj.SetActive(false);

        // Visual objects in the pool must NOT have NetworkObject components,
        // otherwise NGO will try to register them as scene-placed objects.
        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null) DestroyImmediate(netObj);

        pools[type].Enqueue(obj);
        return obj;
    }

    public GameObject Get(CelestialType type)
    {
        if (isShuttingDown) return null;

        if (pools.TryGetValue(type, out var pool))
        {
            if (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                if (obj != null)
                {
                    obj.SetActive(true);
                    return obj;
                }
            }
            
            if (prefabs.TryGetValue(type, out var prefab))
            {
                GameObject newObj = Instantiate(prefab, transform);
                var netObj = newObj.GetComponent<NetworkObject>();
                if (netObj != null) DestroyImmediate(netObj);
                newObj.SetActive(true);
                return newObj;
            }
        }
        
        return null;
    }

    public void ReturnToPool(CelestialType type, GameObject obj)
    {
        if (obj == null || isShuttingDown) return;
        
        obj.SetActive(false);
        
        // Performance optimization: only set parent if it's not already ours
        if (obj.transform.parent != transform)
        {
            obj.transform.SetParent(transform);
        }
        
        if (pools.TryGetValue(type, out var pool))
        {
            pool.Enqueue(obj);
        }
        else
        {
            var netObj = obj.GetComponent<NetworkObject>();
            if (netObj != null) DestroyImmediate(netObj);
            Destroy(obj);
        }
    }
}
