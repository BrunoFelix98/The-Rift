using UnityEngine;

public class GateData : MonoBehaviour
{
    public GateSO data;
    public CelestialHierarchyNode Node { get; private set; }

    public void Initialize(GateSO template, CelestialHierarchyNode node)
    {
        data = template; 
        Node = node;
    }

    public ulong GetDestinationSystemId()
    {
        if (Node == null || Node.Data == null) return 0;
        
        ulong targetStarId = Node.Data.destinationId;
        if (targetStarId == 0) return 0;

        // Find which system owns this target star
        if (SystemCenteredVisualizer.Instance != null && SystemCenteredVisualizer.Instance.universeMap != null)
        {
            foreach (var galaxy in SystemCenteredVisualizer.Instance.universeMap.galaxies)
            {
                foreach (var system in galaxy.systems)
                {
                    // If any celestial in this system is our target star, return this system's ID
                    if (system.celestials.Exists(c => c.orbit.NetworkId == targetStarId))
                    {
                        return system.id;
                    }
                }
            }
        }
        return 0;
    }
}
