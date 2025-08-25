using System;
using System.Collections.Generic;
using Unity.VisualScripting;

[Serializable]
public class SystemDTO
{
    public string systemName;
    public List<PlanetDTO> planets = new List<PlanetDTO>();
    public List<StarDTO> stars = new List<StarDTO>();
    public List<StationDTO> stations = new List<StationDTO>();
    public List<GateDTO> gates = new List<GateDTO>();
    public List<AsteroidBeltDTO> asteroidBelts = new List<AsteroidBeltDTO>();
    public string allegianceId;
    public NebulaDTO nebula;
}