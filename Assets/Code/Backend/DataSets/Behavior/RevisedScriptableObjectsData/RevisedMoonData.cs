using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "RevisedMoonData", menuName = "Universe/Revised Moon Data", order = 7)]
public class RevisedMoonData : ScriptableObject
{
    public string moonName;
    public bool hasStation;
    public CelestialEnvironment environmentType;
    public List<RevisedResourceData> resources = new();
    public float orbitRadius;
    public RevisedPlanetData parentPlanet;
}