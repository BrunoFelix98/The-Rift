using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewPlanet", menuName = "Game Data/Planet")]
public class PlanetSO : ScriptableObject, IOrbitable, IHasPrefab
{
    public string planetName;
    public List<MoonSO> moons = new List<MoonSO>();
    public List<ResourceSO> resources = new List<ResourceSO>();
    public CelestialEnvironment type;
    public OrbitParams orbitParams;

    public GameObject prefabReference { get; set; }

    public OrbitParams GetOrbitParams()
    {
        return orbitParams;
    }

    public void InitializeOrbit(OrbitParams orbitParams) => this.orbitParams = orbitParams;
}