public class Gate : Entity
{
    public SystemEntity ConnectedSystem { get; private set; }

    public Gate(string name, SystemEntity targetSystem) : base(name, EntityType.ARTIFICIAL)
    {
        ConnectedSystem = targetSystem;
    }
}
