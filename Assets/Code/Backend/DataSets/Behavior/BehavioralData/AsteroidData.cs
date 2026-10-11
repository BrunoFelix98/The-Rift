using UnityEngine;

public class AsteroidData : MonoBehaviour
{
    public AsteroidSO data;

    public void Initialize(AsteroidSO template, CelestialHierarchyNode node)
    {
        data = template; 
    }
}
