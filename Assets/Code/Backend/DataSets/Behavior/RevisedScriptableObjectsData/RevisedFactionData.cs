using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "RevisedFactionData", menuName = "Universe/Revised Faction Data", order = 2)]
public class RevisedFactionData : ScriptableObject
{
    public int factionId;
    public string factionName;
    public string homeGalaxy;
    public List<string> corporations = new();
    public List<string> leaderNames = new();
    public List<string> starNames = new();
    public bool requiresNebulas;
    public string factionColor;
    public uint seed;
}