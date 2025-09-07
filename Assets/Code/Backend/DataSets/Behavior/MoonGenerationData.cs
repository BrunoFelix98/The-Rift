using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MoonGenerationData : MonoBehaviour
{
    public string moonName;
    public CelestialEnvironment type;
    public OrbitParams orbitParams;
    public List<StationGenerationData> stations;
}