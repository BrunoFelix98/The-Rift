public class Weapon : Equipment
{
    public int Damage { get; private set; }
    public float Range { get; private set; }
    public DamageType DamageType { get; private set; }
    public BlueprintSO Blueprint { get; private set; }

    public Weapon(string name, int damage, float range, DamageType damageType, BlueprintSO blueprint = null)
        : base(name)
    {
        Damage = damage;
        Range = range;
        DamageType = damageType;
        Blueprint = blueprint;
    }

    public override string ToString()
    {
        return $"{EntityName} (Weapon) - Damage: {Damage}, Range: {Range}, DamageType: {DamageType}";
    }
}
