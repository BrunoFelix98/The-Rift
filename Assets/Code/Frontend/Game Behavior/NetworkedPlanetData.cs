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

    private Dictionary<string, NetworkVariable<int>> resourceAmounts = new Dictionary<string, NetworkVariable<int>>();

    public void Reinitialize(PlanetSO so, CelestialHierarchyNode node)
    {
        if (planetData == null) planetData = GetComponent<PlanetData>();
        planetData.data = so;
        
        ConvertDataToDTO(node);
        InitializeNetworkVariables();
    }

    void ConvertDataToDTO(CelestialHierarchyNode node)
    {
        var op = node.Params;
        planetDTO = new PlanetDTO
        {
            planetName = op.Name.ToString(),
            resources = new List<ResourceDTO>(),
            moons = new List<MoonDTO>()
        };

        // Use PRE-GENERATED resources from persistence data
        foreach (var resData in node.Data.resources)
        {
            planetDTO.resources.Add(new ResourceDTO
            {
                resourceName = resData.name,
                minQuantity = resData.minQuantity,
                maxQuantity = resData.maxQuantity,
                quantity = resData.quantity,
                resourceWeight = resData.weight,
                resourceCategory = new List<ResourceCategory> { GetCategory(resData.rarity) }
            });
        }

        foreach (var child in node.Children)
        {
            if (child.Params.Type == CelestialType.MOON)
            {
                planetDTO.moons.Add(new MoonDTO { moonName = child.Params.Name.ToString() });
            }
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
        foreach (var resource in planetDTO.resources)
        {
            if (!resourceAmounts.ContainsKey(resource.resourceName))
            {
                var networkVar = new NetworkVariable<int>(resource.quantity,
                    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
                resourceAmounts[resource.resourceName] = networkVar;
            }
            else resourceAmounts[resource.resourceName].Value = resource.quantity;
        }
    }

    public void OnReturnToPool() { planetDTO = null; }
}
