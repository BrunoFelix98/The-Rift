public class Resource
{
    public ResourceSO resourceSO;   // Reference to static data; NOT mutable
    public int Quantity;            // Mutable quantity for this instance

    public string ResourceName => resourceSO.resourceName;
    public string ResourceDescription => resourceSO.resourceDescription;
    public float ResourceWeight => resourceSO.resourceWeight;

    public Resource(ResourceSO resourceSO, int startingQuantity)
    {
        this.resourceSO = resourceSO;
        this.Quantity = startingQuantity;
    }
}