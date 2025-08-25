using System;
using System.Collections.Generic;

[Serializable]
public class StationDTO
{
    public string stationName;
    public string allegianceId;   // Use string IDs or int IDs instead of direct ScriptableObject refs
    public string builtBy;
    public float shield;
    public string shieldResistancesId;
    public float armor;
    public string armorResistancesId;
    public string blueprintId;
    public List<ModuleDTO> modules = new List<ModuleDTO>();
}