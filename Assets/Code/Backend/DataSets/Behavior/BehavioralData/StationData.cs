using UnityEngine;

public class StationData : MonoBehaviour
{
    public StationSO data;

    public void Initialize(StationSO template, CelestialHierarchyNode node)
    {
        data = template;
    }
}
