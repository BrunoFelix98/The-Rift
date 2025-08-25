public class ShipRuntimeData
{
    public string shipName;
    public string builtBy;
    public string currentOwner;
    public ShipWeightClass weightClass;
    public float shieldHP;
    public ResistanceProfileSO shieldResistances;
    public float armorHP;
    public ResistanceProfileSO armorResistances;
    public float speed;
    public float tonnage;
    public float cargoCapacity;
    public float longAxisLength;
    public BlueprintSO blueprint;

    // You can add runtime-specific properties here,
    // e.g., current shield HP, armor HP, cooldown timers, etc.

    public ShipRuntimeData(ShipSO so)
    {
        shipName = so.shipName;
        builtBy = so.builtBy;
        currentOwner = so.currentOwner;
        weightClass = so.weightClass;
        shieldHP = so.shieldHP;
        shieldResistances = so.shieldResistances;
        armorHP = so.armorHP;
        armorResistances = so.armorResistances;
        speed = so.speed;
        tonnage = so.tonnage;
        cargoCapacity = so.cargoCapacity;
        longAxisLength = so.longAxisLength;
        blueprint = so.blueprint;
    }
}
