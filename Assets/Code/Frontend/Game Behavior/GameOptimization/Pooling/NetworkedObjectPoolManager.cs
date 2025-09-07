// Server-side Networked Object Pool Manager
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[System.Serializable]
public class PooledPrefab
{
    [Header("Prefab Settings")]
    public GameObject prefab;
    public int initialPoolSize = 100;
    public int maxPoolSize = 500;
    public bool autoExpand = true;

    [Header("Pool Info (Read Only)")]
    [SerializeField] private int activeCount;
    [SerializeField] private int pooledCount;

    public int ActiveCount => activeCount;
    public int PooledCount => pooledCount;

    public void UpdateCounts(int active, int pooled)
    {
        activeCount = active;
        pooledCount = pooled;
    }
}

public class NetworkedObjectPoolManager : NetworkBehaviour
{
    [Header("Pool Configuration")]
    public List<PooledPrefab> pooledPrefabs = new List<PooledPrefab>();

    [SerializeField]
    private Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
    private Dictionary<GameObject, PooledPrefab> prefabSettings = new Dictionary<GameObject, PooledPrefab>();
    private Dictionary<GameObject, int> activeCounts = new Dictionary<GameObject, int>();

    public static NetworkedObjectPoolManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InitializePools()
    {
        foreach (var pooledPrefab in pooledPrefabs)
        {
            //Create empty storage
            var queue = new Queue<GameObject>();
            pools[pooledPrefab.prefab] = queue;
            prefabSettings[pooledPrefab.prefab] = pooledPrefab;
            activeCounts[pooledPrefab.prefab] = 0;

            // Pre-instantiate objects
            for (int i = 0; i < pooledPrefab.initialPoolSize; i++)
            {
                GameObject obj = Instantiate(pooledPrefab.prefab); //Instantiate them
                var netObj = obj.GetComponent<NetworkObject>();
                netObj.Spawn();
                netObj.Despawn(false);
                obj.SetActive(false); //Set them inactive (Network objects cannot be inactive at spawn)
                queue.Enqueue(obj); //Store it in the pool
            }

            Debug.Log($"Initialized pool for {pooledPrefab.prefab.name} with {pooledPrefab.initialPoolSize} objects");
        }
    }

    // Server-only method to get pooled object
    public GameObject GetPooledObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!IsServer) { Debug.LogError("GetPooledObject only on server"); return null; }
        if (!pools.TryGetValue(prefab, out var pool)) { Debug.LogError($"No pool for {prefab.name}"); return null; }
        var config = prefabSettings[prefab];

        // Dequeue or expand
        GameObject obj = pool.Count > 0 ? pool.Dequeue() : (config.autoExpand && activeCounts[prefab] + pool.Count < config.maxPoolSize ? Instantiate(prefab) : null);

        if (obj == null)
        {
            Debug.LogWarning($"Pool exhausted for {prefab.name}");
            return null;
        }

        // Reset transform & activate
        obj.transform.SetParent(null);
        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        // 4) Spawn on network (registered earlier)
        var netObj = obj.GetComponent<NetworkObject>();
        netObj.Spawn();

        activeCounts[prefab]++;
        UpdatePoolDisplay(prefab);

        return obj;
    }

    // Server-only method to return object to pool
    public void ReturnToPool(GameObject obj, GameObject prefab)
    {
        if (!IsServer) { Debug.LogError("ReturnToPool only on server"); return; }
        if (!pools.TryGetValue(prefab, out var pool)) { Debug.LogError($"No pool for {prefab.name}"); return; }

        // 5) Despawn but keep registered
        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj.IsSpawned)
            netObj.Despawn(destroy: false);

        ResetObject(obj);

        obj.SetActive(false);
        pool.Enqueue(obj);

        activeCounts[prefab]--;
        UpdatePoolDisplay(prefab);
    }

    void ResetObject(GameObject obj)
    {
        // Reset common components
        var rigidbody = obj.GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        // Add more reset logic as needed for specific object types
        var poolable = obj.GetComponent<IPoolable>();
        poolable?.OnReturnToPool();
    }

    void UpdatePoolDisplay(GameObject prefab)
    {
        var settings = prefabSettings[prefab];
        settings.UpdateCounts(activeCounts[prefab], pools[prefab].Count);
    }

    // Auto-maintain pool sizes (keeps 100+ objects as requested)
    void Update()
    {
        //Clients cant run this
        if (!IsServer) return;

        //Key value pairs = KVP
        foreach (var kvp in pools)
        {
            var prefab = kvp.Key;
            var pool = kvp.Value;
            var settings = prefabSettings[prefab];

            // Maintain minimum pool size
            int totalObjects = activeCounts[prefab] + pool.Count;

            //If the total amount of objects in this pool is less than the required buffer, add more
            if (totalObjects < settings.initialPoolSize)
            {
                int needed = settings.initialPoolSize - totalObjects;
                for (int i = 0; i < needed; i++)
                {
                    GameObject obj = Instantiate(prefab); //Instantiate it
                    obj.SetActive(false); //Set it inactive
                    pool.Enqueue(obj); //Put it in storage
                }
                UpdatePoolDisplay(prefab);
            }
        }
    }
}

// Interface for objects that need custom reset logic
public interface IPoolable
{
    void OnReturnToPool();
}
