using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

[CreateAssetMenu(fileName = "RevisedStationData", menuName = "Universe/Revised Station Data", order = 10)]
public class RevisedStationData : ScriptableObject
{
    [Header("Station Info")]
    public string stationName;
    [TextArea] public string stationDescription;
    public RevisedMoonData parentMoon;

    [Header("Ownership")]
    public RevisedFactionData owningFaction; // Can be null for neutral/player-owned
    public string owningCorporation;  // Optional sub-owner (e.g. "Zypheri Dominion" corp)
    public bool playerOwned; // True if player owned, false if faction owned

    [Header("Defenses")]
    public RevisedResistanceProfile resistances;
    public float shieldHitpoints;
    public float armorHitpoints;

    [Header("Slot Configuration")]
    public List<RevisedSlotData> availableSlots = new();
    public List<RevisedModuleData> installedModules = new();

    [Header("Location")]
    public RevisedSystemData parentSystem;

    [Header("Station specifications")]
    public bool isMarket;
    public bool isRefinery;
    public bool isManufacturer;

    //Call this after station is onlined
    public void SpecificationSelection()
    {
        if (HasModule(ModuleCategory.StationModule) && installedModules.Any(m => m.moduleName.Equals("Market")))
        {
            isMarket = true;
        }

        if (HasModule(ModuleCategory.StationModule) && installedModules.Any(m => m.moduleName.Equals("Refinery S") || m.moduleName.Equals("Refinery M") || m.moduleName.Equals("Refinery L")))
        {
            isRefinery = true;
        }

        if (HasModule(ModuleCategory.StationModule) && installedModules.Any(m => m.moduleName.Equals("Manufacturing Plant S") || m.moduleName.Equals("Manufacturing Plant M") || m.moduleName.Equals("Manufacturing Plant L")))
        {
            isManufacturer = true;
        }
    }

    public bool HasModule(ModuleCategory category)
    {
        return installedModules.Any(m => m.moduleCategory == category);
    }

    public bool HasAvailableSlotFor(RevisedModuleData module)
    {
        int totalSlots = availableSlots.Count(s => s.slotCategory == module.slotCategory && s.allowedModules.Contains(module.moduleCategory));
        int filledSlots = installedModules.Count(m => m.slotCategory == module.slotCategory);
        return filledSlots < totalSlots;
    }

    public bool InstallModule(RevisedModuleData module)
    {
        if (HasAvailableSlotFor(module))
        {
            installedModules.Add(module);
            return true;
        }
        return false;
    }
}