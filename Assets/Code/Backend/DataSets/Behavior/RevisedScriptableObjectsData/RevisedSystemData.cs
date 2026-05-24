using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "RevisedSystemData", menuName = "Universe/Revised System Data", order = 4)]
public class RevisedSystemData : ScriptableObject
{
    public string systemName;
    
    [Header("Ownership")]
    public RevisedFactionData owningFaction;

    [Header("Celestial Structure")]
    public bool hasNebula;
    public RevisedNebulaData nebulaData;
    public List<RevisedStarData> stars = new();
    public List<RevisedPlanetData> planets = new();
    public List<RevisedAsteroidBeltData> asteroidBelts = new();

    [Header("Infrastructure")]
    public List<RevisedStationData> stations = new();

    [Header("Gate Network")]
    public List<RevisedGateData> gates = new();
}