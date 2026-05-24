using UnityEngine;

public class PlanetData : MonoBehaviour
{
    public PlanetSO data;

    public void Initialize(PlanetSO template, CelestialHierarchyNode node)
    {
        data = template; 
        GetComponent<NetworkedPlanetData>()?.Reinitialize(template, node);
    }
}
