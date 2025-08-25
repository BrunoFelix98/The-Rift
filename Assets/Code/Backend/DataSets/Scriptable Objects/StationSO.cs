using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewStation", menuName = "Game Data/Station")]
public class StationSO : ScriptableObject
{
    public string stationName;
    public ConceptSO allegiance;
    public string builtBy;
    public float shield;
    public ResistanceProfileSO shieldResistances;
    public float armor;
    public ResistanceProfileSO armorResistances;
    public BlueprintSO blueprint;

    public List<ModuleSO> modules = new List<ModuleSO>();
}