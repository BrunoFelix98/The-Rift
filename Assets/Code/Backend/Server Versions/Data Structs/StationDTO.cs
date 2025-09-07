using System;
using System.Collections.Generic;

[Serializable]
public class StationDTO
{
    public string stationName;
    public string allegiance;   // Use string IDs or int IDs instead of direct ScriptableObject refs
    public string builtBy;
    public float shield;
    public ResistProfileDTO shieldResistances;
    public float armor;
    public ResistProfileDTO armorResistances;
    public string blueprintId;
    public List<ModuleDTO> modules = new List<ModuleDTO>();
}