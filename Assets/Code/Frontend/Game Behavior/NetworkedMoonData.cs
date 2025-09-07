using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class NetworkedMoonData : NetworkBehaviour, IPoolable
{
    [Header("Runtime Data (Mutable)")]
    public MoonDTO moonDTO;

    [Header("Pool Integration")]
    [HideInInspector]
    public GameObject moonPrefab;

    [Header("Original Data (Immutable)")]
    [SerializeField] private MoonData moonData;

    // Moons have infinite resources like planets
    private Dictionary<string, NetworkVariable<int>> resourceAmounts = new Dictionary<string, NetworkVariable<int>>();

    void Awake()
    {
        moonData = GetComponent<MoonData>();
        if (moonData == null)
        {
            Debug.LogError($"NetworkedMoonData requires MoonData component on {gameObject.name}");
            return;
        }

        ConvertSOToDTO();
        InitializeNetworkVariables();
    }

    void ConvertSOToDTO()
    {
        if (moonData.data == null) return;

        moonDTO = new MoonDTO
        {
            moonName = moonData.data.moonName,
            resources = new List<ResourceDTO>(),
            stations = new List<StationDTO>() // Stations will be separate GameObjects
        };

        // Convert resources (moons have infinite resources)
        foreach (var resourceSO in moonData.data.resources)
        {
            moonDTO.resources.Add(new ResourceDTO
            {
                resourceName = resourceSO.resourceName,
                resourceDescription = resourceSO.resourceDescription,
                minQuantity = resourceSO.minQuantity,
                maxQuantity = resourceSO.maxQuantity,
                quantity = int.MaxValue, // Infinite resources
                resourceWeight = resourceSO.resourceWeight,
                resourceCategory = new List<ResourceCategory>(resourceSO.resourceCategory),
                celestialType = new List<string>(resourceSO.celestialType),
                allowedFactions = new List<string>(resourceSO.allowedFactions)
            });
        }
    }

    void InitializeNetworkVariables()
    {
        if (!IsServer) return;

        foreach (var resource in moonDTO.resources)
        {
            var networkVar = new NetworkVariable<int>(int.MaxValue,
                NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            resourceAmounts[resource.resourceName] = networkVar;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void MineResourceServerRpc(string resourceName, int amount, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        if (!resourceAmounts.ContainsKey(resourceName))
        {
            NotifyMiningFailedClientRpc("Resource not found", rpcParams.Receive.SenderClientId);
            return;
        }

        // Moons have infinite resources
        NotifyMiningSuccessClientRpc(resourceName, amount, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    void NotifyMiningSuccessClientRpc(string resourceName, int amount, ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            Debug.Log($"Successfully mined {amount} {resourceName} from {moonDTO.moonName} (infinite source)");
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

    // IPoolable implementation
    public void OnReturnToPool()
    {
        ConvertSOToDTO();
        Debug.Log($"Moon {moonDTO.moonName} returned to pool and reset");
    }

    public bool HasResource(string resourceName)
    {
        return resourceAmounts.ContainsKey(resourceName);
    }
}
