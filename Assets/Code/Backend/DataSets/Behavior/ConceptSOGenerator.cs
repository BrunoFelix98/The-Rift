#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class ConceptSOGenerator
{
    public ConceptSO baseConceptSO; // Optional base template for shared defaults
    public LivingSO baseLeaderLivingSO; // Optional living entity to assign as leader placeholder
    public string outputFolder;  // Folder to save ConceptSO assets
    public LivingSO leaderTemplate;
    private Dictionary<string, ConceptSO> factionConceptDict = new Dictionary<string, ConceptSO>();

    // Create ConceptSO assets from FactionData list
    public void CreateConceptsFromFactions(List<FactionData> factions)
    {
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        foreach (var faction in factions)
        {
            if (faction.corporations == null || faction.corporations.Count == 0)
                continue;

            // Create the alliance lead
            string mainAllianceName = faction.corporations[0];
            string allianceFolder = Path.Combine(outputFolder, SanitizeFileName(mainAllianceName));
            CreateFoldersRecursively(allianceFolder);
            ConceptSO allianceConcept = FindOrCreateConceptSO(mainAllianceName, faction.factionName, allianceFolder);

            // Create member corporations and assign allegiance
            CreateMemberCorporations(faction, allianceConcept, allianceFolder);

            // Assign leaders to the alliance and member corporations
            AssignLeadersToCorporations(faction, allianceConcept);

            // Store alliance lead concept in dictionary keyed by factionName
            if (!factionConceptDict.ContainsKey(faction.factionName))
                factionConceptDict[faction.factionName] = allianceConcept;
            EditorUtility.SetDirty(allianceConcept);
        }
        AssetDatabase.SaveAssets();
    }

    public Dictionary<string, ConceptSO> GetFactionConceptDictionary()
    {
        return factionConceptDict;
    }

    private void CreateMemberCorporations(FactionData faction, ConceptSO allianceConcept, string allianceFolder)
    {
        for (int i = 1; i < faction.corporations.Count; i++)
        {
            string corpName = faction.corporations[i];
            ConceptSO corpConcept = FindOrCreateConceptSO(corpName, faction.factionName, allianceFolder);

            // Ensure allegiance points to the allianceConcept
            corpConcept.allegiance = allianceConcept;
            EditorUtility.SetDirty(corpConcept);

            // Keep the list in sync
            if (!allianceConcept.memberCorporations.Contains(corpConcept))
                allianceConcept.memberCorporations.Add(corpConcept);

            EditorUtility.SetDirty(allianceConcept);
        }
    }

    private void AssignLeadersToCorporations(FactionData faction, ConceptSO allianceConcept)
    {
        if (faction.leaderNames == null || faction.leaderNames.Count == 0)
            return;

        // Assign the first leader to the alliance lead
        LivingSO leader = FindOrCreateLiving(faction.leaderNames[0], faction.factionName, allianceConcept, Path.Combine(outputFolder, "Leaders"));
        allianceConcept.leader = leader;

        // Add the alliance leader to the members list if not already present
        if (!allianceConcept.members.Contains(leader))
            allianceConcept.members.Add(leader);

        EditorUtility.SetDirty(allianceConcept);

        // Assign remaining leaders to member corporations
        for (int i = 1; i < faction.leaderNames.Count && i < allianceConcept.memberCorporations.Count + 1; i++)
        {
            LivingSO memberLeader = FindOrCreateLiving(faction.leaderNames[i], faction.factionName, allianceConcept, Path.Combine(outputFolder, "Leaders"));
            var corpConcept = allianceConcept.memberCorporations[i - 1];

            corpConcept.leader = memberLeader;

            // Add each leader to their own member list if not present
            if (!corpConcept.members.Contains(memberLeader))
                corpConcept.members.Add(memberLeader);

            // Ensure allegiance
            corpConcept.allegiance = allianceConcept;

            EditorUtility.SetDirty(corpConcept);
        }
    }

    private ConceptSO FindOrCreateConceptSO(string conceptName, string factionDescription, string folder)
    {
        string assetPath = Path.Combine(folder, $"{SanitizeFileName(conceptName)}.asset");
        ConceptSO existing = AssetDatabase.LoadAssetAtPath<ConceptSO>(assetPath);
        if (existing != null)
            return existing;

        ConceptSO conceptSO = ScriptableObject.CreateInstance<ConceptSO>();
        conceptSO.conceptName = conceptName;
        conceptSO.description = $"Created for faction: {factionDescription}";
        conceptSO.memberCorporations = new List<ConceptSO>();
        conceptSO.members = new List<LivingSO>();
        conceptSO.allegiance = null;
        AssetDatabase.CreateAsset(conceptSO, assetPath);
        EditorUtility.SetDirty(conceptSO);
        return conceptSO;
    }

    private LivingSO FindOrCreateLiving(string leaderName, string factionName, ConceptSO allegiance, string folder)
    {
        CreateFoldersRecursively(folder);
        string safeName = SanitizeFileName(leaderName);
        string assetPath = Path.Combine(folder, safeName + ".asset").Replace("\\", "/");
        LivingSO existing = AssetDatabase.LoadAssetAtPath<LivingSO>(assetPath);
        if (existing != null)
            return existing;

        LivingSO newLiving = ScriptableObject.CreateInstance<LivingSO>();
        newLiving.name = safeName;
        newLiving.displayName = leaderName;
        newLiving.allegiance = allegiance;
        AssetDatabase.CreateAsset(newLiving, assetPath);
        EditorUtility.SetDirty(newLiving);
        return newLiving;
    }

    // Utility to create all needed folders recursively
    private void CreateFoldersRecursively(string path)
    {
        path = path.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] split = path.Split('/');
        string curr = split[0];
        for (int i = 1; i < split.Length; i++)
        {
            string next = curr + "/" + split[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(curr, split[i]);
            curr = next;
        }
    }

    private string SanitizeFileName(string input)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var c in input)
            sb.Append(System.Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);
        return sb.ToString();
    }
}
#endif