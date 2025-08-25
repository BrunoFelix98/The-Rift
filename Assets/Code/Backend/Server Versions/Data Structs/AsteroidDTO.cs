using System;
using System.Collections.Generic;

[Serializable]
public class AsteroidDTO
{
    public string asteroidName;
    public List<ResourceDTO> resources = new List<ResourceDTO>();
}