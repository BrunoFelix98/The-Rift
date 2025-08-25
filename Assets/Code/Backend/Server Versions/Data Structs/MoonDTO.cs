using System;
using System.Collections.Generic;

[Serializable]
public class MoonDTO
{
    public string moonName;
    public List<ResourceDTO> resources = new List<ResourceDTO>();
    public List<StationDTO> stations = new List<StationDTO>();
}