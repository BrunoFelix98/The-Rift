using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewModule", menuName = "Game Data/Module")]
public class ModuleSO : ScriptableObject
{
    public string moduleName;
    public List<SlotType> compatibleSlots = new List<SlotType>();
    public BlueprintSO blueprint;
    public ModuleCategory moduleCategory;
}