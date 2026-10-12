#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class OrganizationSOGenerator
{
    public OrganizationSO baseOrganizationSO; 
    public LivingSO baseLeaderLivingSO; 
    public string outputFolder;  
    public LivingSO leaderTemplate;
    private Dictionary<string, OrganizationSO> factionOrganizationDict = new Dictionary<string, OrganizationSO>();

    public void CreateOrganizationsFromFactions(List<FactionData> factions)
    {
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        foreach (var faction in factions)
        {
            if (faction.corporations == null || faction.corporations.Count == 0)
                continue;

            string mainAllianceName = faction.corporations[0];
            string allianceFolder = Path.Combine(outputFolder, SanitizeFileName(mainAllianceName));
            CreateFoldersRecursively(allianceFolder);
            OrganizationSO allianceOrg = FindOrCreateOrganizationSO(mainAllianceName, faction.factionName, allianceFolder, OrganizationType.Alliance);

            CreateMemberCorporations(faction, allianceOrg, allianceFolder);

            AssignLeadersToCorporations(faction, allianceOrg);

            if (!factionOrganizationDict.ContainsKey(faction.factionName))
                factionOrganizationDict[faction.factionName] = allianceOrg;
            EditorUtility.SetDirty(allianceOrg);
        }
        AssetDatabase.SaveAssets();
    }

    public Dictionary<string, OrganizationSO> GetFactionOrganizationDictionary()
    {
        return factionOrganizationDict;
    }

    private void CreateMemberCorporations(FactionData faction, OrganizationSO allianceOrg, string allianceFolder)
    {
        for (int i = 1; i < faction.corporations.Count; i++)
        {
            string corpName = faction.corporations[i];
            OrganizationSO corpOrg = FindOrCreateOrganizationSO(corpName, faction.factionName, allianceFolder, OrganizationType.Corporation);

            corpOrg.allegiance = allianceOrg;
            EditorUtility.SetDirty(corpOrg);

            if (!allianceOrg.memberOrganizations.Contains(corpOrg))
                allianceOrg.memberOrganizations.Add(corpOrg);

            EditorUtility.SetDirty(allianceOrg);
        }
    }

    private void AssignLeadersToCorporations(FactionData faction, OrganizationSO allianceOrg)
    {
        if (faction.leaderNames == null || faction.leaderNames.Count == 0)
            return;

        LivingSO leader = FindOrCreateLiving(faction.leaderNames[0], faction.factionName, allianceOrg, Path.Combine(outputFolder, "Leaders"));
        allianceOrg.leader = leader;

        if (!allianceOrg.members.Contains(leader))
            allianceOrg.members.Add(leader);

        EditorUtility.SetDirty(allianceOrg);

        for (int i = 1; i < faction.leaderNames.Count && i < allianceOrg.memberOrganizations.Count + 1; i++)
        {
            LivingSO memberLeader = FindOrCreateLiving(faction.leaderNames[i], faction.factionName, allianceOrg, Path.Combine(outputFolder, "Leaders"));
            var corpOrg = allianceOrg.memberOrganizations[i - 1];

            corpOrg.leader = memberLeader;

            if (!corpOrg.members.Contains(memberLeader))
                corpOrg.members.Add(memberLeader);

            corpOrg.allegiance = allianceOrg;
            EditorUtility.SetDirty(corpOrg);
        }
    }

    private OrganizationSO FindOrCreateOrganizationSO(string name, string factionDescription, string folder, OrganizationType type)
    {
        EnsureFolder(folder);

        string assetPath = Path.Combine(folder, $"{SanitizeFileName(name)}.asset").Replace("\\", "/");
        OrganizationSO existing = AssetDatabase.LoadAssetAtPath<OrganizationSO>(assetPath);
        if (existing != null)
            return existing;

        OrganizationSO orgSO = ScriptableObject.CreateInstance<OrganizationSO>();
        orgSO.organizationName = name;
        orgSO.type = type;
        orgSO.description = $"Created for faction: {factionDescription}";
        orgSO.memberOrganizations = new List<OrganizationSO>();
        orgSO.members = new List<LivingSO>();
        orgSO.allegiance = null;
        AssetDatabase.CreateAsset(orgSO, assetPath);
        EditorUtility.SetDirty(orgSO);
        return orgSO;
    }

    private LivingSO FindOrCreateLiving(string leaderName, string factionName, OrganizationSO allegiance, string folder)
    {
        EnsureFolder(folder);

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

    private void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return;
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parentFolder = Path.GetDirectoryName(folder);
        string newFolderName = Path.GetFileName(folder);

        if (!string.IsNullOrEmpty(parentFolder) && !string.IsNullOrEmpty(newFolderName))
        {
            EnsureFolder(parentFolder); 
            AssetDatabase.CreateFolder(parentFolder, newFolderName);
        }
    }
}
#endif