using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SystemGenerationData : MonoBehaviour
{
    public string systemName;
    public OrbitParams orbitParams;
    public ConceptSO allegiance;
    public List<StarGenerationData> stars;
    public List<PlanetGenerationData> planets;
    public List<AsteroidBeltGenerationData> asteroidBelts;
    public List<GateGenerationData> gates;
    public List<StationGenerationData> stations;
    public bool requiresNebula;
}