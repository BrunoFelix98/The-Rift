using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RevisedSlot", menuName = "Universe/Revised Slot", order = 7)]
public class RevisedSlotData : ScriptableObject
{
    [Header("Module Identity")]
    public string slotName;
    [TextArea] public string slotDescription;

    [Header("Slot Classification")]
    public SlotCategory slotCategory; // Market, Utility, Defense, etc.

    [Header("Compatibility")]
    public List<ModuleCategory> allowedModules; // what kind of modules can fit here
}