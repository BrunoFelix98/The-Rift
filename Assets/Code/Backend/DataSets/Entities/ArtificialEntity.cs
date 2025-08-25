public abstract class ArtificialEntity : Entity
{
    public string BuiltBy { get; private set; }
    public BlueprintSO Blueprint { get; private set; }

    protected ArtificialEntity(string name, string builtBy, BlueprintSO blueprint = null)
        : base(name, EntityType.ARTIFICIAL)
    {
        BuiltBy = builtBy;
        Blueprint = blueprint;
    }
}
