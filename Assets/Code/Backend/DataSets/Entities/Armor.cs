public class Armor : Equipment
{
    public int Defense { get; private set; }
    public int Durability { get; private set; }
    public BlueprintSO Blueprint { get; private set; }

    public Armor(string name, int defense, int durability, BlueprintSO blueprint = null)
        : base(name)
    {
        Defense = defense;
        Durability = durability;
        Blueprint = blueprint;
    }

    public override string ToString()
    {
        return $"{EntityName} (Armor) - Defense: {Defense}, Durability: {Durability}";
    }
}
