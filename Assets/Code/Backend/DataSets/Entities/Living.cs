public class Living : Entity
{
    public string DisplayName { get; set; }
    public bool IsPlayer { get; set; }
    // Other details like stats, attributes, etc.

    public Living(string name, bool isPlayer = false) : base(name, EntityType.LIVING)
    {
        IsPlayer = isPlayer;
    }
}
