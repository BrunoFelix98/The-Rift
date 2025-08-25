using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMoon", menuName = "Game Data/Moon")]
public class MoonSO : ScriptableObject
{
    public string moonName;
    public List<ResourceSO> resources = new List<ResourceSO>();
    public List<StationSO> stations = new List<StationSO>();
    public CelestialType type;
}
