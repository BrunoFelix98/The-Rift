using UnityEngine;

public class PlanetData : MonoBehaviour
{
    public PlanetSO data;

    public void Initialize(PlanetSO template, CelestialHierarchyNode node)
    {
        data = template; 

        var visuals = GetComponent<CelestialBodyVisuals>();
        if (visuals == null) visuals = gameObject.AddComponent<CelestialBodyVisuals>();
        visuals.Apply(node.Params, isMoon: false);
    }
}
