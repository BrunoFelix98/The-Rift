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

    private Dictionary<string, NetworkVariable<int>> resourceAmounts = new Dictionary<string, NetworkVariable<int>>();

    public void Reinitialize(MoonSO so, CelestialHierarchyNode node)
    {
        if (moonData == null) moonData = GetComponent<MoonData>();
        moonData.data = so;
        
        ConvertDataToDTO(node);
        InitializeNetworkVariables();
    }

    void ConvertDataToDTO(CelestialHierarchyNode node)
    {
        var op = node.Params;
        moonDTO = new MoonDTO
        {
            moonName = op.Name.ToString(),
            resources = new List<ResourceDTO>(),
            stations = new List<StationDTO>()
        };

        foreach (var resData in node.Data.resources)
        {
            moonDTO.resources.Add(new ResourceDTO
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
            if (child.Params.Type == CelestialType.STATION)
            {
                moonDTO.stations.Add(new StationDTO { stationName = child.Params.Name.ToString() });
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
        foreach (var resource in moonDTO.resources)
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

    public void OnReturnToPool() { moonDTO = null; }
}
