using System;

[Serializable]
public class LivingDTO
{
    public string displayName;
    public string bio;
    public bool isPlayer;
    public int hitpoints;
    public int movementSpeed;
    public int carryWeight;

    public string weaponId;      // reference to ModuleDTO by ID
    public string armorId;       // reference to ModuleDTO by ID
    public string allegianceId;  // reference to ConceptDTO by ID

    // Add any additional fields as needed
}