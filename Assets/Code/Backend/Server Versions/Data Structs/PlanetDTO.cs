using System;
using System.Collections.Generic;

[Serializable]
public class PlanetDTO
{
    public string planetName;
    public List<MoonDTO> moons = new List<MoonDTO>();
    public List<ResourceDTO> resources = new List<ResourceDTO>();
}