using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "RevisedPlanetData", menuName = "Universe/Revised Planet Data", order = 6)]
public class RevisedPlanetData : ScriptableObject
{
    public string planetName;
    public CelestialEnvironment environmentType;
    public float orbitRadius;
    public float orbitSpeed;
    public List<RevisedResourceData> resources = new();
    public List<RevisedMoonData> moons = new();
}