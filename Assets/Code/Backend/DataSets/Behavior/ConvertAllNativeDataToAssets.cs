using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

public static class ConvertAllNativeDataToAssets
{
    public static void Run(
        NativeList<GalaxyGenerationData> galaxies,
        NativeList<SystemGenerationData> systems,
        NativeList<StarGenerationData> stars,
        NativeList<PlanetGenerationData> planets,
        NativeList<MoonGenerationData> moons,
        NativeList<AsteroidBeltGenerationData> belts,
        NativeList<AsteroidGenerationData> asteroids,
        NativeList<StationGenerationData> stations,
        NativeList<GateGenerationData> gates,
        Dictionary<int, ConceptSO> idToConceptSO,
        Dictionary<int, NebulaSO> idToNebulaSO,
        List<ResourceData> planetResources,
        List<ResourceData> moonResources,
        List<ResourceData> asteroidResources,
        string outputFolder,
        string prefabFolder)
    {
        EnsureFolder(outputFolder);
        EnsureFolder(prefabFolder);

        for (int gi = 0; gi < galaxies.Length; gi++)
        {
            var g = galaxies[gi];
            var galaxyPath = $"{outputFolder}/{g.galaxyName.ToString()}";
            EnsureFolder(galaxyPath);

            // Create GalaxySO
            var galaxySO = ScriptableObject.CreateInstance<GalaxySO>();
            galaxySO.galaxyName = g.galaxyName.ToString();
            galaxySO.orbitParams = g.orbitParams;
            galaxySO.systems = new List<SystemSO>();

            // For each system
            for (int si = g.systemListStartIndex; si < g.systemListStartIndex + g.systemCount; si++)
            {
                var sd = systems[si];
                var sysFolder = $"{galaxyPath}/{sd.systemName.ToString()}";
                EnsureFolder(sysFolder);

                var systemSO = ScriptableObject.CreateInstance<SystemSO>();
                systemSO.systemName = sd.systemName.ToString();
                systemSO.orbitParams = sd.orbitParams;
                if (!idToConceptSO.TryGetValue(sd.allegianceID, out var concept))
                    concept = null;
                systemSO.allegiance = concept;
                systemSO.stars = new List<StarSO>();
                systemSO.planets = new List<PlanetSO>();
                systemSO.asteroidBelts = new List<AsteroidBeltSO>();
                systemSO.gates = new List<GateSO>();
                systemSO.stations = new List<StationSO>();

                if (sd.requiresNebula && idToNebulaSO.TryGetValue(sd.allegianceID, out var factionNeb) && factionNeb != null)
                    systemSO.nebula = factionNeb;
                else if (sd.requiresNebula && idToNebulaSO.TryGetValue(sd.nebulaID, out var randomNeb) && randomNeb != null)
                    systemSO.nebula = randomNeb;

                bool hasFaction = systemSO.allegiance != null;

                // Save system asset
                AssetDatabase.CreateAsset(systemSO, $"{sysFolder}/{systemSO.systemName}.asset");

                // Stars
                for (int sti = sd.starsListStartIndex; sti < sd.starsListStartIndex + sd.starCount; sti++)
                {
                    var st = stars[sti];
                    var starSO = CreateChildAsset<StarSO>("Stars", st.starName.ToString(), st.orbitParams, sysFolder, prefabFolder, outputFolder);
                    systemSO.stars.Add(starSO);
                }

                // Nebula
                if (systemSO.nebula != null)
                {
                    var nebulaPrefab = CreatePrefabForSO(systemSO.nebula, sysFolder, null, null, null, prefabFolder, outputFolder);
                    systemSO.nebula.prefabReference = nebulaPrefab;
                }

                // Planets & moons
                for (int pi = sd.planetsListStartIndex; pi < sd.planetsListStartIndex + sd.planetCount; pi++)
                {
                    var pd = planets[pi];
                    var planetSO = CreateChildAsset<PlanetSO>("Planets", pd.planetName.ToString(), pd.orbitParams, sysFolder, prefabFolder, outputFolder, planetResources);
                    planetSO.type = GetRandomEnvironment();
                    planetSO.moons = new List<MoonSO>();
                    systemSO.planets.Add(planetSO);

                    for (int mi = pd.moonListStartIndex; mi < pd.moonListStartIndex + pd.moonCount; mi++)
                    {
                        var md = moons[mi];
                        var moonSO = CreateChildAsset<MoonSO>("Planets/" + planetSO.name + "/Moons", md.moonName.ToString(), md.orbitParams, sysFolder, prefabFolder, outputFolder, null, moonResources);
                        moonSO.type = GetRandomEnvironment();
                        planetSO.moons.Add(moonSO);
                    }
                }

                // Belts & asteroids
                for (int bi = sd.asteroidBeltsListStartIndex; bi < sd.asteroidBeltsListStartIndex + sd.asteroidBeltCount; bi++)
                {
                    var bd = belts[bi];
                    var beltSO = CreateChildAsset<AsteroidBeltSO>("Belts", bd.asteroidBeltName.ToString(), bd.orbitParams, sysFolder, prefabFolder, outputFolder);
                    beltSO.asteroids = new List<AsteroidSO>();
                    systemSO.asteroidBelts.Add(beltSO);

                    for (int ai = bd.asteroidListStartIndex; ai < bd.asteroidListStartIndex + bd.asteroidCount; ai++)
                    {
                        var ad = asteroids[ai];
                        var astSO = CreateChildAsset<AsteroidSO>("Belts/" + beltSO.name + "/Asteroids", ad.asteroidName.ToString(), ad.orbitParams, sysFolder, prefabFolder, outputFolder, null, null, asteroidResources, beltSO.type);
                        beltSO.asteroids.Add(astSO);
                    }
                }

                // Gates
                for (int gi2 = sd.gatesListStartIndex; gi2 < sd.gatesListStartIndex + sd.gateCount; gi2++)
                {
                    var gd = gates[gi2];
                    var gateSO = CreateChildAsset<GateSO>("Gates", gd.gateName.ToString(), gd.orbitParams, sysFolder, prefabFolder, outputFolder);
                    systemSO.gates.Add(gateSO);
                }

                // Stations
                for (int sti2 = sd.stationsListStartIndex; sti2 < sd.stationsListStartIndex + sd.stationCount; sti2++)
                {
                    var sd2 = stations[sti2];
                    var stationSO = CreateChildAsset<StationSO>("Stations", sd2.stationName.ToString(), sd2.orbitParams, sysFolder, prefabFolder, outputFolder);
                    stationSO.allegiance = systemSO.allegiance;

                    var parentId = sd2.orbitParams.ParentId;
                    bool attachedToMoon = false;

                    foreach (var planet in systemSO.planets)
                    {
                        foreach (var moon in planet.moons)
                        {
                            if (moon.GetOrbitParams().NetworkId == parentId)
                            {
                                if (moon.stations == null)
                                    moon.stations = new List<StationSO>();

                                moon.stations.Add(stationSO);
                                attachedToMoon = true;
                                break;
                            }
                        }
                        if (attachedToMoon) break;
                    }

                    systemSO.stations.Add(stationSO);

                    // Optional: warn if a station wasn’t attached to any moon
                    if (!attachedToMoon)
                    {
                        Debug.LogWarning(
                            $"Station '{stationSO.name}' in system '{systemSO.systemName}' " +
                            $"had no matching moon for ParentId={parentId}. Added only to system list."
                        );
                    }
                }

                if (systemSO.allegiance == null && systemSO.stations.Count > 3)
                    systemSO.stations = systemSO.stations.Take(3).ToList();

                // Prefab
                var tempSysGO = new GameObject(systemSO.systemName);
                var compSys = tempSysGO.AddComponent<SystemData>();
                compSys.data = systemSO; // your MonoBehaviour wrapper
                var prefabSys = PrefabUtility.SaveAsPrefabAsset(tempSysGO, $"{sysFolder}/{systemSO.systemName}.prefab");
                UnityEngine.Object.DestroyImmediate(tempSysGO);
                systemSO.prefabReference = prefabSys;

                galaxySO.systems.Add(systemSO);
                EditorUtility.SetDirty(systemSO);
                EditorUtility.SetDirty(galaxySO);
            }

            // Save galaxy asset
            AssetDatabase.CreateAsset(galaxySO, $"{galaxyPath}/{galaxySO.galaxyName}.asset");

            // Galaxy prefab
            var tempGO = new GameObject(galaxySO.galaxyName);
            var comp = tempGO.AddComponent<GalaxyData>();
            comp.data = galaxySO; // your MonoBehaviour wrapper
            var prefab = PrefabUtility.SaveAsPrefabAsset(tempGO, $"{galaxyPath}/{galaxySO.galaxyName}.prefab");
            UnityEngine.Object.DestroyImmediate(tempGO);
            galaxySO.prefabReference = prefab;

            EditorUtility.SetDirty(galaxySO);
        }

        AssetDatabase.SaveAssets();
    }

    private static CelestialEnvironment GetRandomEnvironment()
    {
        float roll = UnityEngine.Random.value; // 0..1
        if (roll < 0.35f)
            return CelestialEnvironment.ROCKY;
        else if (roll < 0.65f)
            return CelestialEnvironment.TEMPERATE;
        else if (roll < 0.80f)
            return CelestialEnvironment.ICE;
        else
            return CelestialEnvironment.GAS;
    }

    static T CreateChildAsset<T>(string subFolder, string assetName, OrbitParams orbit, string parentFolder, string prefabFolder, string outputFolder, List<ResourceData> planetResources = null, List<ResourceData> moonResources = null, List<ResourceData> asteroidResources = null, CelestialEnvironment? forcedEnvironment = null) where T : ScriptableObject
    {
        var folder = Path.Combine(parentFolder, subFolder);
        EnsureFolder(folder);
        Debug.Log("Creating folder: " + folder);
        var so = ScriptableObject.CreateInstance<T>();
        so.name = assetName;
        (so as IOrbitable)?.InitializeOrbit(orbit);
        AssetDatabase.CreateAsset(so, $"{folder}/{assetName}.asset");

        switch (so)
        {
            case PlanetSO planetSO:
                planetSO.type = forcedEnvironment ?? GetRandomEnvironment();
                break;
            case MoonSO moonSO:
                moonSO.type = forcedEnvironment ?? GetRandomEnvironment();
                break;
            case AsteroidBeltSO asteroidBeltSO:
                asteroidBeltSO.type = forcedEnvironment ?? GetRandomEnvironment();
                break;
            default:
                break;
        }

        // Create and save prefab with full components
        CreatePrefabForSO(so, folder, planetResources, moonResources, asteroidResources, prefabFolder, outputFolder);

        return so;
    }

    static GameObject CreatePrefabForSO<T>(T so, string folder, List<ResourceData> planetResources, List<ResourceData> moonResources, List<ResourceData> asteroidResources, string prefabFolder, string outputFolder) where T : ScriptableObject
    {
        GameObject go = new GameObject(so.name);

        // Add components based on SO type
        switch (so)
        {
            case StarSO starSO:
                CelestialPrefabBuilder.AddStarComponents(go, CelestialEnvironment.GAS, starSO);
                break;
            case PlanetSO planetSO:
                CelestialPrefabBuilder.AddPlanetComponents(go, planetSO.type, planetSO);
                planetSO.resources = GenerateResources(planetResources, folder, "Planet", planetSO.type);
                break;
            case MoonSO moonSO:
                CelestialPrefabBuilder.AddMoonComponents(go, moonSO.type, moonSO);
                moonSO.resources = GenerateResources(moonResources, folder, "Moon", moonSO.type);
                break;
            case SystemSO systemSO:
                CelestialPrefabBuilder.AddSystemComponents(go, systemSO);
                break;
            case StationSO stationSO:
                CelestialPrefabBuilder.AddStationComponents(go, stationSO);
                break;
            case NebulaSO nebulaSO:
                CelestialPrefabBuilder.AddNebulaComponents(go, nebulaSO);
                break;
            case AsteroidSO asteroidSO:
                CelestialPrefabBuilder.AddAsteroidComponents(go, asteroidSO.type, asteroidSO);
                asteroidSO.resources = GenerateResources(asteroidResources, folder, "Asteroid", asteroidSO.type);
                break;
            case AsteroidBeltSO asteroidBeltSO:
                CelestialPrefabBuilder.AddAsteroidBeltComponents(go, asteroidBeltSO.type, asteroidBeltSO);
                break;
            case GateSO gateSO:
                CelestialPrefabBuilder.AddGateComponents(go, gateSO);
                break;
            case GalaxySO galaxySO:
                CelestialPrefabBuilder.AddGalaxyComponents(go, galaxySO);
                break;
        }

        // Add orbit component
        var orbitComp = go.AddComponent<OrbitComponent>();
        var orbit = (so as IOrbitable)?.GetOrbitParams();
        if (orbit.HasValue)
            orbitComp.orbitParams = orbit.Value;
        else
            orbitComp.orbitParams = default;

        // Save prefab
        var relative = folder.StartsWith(outputFolder) ? folder.Substring(outputFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : "";
        var prefabDir = Path.Combine(prefabFolder, relative);
        EnsureFolder(prefabDir);
        Debug.Log("Creating prefab directory: " + prefabDir);

        // Save prefab into the prefabFolder hierarchy
        var prefabPath = Path.Combine(prefabDir, $"{so.name}.prefab");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        UnityEngine.Object.DestroyImmediate(go);

        // Assign prefab reference to SO
        if (so is IHasPrefab hasPrefab)
            hasPrefab.prefabReference = prefab;

        return prefab;
    }

    private static readonly Dictionary<(string category, CelestialEnvironment type), List<ResourceSO>> resourceCache = new Dictionary<(string, CelestialEnvironment), List<ResourceSO>>();
    
    public static List<ResourceSO> GenerateResources(List<ResourceData> templates, string targetFolder, string category, CelestialEnvironment bodyType)
    {
        var key = (category, bodyType);
        if (resourceCache.TryGetValue(key, out var cached))
            return new List<ResourceSO>(cached); // return copy of refs

        // Ensure resources folder exists
        var resourcesFolder = Path.Combine(targetFolder, "Resources");
        EnsureFolder(resourcesFolder);
        Debug.Log("Creating folder: " + resourcesFolder);

        var result = new List<ResourceSO>();
        foreach (var tpl in templates)
        {
            // Skip if category doesn't match and isn't universal
            bool categoryMatches = tpl.category.Any(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase) || string.Equals(c, "Universal", StringComparison.OrdinalIgnoreCase));

            if (!categoryMatches)
                continue;

            // Skip if bodyType not in celestialType
            bool typeMatches = tpl.celestialType.Any(t => string.Equals(t, bodyType.ToString(), StringComparison.OrdinalIgnoreCase) || string.Equals(t, "Universal", StringComparison.OrdinalIgnoreCase));

            if (!typeMatches)
                continue;

            // Instantiate SO
            var so = ScriptableObject.CreateInstance<ResourceSO>();
            so.resourceName = tpl.resourceName;
            so.resourceDescription = tpl.resourceDescription;
            so.minQuantity = tpl.minQuantity;
            so.maxQuantity = tpl.maxQuantity;
            so.resourceWeight = tpl.resourceWeight;
            so.resourceCategory = tpl.category.Select(c => Enum.Parse<ResourceCategory>(c)).ToList();
            so.celestialType = new List<string>(tpl.celestialType);
            so.allowedFactions = new List<string>(tpl.allowedFactions);

            var path = Path.Combine(resourcesFolder, $"{SanitizeFileName(so.resourceName)}.asset");
            AssetDatabase.CreateAsset(so, path);
            EditorUtility.SetDirty(so);
            result.Add(so);
        }

        resourceCache[key] = result;
        return new List<ResourceSO>(result);
    }

    public static string SanitizeFileName(string input, string fallbackName = "UnnamedAsset")
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            string fallback = fallbackName + Guid.NewGuid().ToString("N").Substring(0, 6);
            Debug.LogWarning($"SanitizeFileName: Input is null or whitespace. Using fallback name: {fallback}");
            return fallback;
        }

        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(input.Length);

        foreach (var c in input)
        {
            if (Array.IndexOf(invalidChars, c) < 0)
                sb.Append(c);
        }

        var result = sb.ToString().Trim();

        if (string.IsNullOrEmpty(result))
        {
            string fallback = fallbackName + Guid.NewGuid().ToString("N").Substring(0, 6);
            Debug.LogWarning($"SanitizeFileName: '{input}' produced an empty or invalid filename. Using fallback name: {fallback}");
            return fallback;
        }

        Debug.Log($"Sanitized filename: {result}");
        return result;
    }

    static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
            EnsureFolder(parent);

        var leaf = Path.GetFileName(path);
        if (string.IsNullOrEmpty(leaf) || string.IsNullOrEmpty(parent))
            return;

        AssetDatabase.CreateFolder(parent, leaf);
    }

}
