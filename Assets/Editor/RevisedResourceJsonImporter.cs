#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public static class RevisedResourceJsonImporter
{
    [System.Serializable]
    private class ResourceWrapper
    {
        public List<RevisedResourceJsonData> resources;
    }

    [System.Serializable]
    private class RevisedResourceJsonData
    {
        public string resourceName;
        public string resourceDescription;
        public int minQuantity;
        public int maxQuantity;
        public float resourceWeight;
        public List<string> appliesTo;
        public List<string> environment;
        public List<string> allowedFactions;
        public string rarity;
    }

    [MenuItem("Tools/Universe/Import Resources JSON")]
    public static void ImportResourcesFromJson()
    {
        string path = EditorUtility.OpenFilePanel("Select Resources JSON", "Assets/Code/Backend/DataSets/Behavior/JSONData/ResourcesJSON.json", "json");
        if (string.IsNullOrEmpty(path)) return;

        string json = File.ReadAllText(path);
        var wrapper = JsonUtility.FromJson<ResourceWrapper>(json);

        if (wrapper?.resources == null || wrapper.resources.Count == 0)
        {
            Debug.LogWarning("No resources found in JSON file!");
            return;
        }

        string outputDir = "Assets/RuleBasedGalaxyGenerator/Resources";
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        foreach (var res in wrapper.resources)
        {
            var so = ScriptableObject.CreateInstance<RevisedResourceData>();

            // Basic info
            so.resourceName = res.resourceName;
            so.resourceDescription = res.resourceDescription;
            so.minQuantity = res.minQuantity;
            so.maxQuantity = res.maxQuantity;
            so.resourceWeight = res.resourceWeight;
            so.allowedFactions = res.allowedFactions ?? new List<string>();

            // Rarity
            if (System.Enum.TryParse(res.rarity, true, out ResourceRarity parsedRarity))
                so.rarity = parsedRarity;
            else
                so.rarity = ResourceRarity.COMMON;

            // AppliesTo TO CelestialType
            so.appliesTo = new List<CelestialType>();
            if (res.appliesTo != null)
            {
                foreach (var t in res.appliesTo)
                {
                    if (System.Enum.TryParse(t, true, out CelestialType ct))
                        so.appliesTo.Add(ct);
                }
            }

            // Environment TO EnvironmentType
            so.environment = new List<CelestialEnvironment>();
            if (res.environment != null)
            {
                foreach (var env in res.environment)
                {
                    if (System.Enum.TryParse(env, true, out CelestialEnvironment et))
                        so.environment.Add(et);
                }
            }

            string safeName = MakeSafeFileName(res.resourceName);
            string assetPath = $"{outputDir}/{safeName}.asset";

            AssetDatabase.CreateAsset(so, assetPath);
            Debug.Log($"Created ResourceData: {so.resourceName}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Resource import complete!");
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Replace(" ", "_");
    }
}
#endif
