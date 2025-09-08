using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSystem", menuName = "Game Data/System")]
public class SystemSO : ScriptableObject, IOrbitable, IHasPrefab
{
    public string systemName;
    public List<StarSO> stars = new List<StarSO>();
    public List<PlanetSO> planets = new List<PlanetSO>();
    public List<AsteroidBeltSO> asteroidBelts = new List<AsteroidBeltSO>();
    public List<GateSO> gates = new List<GateSO>();
    public List<StationSO> stations = new List<StationSO>();

    // Reference to nebula SO if this system is within or near a nebula
    public NebulaSO nebula;

    // Allegiance to a faction or concept, stored as string identifier
    public ConceptSO allegiance;
    public OrbitParams orbitParams;

    public GameObject prefabReference { get; set; }

    public void AssignAllegiance(ConceptSO allegiance)
    {
        if (this.allegiance == null)
        {
            this.allegiance = allegiance;
            Debug.Log($"Assigned allegiance {allegiance.name} to system {systemName}");
        }
    }

    public OrbitParams GetOrbitParams()
    {
        return orbitParams;
    }
    public void InitializeOrbit(OrbitParams orbitParams) => this.orbitParams = orbitParams;
}
