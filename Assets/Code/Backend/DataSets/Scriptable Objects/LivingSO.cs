using NUnit.Framework;
using UnityEngine;

[CreateAssetMenu(fileName = "NewLiving", menuName = "Game Data/Living")]
public class LivingSO : ScriptableObject
{
    public string displayName;
    [TextArea]
    public string bio;

    public bool isPlayer;

    public int hitpoints;
    public int movementSpeed;
    public int carryWeight;
    public ModuleSO weapon;
    public ModuleSO armor;
    public ConceptSO allegiance;

    // Additional fields for stats, appearance, etc. can be added here
}
