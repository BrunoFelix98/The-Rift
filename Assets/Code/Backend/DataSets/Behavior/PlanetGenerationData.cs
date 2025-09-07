using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlanetGenerationData : MonoBehaviour
{
    public string planetName;
    public CelestialEnvironment type;
    public OrbitParams orbitParams;
    public List<MoonGenerationData> moons;
}