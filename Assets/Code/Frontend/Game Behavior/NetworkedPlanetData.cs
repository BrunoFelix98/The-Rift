using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class NetworkedPlanetData : NetworkBehaviour, IPoolable
{
    [Header("Runtime Data (Mutable)")]
    public PlanetDTO planetDTO;

    [Header("Pool Integration")]
    [HideInInspector]
    public GameObject planetPrefab;

    [Header("Original Data (Immutable)")]
    [SerializeField] private PlanetData planetData;

    // Planets have infinite resources, so no depletion tracking needed
    private Dictionary<string, NetworkVariable<int>> resourceAmounts = new Dictionary<string, NetworkVariable<int>>();

    void Awake()
    {
        planetData = GetComponent<PlanetData>();
        if (planetData == null)
        {
            Debug.LogError($"NetworkedPlanetData requires PlanetData component on {gameObject.name}");
            return;
        }

        ConvertSOToDTO();
        InitializeNetworkVariables();
    }

    void ConvertSOToDTO()
    {
        if (planetData.data == null) return;

        planetDTO = new PlanetDTO
        {
            planetName = planetData.data.planetName,
            resources = new List<ResourceDTO>(),
            moons = new List<MoonDTO>() // Moons will be separate GameObjects, this is just for data reference
        };

        // Convert resources (planets have infinite resources)
        foreach (var resourceSO in planetData.data.resources)
        {
            planetDTO.resources.Add(new ResourceDTO
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

        // Planets have infinite resources, but we still track for consistency
        foreach (var resource in planetDTO.resources)
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

        // Planets have infinite resources - always succeed
        NotifyMiningSuccessClientRpc(resourceName, amount, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    void NotifyMiningSuccessClientRpc(string resourceName, int amount, ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            Debug.Log($"Successfully mined {amount} {resourceName} from {planetDTO.planetName} (infinite source)");
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
        // Reset to original SO values
        ConvertSOToDTO();
        Debug.Log($"Planet {planetDTO.planetName} returned to pool and reset");
    }

    public bool HasResource(string resourceName)
    {
        return resourceAmounts.ContainsKey(resourceName);
    }
}
