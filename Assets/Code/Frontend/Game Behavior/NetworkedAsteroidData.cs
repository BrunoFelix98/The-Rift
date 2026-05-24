using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class NetworkedAsteroidData : NetworkBehaviour, IPoolable
{
    [Header("Runtime Data (Mutable)")]
    public AsteroidDTO asteroidDTO;

    [Header("Pool Integration")]
    [HideInInspector]
    public GameObject asteroidPrefab;

    [Header("Original Data (Immutable)")]
    [SerializeField] private AsteroidData asteroidBehavior;

    private NetworkVariable<bool> isDepleted = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Dictionary<string, NetworkVariable<int>> resourceAmounts = new Dictionary<string, NetworkVariable<int>>();

    void Awake()
    {
        asteroidBehavior = GetComponent<AsteroidData>();
    }

    public void Reinitialize(AsteroidSO so, CelestialHierarchyNode node)
    {
        if (asteroidBehavior == null) asteroidBehavior = GetComponent<AsteroidData>();
        asteroidBehavior.data = so;
        
        ConvertDataToDTO(node);
        InitializeNetworkVariables();
    }

    void ConvertDataToDTO(CelestialHierarchyNode node)
    {
        var op = node.Params;
        asteroidDTO = new AsteroidDTO
        {
            asteroidName = op.Name.ToString(),
            type = CelestialType.ASTEROID,
            resources = new List<ResourceDTO>()
        };

        foreach (var resData in node.Data.resources)
        {
            asteroidDTO.resources.Add(new ResourceDTO
            {
                resourceName = resData.name,
                minQuantity = resData.minQuantity,
                maxQuantity = resData.maxQuantity,
                quantity = resData.quantity,
                resourceWeight = resData.weight,
                resourceCategory = new List<ResourceCategory> { GetCategory(resData.rarity) }
            });
        }
    }

    private ResourceCategory GetCategory(string rarity)
    {
        if (string.IsNullOrEmpty(rarity)) return ResourceCategory.Universal;
        if (rarity.Contains("Common")) return ResourceCategory.Asteroid;
        if (rarity.Contains("Rare")) return ResourceCategory.Moon;
        return ResourceCategory.Planet;
    }

    void InitializeNetworkVariables()
    {
        if (!IsServer) return;
        foreach (var resource in asteroidDTO.resources)
        {
            if (!resourceAmounts.ContainsKey(resource.resourceName))
            {
                var networkVar = new NetworkVariable<int>(resource.quantity,
                    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
                resourceAmounts[resource.resourceName] = networkVar;
            }
            else resourceAmounts[resource.resourceName].Value = resource.quantity;
        }
        isDepleted.Value = false;
    }

    public void OnReturnToPool() { asteroidDTO = null; }
}
