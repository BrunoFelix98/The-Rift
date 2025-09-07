using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewPlanet", menuName = "Game Data/Planet")]
public class PlanetSO : ScriptableObject
{
    public string planetName;
    public List<MoonSO> moons = new List<MoonSO>();
    public List<ResourceSO> resources = new List<ResourceSO>();
    public CelestialEnvironment type;
    public GameObject prefabReference;
    public OrbitParams orbitParams;
}