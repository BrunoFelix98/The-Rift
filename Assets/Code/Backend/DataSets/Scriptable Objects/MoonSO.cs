using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMoon", menuName = "Game Data/Moon")]
public class MoonSO : ScriptableObject, IOrbitable, IHasPrefab
{
    public string moonName;
    public List<ResourceSO> resources = new List<ResourceSO>();
    public List<StationSO> stations = new List<StationSO>();
    public CelestialEnvironment type;
    public OrbitParams orbitParams;

    public GameObject prefabReference { get; set; }

    public OrbitParams GetOrbitParams()
    {
        return orbitParams;
    }
    public void InitializeOrbit(OrbitParams orbitParams) => this.orbitParams = orbitParams;
}
