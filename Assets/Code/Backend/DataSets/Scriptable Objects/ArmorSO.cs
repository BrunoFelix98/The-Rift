using UnityEngine;

[CreateAssetMenu(fileName = "NewArmor", menuName = "Game Data/Armor")]
public class ArmorSO : ScriptableObject
{
    public string armorName;
    public int defense;
    public int durability;
    public BlueprintSO blueprint;
}
