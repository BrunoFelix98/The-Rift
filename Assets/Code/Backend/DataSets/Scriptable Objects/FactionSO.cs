using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GatePair
{
    public string fromStar;
    public string toStar;
}

[Serializable]
public class CorporationData
{
    public string corporationName;
    public LivingSO leader;
}

[CreateAssetMenu(fileName = "NewFaction", menuName = "Game Data/Faction")]
public class FactionSO : ScriptableObject
{
    public int factionId;
    public string factionName;
    public string homeGalaxy;
    
    public List<CorporationData> corporations = new List<CorporationData>();
    public List<string> starNames = new List<string>();
    
    public bool requiresNebulas;
    public Color factionColor;
    public int seed;
    
    public List<GatePair> gates = new List<GatePair>();

    [Header("Main Leader")]
    public LivingSO factionLeader;
}
