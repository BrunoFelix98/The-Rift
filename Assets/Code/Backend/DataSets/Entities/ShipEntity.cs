using UnityEngine;

public class ShipEntity : MonoBehaviour
{
    public ShipSO shipData;

    // Your existing Entity instance
    private Entity entity;

    // Accessors for entity data
    public int EntityID => entity.EntityID;
    public string EntityName => entity.EntityName;
    public EntityType EntityType => entity.EntityType;

    // Initialize method to create the Entity instance
    public void Initialize(string name, EntityType type)
    {
        entity = new Entity(name, type);
        // Additional initialization logic here, e.g., load ShipSO data
        Debug.Log($"Initialized ShipEntity: {entity}");
    }
}