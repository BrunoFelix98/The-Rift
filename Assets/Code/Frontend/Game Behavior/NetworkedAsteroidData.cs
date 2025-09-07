using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class NetworkedAsteroidData : NetworkBehaviour, IPoolable
{
    [Header("Runtime Data (Mutable)")]
    public AsteroidDTO asteroidDTO;

    [Header("Pool Integration")]
    [HideInInspector] // Will be set automatically
    public GameObject asteroidPrefab;

    [Header("Original Data (Immutable)")]
    [SerializeField] private AsteroidData asteroidBehavior;

    private NetworkVariable<bool> isDepleted = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // NetworkVariables for each resource amount
    private Dictionary<string, NetworkVariable<int>> resourceAmounts = new Dictionary<string, NetworkVariable<int>>();

    void Awake()
    {
        asteroidBehavior = GetComponent<AsteroidData>();
        if (asteroidBehavior == null)
        {
            Debug.LogError($"NetworkedAsteroidData requires AsteroidBehavior component on {gameObject.name}");
            return;
        }

        // Auto-find prefab reference from pool manager if not set
        if (asteroidPrefab == null)
        {
            AutoFindPrefabReference();
        }

        ConvertSOToDTO();
        InitializeNetworkVariables();
    }

    void AutoFindPrefabReference()
    {
        // Try to find matching prefab in pool manager
        if (NetworkedObjectPoolManager.Instance != null)
        {
            foreach (var pooledPrefab in NetworkedObjectPoolManager.Instance.pooledPrefabs)
            {
                var prefabAsteroidData = pooledPrefab.prefab.GetComponent<NetworkedAsteroidData>();
                if (prefabAsteroidData != null)
                {
                    asteroidPrefab = pooledPrefab.prefab;
                    break;
                }
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        isDepleted.OnValueChanged += OnDepletionChanged;
    }

    void ConvertSOToDTO()
    {
        if (asteroidBehavior.data == null) return;

        asteroidDTO = new AsteroidDTO
        {
            asteroidName = asteroidBehavior.data.asteroidName,
            type = CelestialType.ASTEROID,
            resources = new List<ResourceDTO>()
        };

        // Convert ALL resource fields from SO to DTO
        foreach (var resourceSO in asteroidBehavior.data.resources)
        {
            asteroidDTO.resources.Add(new ResourceDTO
            {
                resourceName = resourceSO.resourceName,
                resourceDescription = resourceSO.resourceDescription,
                minQuantity = resourceSO.minQuantity,
                maxQuantity = resourceSO.maxQuantity,
                quantity = resourceSO.quantity, // This is the mutable field
                resourceWeight = resourceSO.resourceWeight,
                resourceCategory = new List<ResourceCategory>(resourceSO.resourceCategory), // Copy the list
                celestialType = new List<string>(resourceSO.celestialType), // Copy the list
                allowedFactions = new List<string>(resourceSO.allowedFactions) // Copy the list
            });
        }
    }

    void InitializeNetworkVariables()
    {
        if (!IsServer) return;

        foreach (var resource in asteroidDTO.resources)
        {
            var networkVar = new NetworkVariable<int>(resource.quantity,
                NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            resourceAmounts[resource.resourceName] = networkVar;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void MineResourceServerRpc(string resourceName, int amount, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || isDepleted.Value) return;

        if (!resourceAmounts.ContainsKey(resourceName))
        {
            NotifyMiningFailedClientRpc("Resource not found", rpcParams.Receive.SenderClientId);
            return;
        }

        var networkVar = resourceAmounts[resourceName];
        int currentAmount = networkVar.Value;

        if (currentAmount <= 0)
        {
            NotifyMiningFailedClientRpc("Resource depleted", rpcParams.Receive.SenderClientId);
            return;
        }

        int actualMined = Mathf.Min(amount, currentAmount);
        networkVar.Value = currentAmount - actualMined;

        // Update DTO
        var resourceDTO = asteroidDTO.resources.Find(r => r.resourceName == resourceName);
        if (resourceDTO != null)
        {
            resourceDTO.quantity = networkVar.Value;
        }

        CheckDepletion();
        NotifyMiningSuccessClientRpc(resourceName, actualMined, rpcParams.Receive.SenderClientId);
    }

    void CheckDepletion()
    {
        bool allDepleted = true;
        foreach (var kvp in resourceAmounts)
        {
            if (kvp.Value.Value > 0)
            {
                allDepleted = false;
                break;
            }
        }

        if (allDepleted)
        {
            isDepleted.Value = true;
        }
    }

    void OnDepletionChanged(bool previousValue, bool newValue)
    {
        if (newValue && IsServer)
        {
            // Use the pool manager instead of manual despawning
            if (NetworkedObjectPoolManager.Instance != null && asteroidPrefab != null)
            {
                NetworkedObjectPoolManager.Instance.ReturnToPool(gameObject, asteroidPrefab);
            }
            else
            {
                Debug.LogWarning($"Cannot return {gameObject.name} to pool - missing pool manager or prefab reference");
            }
        }
    }

    // IPoolable implementation - called by pool manager when object is returned
    public void OnReturnToPool()
    {
        // Reset DTO to original SO values
        ConvertSOToDTO();

        // Reset network variables
        if (IsServer)
        {
            isDepleted.Value = false;

            foreach (var kvp in resourceAmounts)
            {
                // Find matching resource in DTO and reset
                var resource = asteroidDTO.resources.Find(r => r.resourceName == kvp.Key);
                if (resource != null)
                {
                    kvp.Value.Value = resource.quantity;
                }
            }
        }

        Debug.Log($"Asteroid {asteroidDTO.asteroidName} returned to pool and reset");
    }

    [ClientRpc]
    void NotifyMiningSuccessClientRpc(string resourceName, int amount, ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            Debug.Log($"Successfully mined {amount} {resourceName} from {asteroidDTO.asteroidName}");
        }
    }

    [ClientRpc]
    void NotifyMiningFailedClientRpc(string reason, ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            Debug.Log($"Mining failed: {reason}");
        }
    }

    // Helper method to get current resource amount
    public int GetResourceAmount(string resourceName)
    {
        if (resourceAmounts.ContainsKey(resourceName))
        {
            return resourceAmounts[resourceName].Value;
        }
        return 0;
    }

    // Helper method to check if resource exists
    public bool HasResource(string resourceName)
    {
        return resourceAmounts.ContainsKey(resourceName) && resourceAmounts[resourceName].Value > 0;
    }
}