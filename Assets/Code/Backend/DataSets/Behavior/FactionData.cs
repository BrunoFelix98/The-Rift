using System;
using System.Collections.Generic;

[Serializable]
public class FactionData
{
    public string factionName;
    public string homeGalaxy;
    public List<string> corporations;
    public List<string> leaderNames;
    public List<string> starNames;
    public bool requiresNebulas;
}