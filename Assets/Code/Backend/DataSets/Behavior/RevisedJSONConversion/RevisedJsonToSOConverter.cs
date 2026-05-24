#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RevisedJsonToSOConverter
{
    public static void LoadFactions(string jsonPath)
    {
        string json = File.ReadAllText(jsonPath);
        var wrapper = JsonUtility.FromJson<FactionWrapper>(json);

        foreach (var f in wrapper.factions)
        {
            var asset = ScriptableObject.CreateInstance<RevisedFactionData>();
            asset.factionName = f.factionName;
            asset.homeGalaxy = f.homeGalaxy;
            asset.corporations = f.corporations;
            asset.leaderNames = f.leaderNames;
            asset.starNames = f.starNames;
            asset.requiresNebulas = f.requiresNebulas;

            AssetDatabase.CreateAsset(asset, $"Assets/RuleBasedGeneratedGalaxy/Factions/{f.factionName}.asset");
        }
        AssetDatabase.SaveAssets();
    }

    [System.Serializable]
    private class FactionWrapper
    {
        public List<RevisedFactionDataJSON> factions;
    }

    [System.Serializable]
    private class RevisedFactionDataJSON
    {
        public string factionName;
        public string homeGalaxy;
        public List<string> corporations;
        public List<string> leaderNames;
        public List<string> starNames;
        public bool requiresNebulas;
    }
}
#endif
