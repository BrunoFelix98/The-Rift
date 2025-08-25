using System;
using System.Collections.Generic;

[Serializable]
public class AsteroidBeltDTO
{
    public string beltName;
    public List<AsteroidDTO> asteroids = new List<AsteroidDTO>();
}