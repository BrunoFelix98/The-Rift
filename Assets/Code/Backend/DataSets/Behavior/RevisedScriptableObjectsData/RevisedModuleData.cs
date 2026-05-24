using UnityEngine;

[CreateAssetMenu(fileName = "RevisedModuleData", menuName = "Universe/Modules/Revised Module Data", order = 2)]
public class RevisedModuleData : ScriptableObject
{
    [Header("Module Identity")]
    public string moduleName;
    [TextArea] public string moduleDescription;

    [Header("Category & Blueprint")]
    public ModuleCategory moduleCategory; //What kind of module it is
    public SlotCategory slotCategory; //Where it can be fitted

    [Header("Blueprint & Crafting")]
    public RevisedBlueprintData craftingBlueprint; // null for NPC-only or default modules

    [Header("Performance Stats (Optional)")]
    public float fuelUsage;
    public float cpuUsage;
    public float powerGridUsage;
}
