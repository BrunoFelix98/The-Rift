using UnityEngine;

public class MoonData : MonoBehaviour
{
    public MoonSO data;

    public void Initialize(MoonSO template, CelestialHierarchyNode node)
    {
        data = template; 

        var visuals = GetComponent<CelestialBodyVisuals>();
        if (visuals == null) visuals = gameObject.AddComponent<CelestialBodyVisuals>();
        visuals.Apply(node.Params, isMoon: true);
    }
}
