using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewShip", menuName = "Game Data/Ship")]
public class ShipSO : ScriptableObject
{
    public string shipName;
    public string builtBy;
    public string currentOwner;
    public ShipWeightClass weightClass;

    public int weaponSlotCount;
    public int defensiveSlotCount;
    public int propulsionSlotCount;
    public int modificationSlotCount;
    public int capitalShipModuleSlotCount;

    public float targettingResolutionStrength; // Higher = faster lock (non-fighter ships)
    public int targetCount;                     // Max targets locked (non-fighter ships)

    public float shieldHP;
    public ResistanceProfileSO shieldResistances;
    public float armorHP;
    public ResistanceProfileSO armorResistances;

    public float speed;
    public float warpSpeed;
    public float tonnage;                      // weight

    public float cargoCapacity;
    public float longAxisLength;

    public BlueprintSO blueprint;

    public List<ModuleSO> weaponSlots = new List<ModuleSO>();
    public List<ModuleSO> propulsionSlots = new List<ModuleSO>();
    public List<ModuleSO> defensiveSlots = new List<ModuleSO>();
    public List<ModuleSO> shipModificationSlots = new List<ModuleSO>();
    public List<ModuleSO> capitalSlotModules = new List<ModuleSO>();
}
