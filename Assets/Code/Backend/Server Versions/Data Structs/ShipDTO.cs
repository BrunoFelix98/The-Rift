using System;
using System.Collections.Generic;

[Serializable]
public class ShipDTO
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

    public float targettingResolutionStrength;
    public int targetCount;

    public float shieldHP;
    public ResistProfileDTO shieldResistances;
    public float armorHP;
    public ResistProfileDTO armorResistances;

    public float speed;
    public float warpSpeed;
    public float tonnage;

    public float cargoCapacity;
    public float longAxisLength;

    public string blueprintId;

    public List<string> weaponSlotIds = new List<string>();
    public List<string> propulsionSlotIds = new List<string>();
    public List<string> defensiveSlotIds = new List<string>();
    public List<string> shipModificationSlotIds = new List<string>();
    public List<string> capitalSlotModuleIds = new List<string>();
}