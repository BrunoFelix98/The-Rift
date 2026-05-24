using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class NetworkedStationData : NetworkBehaviour, IPoolable
{
    [Header("Runtime Data (Mutable)")]
    public StationDTO stationDTO;

    [Header("Pool Integration")]
    [HideInInspector]
    public GameObject stationPrefab;

    [Header("Original Data (Immutable)")]
    [SerializeField] private StationData stationData;

    private NetworkVariable<float> currentShield = new NetworkVariable<float>(0f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> currentArmor = new NetworkVariable<float>(0f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> isDestroyed = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public void Reinitialize(StationSO so, CelestialHierarchyNode node)
    {
        if (stationData == null) stationData = GetComponent<StationData>();
        stationData.data = so;
        ConvertSOToDTO(node);
        InitializeNetworkVariables();
    }

    void ConvertSOToDTO(CelestialHierarchyNode node)
    {
        var op = node.Params;
        if (stationData.data == null) return;

        stationDTO = new StationDTO
        {
            stationName = op.Name.ToString(),
            allegiance = GetFactionName(op.FactionId),
            builtBy = stationData.data.builtBy,
            shield = stationData.data.shield,
            armor = stationData.data.armor,
            shieldResistances = ConvertResistanceProfile(stationData.data.shieldResistances),
            armorResistances = ConvertResistanceProfile(stationData.data.armorResistances),
            modules = new List<ModuleDTO>()
        };

        foreach (var moduleSO in stationData.data.modules)
        {
            if (moduleSO == null) continue;
            var moduleDTO = new ModuleDTO
            {
                moduleName = moduleSO.moduleName,
                compatibleSlots = new List<string>(),
                blueprintId = moduleSO.blueprint?.name ?? "Unknown",
                moduleCategory = moduleSO.moduleCategory
            };
            foreach (var slotType in moduleSO.compatibleSlots) moduleDTO.compatibleSlots.Add(slotType.ToString());
            stationDTO.modules.Add(moduleDTO);
        }
    }

    private string GetFactionName(int id)
    {
        switch (id)
        {
            case 0: return "Aldewhara Kinship";
            case 1: return "Zypheri Dominion";
            case 2: return "Xirn";
            case 3: return "Sanctum Ferrum";
            default: return "Unknown";
        }
    }

    private ResistProfileDTO ConvertResistanceProfile(ResistanceProfileSO resistanceSO)
    {
        if (resistanceSO == null) return new ResistProfileDTO { em = 0, heat = 0, kinetic = 0, explosive = 0 };
        return new ResistProfileDTO { em = resistanceSO.em, heat = resistanceSO.heat, kinetic = resistanceSO.kinetic, explosive = resistanceSO.explosive };
    }

    void InitializeNetworkVariables()
    {
        if (!IsServer) return;
        currentShield.Value = stationDTO.shield;
        currentArmor.Value = stationDTO.armor;
        isDestroyed.Value = false;
    }

    public void OnReturnToPool() 
    {
        stationDTO = null;
    }
}
