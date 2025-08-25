[System.Serializable]
public class Entity
{
    //Static counter to keep track of how many entities have been created
    private static int nextID = 0;

    //Instance fields
    private int entityID;
    private string entityName;
    private EntityType entityType;

    //Public read-only properties
    public int EntityID => entityID;
    public string EntityName => entityName;
    public EntityType EntityType => entityType;

    //Constructor
    public Entity(string name, EntityType type)
    {
        entityID = ++nextID; // Auto-increment ID for each new entity
        entityName = name;
        entityType = type;
    }

    //Optional: nice string representation
    public override string ToString()
    {
        return $"[{EntityID}] {EntityName} ({EntityType})";
    }
}
