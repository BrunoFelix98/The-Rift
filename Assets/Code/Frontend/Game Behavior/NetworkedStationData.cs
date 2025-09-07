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

    // Stations have health/shield/armor that can change
    private NetworkVariable<float> currentShield = new NetworkVariable<float>(0f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> currentArmor = new NetworkVariable<float>(0f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> isDestroyed = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        currentShield.OnValueChanged += OnShieldChanged;
        currentArmor.OnValueChanged += OnArmorChanged;
        isDestroyed.OnValueChanged += OnDestroyedChanged;
    }

    void Awake()
    {
        stationData = GetComponent<StationData>();
        if (stationData == null)
        {
            Debug.LogError($"NetworkedStationData requires StationData component on {gameObject.name}");
            return;
        }

        ConvertSOToDTO();
        InitializeNetworkVariables();
    }

    void ConvertSOToDTO()
    {
        if (stationData.data == null) return;

        stationDTO = new StationDTO
        {
            stationName = stationData.data.stationName,
            allegiance = stationData.data.allegiance?.name ?? "Unknown",
            builtBy = stationData.data.builtBy,
            shield = stationData.data.shield,
            armor = stationData.data.armor,
            shieldResistances = ConvertResistanceProfile(stationData.data.shieldResistances),
            armorResistances = ConvertResistanceProfile(stationData.data.armorResistances),
            modules = new List<ModuleDTO>() // Now properly using ModuleDTO
        };

        // Convert modules list
        foreach (var moduleSO in stationData.data.modules)
        {
            var moduleDTO = new ModuleDTO
            {
                moduleName = moduleSO.moduleName,
                compatibleSlots = new List<string>(),
                blueprintId = moduleSO.blueprint?.name ?? "Unknown", // Convert BlueprintSO to string ID
                moduleCategory = moduleSO.moduleCategory
            };

            // Convert SlotType enum list to string list
            foreach (var slotType in moduleSO.compatibleSlots)
            {
                moduleDTO.compatibleSlots.Add(slotType.ToString());
            }

            stationDTO.modules.Add(moduleDTO);
        }
    }

    // Helper method to convert ResistanceProfileSO to ResistProfileDTO
    private ResistProfileDTO ConvertResistanceProfile(ResistanceProfileSO resistanceSO)
    {
        if (resistanceSO == null)
        {
            // Return default resistance profile if null
            return new ResistProfileDTO
            {
                em = 0f,
                heat = 0f,
                kinetic = 0f,
                explosive = 0f
            };
        }

        return new ResistProfileDTO
        {
            em = resistanceSO.em,
            heat = resistanceSO.heat,
            kinetic = resistanceSO.kinetic,
            explosive = resistanceSO.explosive
        };
    }

    void InitializeNetworkVariables()
    {
        if (!IsServer) return;

        currentShield.Value = stationDTO.shield;
        currentArmor.Value = stationDTO.armor;
        isDestroyed.Value = false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void DamageStationServerRpc(float damage, DamageType damageType, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || isDestroyed.Value) return;

        float effectiveShieldDamage = 0f;
        float effectiveArmorDamage = 0f;

        // Calculate effective damage against shields
        if (currentShield.Value > 0)
        {
            float shieldResistancePercent = GetResistanceValue(stationDTO.shieldResistances, damageType);
            // Convert percentage to multiplier: 25% resistance = 0.75 damage multiplier
            effectiveShieldDamage = damage * (1f - (shieldResistancePercent / 100f));

            float remainingDamage = effectiveShieldDamage - currentShield.Value;
            currentShield.Value = Mathf.Max(0, currentShield.Value - effectiveShieldDamage);

            // Overflow to armor
            if (remainingDamage > 0)
            {
                float armorResistancePercent = GetResistanceValue(stationDTO.armorResistances, damageType);
                effectiveArmorDamage = remainingDamage * (1f - (armorResistancePercent / 100f));
            }
        }
        else
        {
            // Direct armor damage
            float armorResistancePercent = GetResistanceValue(stationDTO.armorResistances, damageType);
            effectiveArmorDamage = damage * (1f - (armorResistancePercent / 100f));
        }

        // Apply armor damage
        if (effectiveArmorDamage > 0)
        {
            currentArmor.Value = Mathf.Max(0, currentArmor.Value - effectiveArmorDamage);
        }

        // Check destruction
        if (currentShield.Value <= 0 && currentArmor.Value <= 0)
        {
            isDestroyed.Value = true;
        }

        NotifyDamageClientRpc(damage, effectiveShieldDamage, effectiveArmorDamage, damageType, currentShield.Value, currentArmor.Value);
    }

    [ClientRpc]
    void NotifyDamageClientRpc(float originalDamage, float shieldDamage, float armorDamage, DamageType damageType, float currentShield, float currentArmor)
    {
        float shieldResistancePercent = GetResistanceValue(stationDTO.shieldResistances, damageType);
        float armorResistancePercent = GetResistanceValue(stationDTO.armorResistances, damageType);

        Debug.Log($"Station {stationDTO.stationName} took {originalDamage} {damageType} damage:");
        Debug.Log($"  Shield: {shieldDamage} damage ({shieldResistancePercent}% resistance)");
        Debug.Log($"  Armor: {armorDamage} damage ({armorResistancePercent}% resistance)");
        Debug.Log($"  Remaining: {currentShield} shield, {currentArmor} armor");
    }

    // Helper method to get resistance value by damage type
    private float GetResistanceValue(ResistProfileDTO resistances, DamageType damageType)
    {
        return damageType switch
        {
            DamageType.EM => resistances.em,
            DamageType.HEAT => resistances.heat,
            DamageType.KINETIC => resistances.kinetic,
            DamageType.EXPLOSIVE => resistances.explosive,
            _ => 0f
        };
    }

    [ServerRpc(RequireOwnership = false)]
    public void DockAtStationServerRpc(ulong playerId, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || isDestroyed.Value) return;

        // Handle docking logic here
        NotifyDockingSuccessClientRpc(playerId);
    }

    void OnShieldChanged(float previousValue, float newValue)
    {
        // Update visual shield effects
        Debug.Log($"Station {stationDTO.stationName} shield: {newValue}/{stationDTO.shield}");
    }

    void OnArmorChanged(float previousValue, float newValue)
    {
        // Update visual armor damage
        Debug.Log($"Station {stationDTO.stationName} armor: {newValue}/{stationDTO.armor}");
    }

    void OnDestroyedChanged(bool previousValue, bool newValue)
    {
        if (newValue)
        {
            // Station destroyed - return to pool or handle destruction
            if (NetworkedObjectPoolManager.Instance != null && stationPrefab != null)
            {
                NetworkedObjectPoolManager.Instance.ReturnToPool(gameObject, stationPrefab);
            }
        }
    }

    [ClientRpc]
    void NotifyDockingSuccessClientRpc(ulong playerId)
    {
        Debug.Log($"Player {playerId} successfully docked at {stationDTO.stationName}");
    }

    // IPoolable implementation
    public void OnReturnToPool()
    {
        ConvertSOToDTO();

        if (IsServer)
        {
            currentShield.Value = stationDTO.shield;
            currentArmor.Value = stationDTO.armor;
            isDestroyed.Value = false;
        }

        Debug.Log($"Station {stationDTO.stationName} returned to pool and reset");
    }

    public bool IsOperational()
    {
        return !isDestroyed.Value;
    }

    public float GetShieldPercentage()
    {
        return stationDTO.shield > 0 ? (currentShield.Value / stationDTO.shield) : 0f;
    }

    public float GetArmorPercentage()
    {
        return stationDTO.armor > 0 ? (currentArmor.Value / stationDTO.armor) : 0f;
    }
}
