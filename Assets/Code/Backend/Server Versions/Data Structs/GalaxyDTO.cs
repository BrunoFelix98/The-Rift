using System;
using System.Collections.Generic;

[Serializable]
public class GalaxyDTO
{
    public string galaxyName;
    public List<SystemDTO> systems = new List<SystemDTO>();
}