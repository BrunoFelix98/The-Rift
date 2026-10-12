// Assets/Editor/UniverseBaker/UniverseBakerEditor.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using WebSocketSharp;

// NOTE: Adjust type names if your SO classes have different names/paths.

// ---------- JSON DTOs (simple wrappers for Editor-time JSON parsing) ----------
[Serializable]
public class RevisedGalaxyJsonWrapper { public RevisedGalaxyJson[] galaxies; }
[Serializable]
public class RevisedGalaxyJson
{
    public int id;
    public string name;
    public long dustParticles;
    public uint seed;
    public int maxSystems = 1000;
}

[Serializable]
public class RevisedFactionJsonWrapper { public RevisedFactionJson[] factions; }
[Serializable]
public class RevisedFactionJson
{
    public int factionId;
    public string factionName;
    public string homeGalaxy;
    public string[] corporations;
    public string[] leaderNames;
    public string[] starNames; // these will be created as faction systems (names)
    public bool requiresNebulas;
    public string factionColor; // optional
    public uint seed;
    // gates can be present; we'll handle them after systems created
    public RevisedGateJson[] gates;
}

[Serializable]
public class RevisedGateJson
{
    public string from;
    public string to;
}

// resources - not used in job, but loaded for creation step if needed
[Serializable]
public class RevisedResourcesJsonWrapper { public RevisedResourceJson[] resources; }
[Serializable]
public class RevisedResourceJson
{
    public string resourceName;
    public string resourceDescription;
    public int minQuantity;
    public int maxQuantity;
    public float resourceWeight;
    public string[] appliesTo;
    public string[] environment;
    public string[] allowedFactions;
    public string rarity;
}

// ---------- Native POD result written by jobs ----------
public struct RevisedSystemGenResult
{
    public int systemIndex;      // absolute index in galaxy
    public uint seed;            // per-system deterministic seed
    public byte numStars;
    public byte numPlanets;
    public byte numBelts;
    public byte maxAsteroidsPerBelt; // we produce a single max value for later expansion
    public byte maxMoonsPerPlanet;
    public byte factionId;       // 255 = unowned
    public byte hasNebula;       // 0 or 1
    public byte numStations;     // at least 1 for faction systems
}

// ---------- The parallel job ----------
[BurstCompile]
public struct RevisedGenerateSystemsJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<uint> seedInputs;
    public NativeArray<RevisedSystemGenResult> results;

    public void Execute(int i)
    {
        uint s = seedInputs[i];
        // simple RNG based on Unity.Mathematics.Random
        var rng = new Unity.Mathematics.Random(s);

        RevisedSystemGenResult r = new RevisedSystemGenResult();
        r.systemIndex = i;
        r.seed = s;

        // stars 1-3
        r.numStars = (byte)rng.NextInt(1, 4);

        // planets 1-10
        r.numPlanets = (byte)rng.NextInt(1, 11);

        // belts 1-5
        r.numBelts = (byte)rng.NextInt(1, 6);

        // max asteroids per belt 1-15
        r.maxAsteroidsPerBelt = (byte)rng.NextInt(1, 16);

        // max moons per planet 0-20
        r.maxMoonsPerPlanet = (byte)rng.NextInt(0, 21);

        // factionId default to 255 (unowned) — set by caller if needed
        r.factionId = 255;

        // hasNebula default 0 — main thread may flip it on if faction requires
        r.hasNebula = 0;

        // stations: default 0; factions will set >=1
        r.numStations = 0;

        results[i] = r;
    }
}

// ---------- Editor baker ----------
public static class RevisedUniverseBakerEditor
{
    private const string JSON_DIR = "Assets/Code/Backend/DataSets/Behavior/JSONData";
    private const string OUTPUT_ROOT = "Assets/RuleBasedGeneratedGalaxy";

    [MenuItem("Tools/Universe/Bake Universe (Jobs)")]
    public static void BakeUniverseMenu()
    {
        if (!EditorUtility.DisplayDialog("Bake Universe",
            "This will generate SystemData, StationData and GateData assets under Assets/RuleBasedGeneratedGalaxy. Continue?",
            "Yes", "Cancel")) return;

        try
        {
            BakeUniverse();
            EditorUtility.DisplayDialog("Bake Universe", "Universe baking complete!", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Bake Universe", "Failed: " + ex.Message, "OK");
        }
    }

    public static void BakeUniverse()
    {
        // 1) Load JSONs
        var galaxies = LoadGalaxies();
        var factions = LoadFactions();
        var resources = LoadResources();

        // Create factions and resources
        CreateFactionSOAssets(factions);
        CreateResourceSOAssets(resources);

        // Ensure output directories
        Directory.CreateDirectory(OUTPUT_ROOT);
        Directory.CreateDirectory($"{OUTPUT_ROOT}/Systems");
        Directory.CreateDirectory($"{OUTPUT_ROOT}/Stations");
        Directory.CreateDirectory($"{OUTPUT_ROOT}/Gates");
        Directory.CreateDirectory($"{OUTPUT_ROOT}/Asteroids");

        // For each galaxy: build generation request list (faction systems first), respecting dust
        foreach (var g in galaxies)
        {
            Debug.Log($"Generating galaxy: {g.name} (seed={g.seed})");
            List<GenerationRequest> requests = BuildGenerationRequestsForGalaxy(g, factions);

            // Build seed array for jobs
            int n = requests.Count;
            NativeArray<uint> seeds = new NativeArray<uint>(n, Allocator.TempJob);
            for (int i = 0; i < n; ++i) seeds[i] = requests[i].seed;

            // allocate results
            NativeArray<RevisedSystemGenResult> results = new NativeArray<RevisedSystemGenResult>(n, Allocator.TempJob);

            // schedule job
            var job = new RevisedGenerateSystemsJob { seedInputs = seeds, results = results };
            JobHandle handle = job.Schedule(n, 64);
            handle.Complete();

            // marshal results and create assets on main thread
            for (int i = 0; i < n; ++i)
            {
                var r = results[i];
                var req = requests[i];

                // finalize faction data
                if (req.isFactionGuaranteed)
                {
                    r.factionId = (byte)req.factionId;
                    r.numStations = (byte)Math.Max(1, req.defaultStations); // guarantee at least 1 station
                    r.hasNebula = (byte)(req.requiresNebula ? 1 : 0);
                }

                //Create system data
                var systemSO = CreateSystemAsset(g, req, r);
                PopulateAsteroidBelts(systemSO);
                PopulatePlanetAndMoonResources(systemSO);
                AssignSystemOwnership(systemSO, FindFactionByStarName(systemSO.systemName));
                AssignStationOwnership(systemSO);
            }

            seeds.Dispose();
            results.Dispose();

            // After systems created, process gates (explicit gates from faction JSON)
            foreach (var faction in factions)
                if (string.Equals(faction.homeGalaxy, g.name, StringComparison.OrdinalIgnoreCase))
                    CreateAndLinkGates(faction);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Universe bake finished.");
    }

    // ---------- Helpers & small types ----------
    private class GenerationRequest
    {
        public string requestedName; // can be null, then name by index
        public uint seed;
        public bool isFactionGuaranteed;
        public int factionId;
        public bool requiresNebula;
        public int defaultStations; // how many to create by default for faction systems
    }

    private static List<GenerationRequest> BuildGenerationRequestsForGalaxy(RevisedGalaxyJson galaxy, RevisedFactionJson[] factions)
    {
        var list = new List<GenerationRequest>(galaxy.maxSystems);

        // 1) add faction systems first (guaranteed)
        foreach (var f in factions)
        {
            if (!string.Equals(f.homeGalaxy, galaxy.name, StringComparison.OrdinalIgnoreCase)) continue;

            // each f.starNames is a guaranteed system (faction-controlled)
            if (f.starNames != null)
            {
                foreach (var starName in f.starNames)
                {
                    uint seed = HashToSeed(galaxy.seed, f.seed, starName);
                    list.Add(new GenerationRequest
                    {
                        requestedName = starName,
                        seed = seed,
                        isFactionGuaranteed = true,
                        factionId = f.factionId,
                        requiresNebula = f.requiresNebulas,
                        defaultStations = 1
                    });
                }
            }
        }

        // 2) add non-faction placeholders until dust is exhausted or maxSystems is reached
        // We model each placeholder as costing an estimated dust cost; for simplicity use 1 per system
        long remainingDust = galaxy.dustParticles;
        int nextIndex = 0;
        while (list.Count < galaxy.maxSystems)
        {
            if (remainingDust <= 0) break; // out of dust

            string name = $"SYS-{galaxy.name}-{nextIndex}";
            uint seed = HashToSeed(galaxy.seed, (uint)nextIndex, galaxy.name);
            list.Add(new GenerationRequest
            {
                requestedName = name,
                seed = seed,
                isFactionGuaranteed = false,
                factionId = -1,
                requiresNebula = false,
                defaultStations = 0
            });

            remainingDust -= 1; // crude cost model; adjust to your per-object cost later
            nextIndex++;
        }

        return list;
    }

    private static uint HashToSeed(uint a, uint b, string s)
    {
        // simple deterministic mixing
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ a) * 16777619u;
            hash = (hash ^ b) * 16777619u;
            foreach (var ch in s) hash = (hash ^ ch) * 16777619u;
            return hash;
        }
    }

    private static uint HashToSeed(uint a, string s, string t)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ a) * 16777619u;
            foreach (var ch in s) hash = (hash ^ ch) * 16777619u;
            foreach (var ch in t) hash = (hash ^ ch) * 16777619u;
            return hash;
        }
    }

    // ---------- Asset creation ----------

    private static void PopulateAsteroidBelts(RevisedSystemData system)
    {
        if (system == null || system.asteroidBelts == null) return;

        string asteroidDir = $"{OUTPUT_ROOT}/Asteroids";
        if (!Directory.Exists(asteroidDir)) Directory.CreateDirectory(asteroidDir);

        // Preload resource SOs for assignment (best-effort)
        var resourceGuids = AssetDatabase.FindAssets("t:RevisedResourceData", new[] { $"{OUTPUT_ROOT}/Resources" });
        var resourceAssets = new List<RevisedResourceData>();
        foreach (var g in resourceGuids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var rs = AssetDatabase.LoadAssetAtPath<RevisedResourceData>(p);
            if (rs != null) resourceAssets.Add(rs);
        }

        foreach (var belt in system.asteroidBelts)
        {
            if (belt == null) continue;
            if (belt.asteroids == null) belt.asteroids = new List<RevisedAsteroidData>();

            // determine asteroid count deterministically (or use Random)
            int count = UnityEngine.Random.Range(Mathf.Max(1, belt.asteroids.Count), Mathf.Max(3, 8));
            for (int i = 0; i < count; ++i)
            {
                var asteroid = ScriptableObject.CreateInstance<RevisedAsteroidData>();
                asteroid.asteroidName = $"{system.systemName}_Ast_{belt.beltName}_{i}";
                // optional properties
                if (HasField(asteroid, "size")) SetFloatField(asteroid, "size", UnityEngine.Random.Range(1f, 12f));
                if (HasField(asteroid, "composition")) SetStringField(asteroid, "composition", "Rocky");

                // Save asteroid as its own asset
                string astPath = $"{asteroidDir}/{MakeSafeFileName(asteroid.asteroidName)}.asset";
                AssetDatabase.CreateAsset(asteroid, astPath);

                // Best-effort: assign a resource to the asteroid from available resource SOs
                TryAssignResourceToAsteroid(asteroid, resourceAssets, system);

                // add to belt list (they remain references to separate assets)
                belt.asteroids.Add(asteroid);
                EditorUtility.SetDirty(belt);
            }
        }
        AssetDatabase.SaveAssets();
    }

    // reflection helpers used above
    private static bool HasField(UnityEngine.Object obj, string fieldName)
    {
        return obj.GetType().GetField(fieldName) != null || obj.GetType().GetProperty(fieldName) != null;
    }

    private static void SetStringField(UnityEngine.Object obj, string fieldName, string value)
    {
        var fld = obj.GetType().GetField(fieldName);
        if (fld != null && fld.FieldType == typeof(string)) { fld.SetValue(obj, value); return; }
        var prop = obj.GetType().GetProperty(fieldName);
        if (prop != null && prop.PropertyType == typeof(string)) { prop.SetValue(obj, value); return; }
    }

    private static void SetFloatField(UnityEngine.Object obj, string fieldName, float value)
    {
        var fld = obj.GetType().GetField(fieldName);
        if (fld != null && fld.FieldType == typeof(float)) { fld.SetValue(obj, value); return; }
        var prop = obj.GetType().GetProperty(fieldName);
        if (prop != null && prop.PropertyType == typeof(float)) { prop.SetValue(obj, value); return; }
    }

    // Best-effort resource assignment to asteroid. It looks for common field names:
    // - `primaryResource` (string or RevisedResourceData reference) or
    // - `primaryResourceName` (string)
    // If none present, it logs a warning (so you can tell me the exact asteroid schema to make it perfect).
    private static void TryAssignResourceToAsteroid(RevisedAsteroidData asteroid, List<RevisedResourceData> resourcePool, RevisedSystemData system)
    {
        if (resourcePool == null || resourcePool.Count == 0) return;

        // pick candidate resources that allow asteroids & match faction or universal
        var candidates = new List<RevisedResourceData>();
        foreach (var r in resourcePool)
        {
            bool applies = r.appliesTo != null && r.appliesTo.Contains(CelestialType.ASTEROID);
            bool factionAllowed = r.allowedFactions == null || r.allowedFactions.Count == 0 || r.allowedFactions.Contains("Universal") ||
                                  (system.owningFaction != null && r.allowedFactions.Contains(system.owningFaction.factionName));
            if (applies && factionAllowed) candidates.Add(r);
        }

        if (candidates.Count == 0) return;

        var choice = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        // try to find a field/property for assignment
        var fldRef = asteroid.GetType().GetField("primaryResource");
        if (fldRef != null)
        {
            // if it's a string field, write name; if it's a resource SO reference, try setting it
            if (fldRef.FieldType == typeof(string))
                fldRef.SetValue(asteroid, choice.resourceName);
            else if (fldRef.FieldType.IsAssignableFrom(choice.GetType()))
                fldRef.SetValue(asteroid, choice);
            return;
        }

        var fldName = asteroid.GetType().GetField("primaryResourceName");
        if (fldName != null && fldName.FieldType == typeof(string))
        {
            fldName.SetValue(asteroid, choice.resourceName);
            return;
        }

        // If asteroid has a list of resource entries, we can't reliably populate it without knowing its type.
        Debug.Log($"Note: asteroid '{asteroid.asteroidName}' could not assign resource automatically. If you want resources added, tell me the asteroid resource field shape (field name and type).");
    }

    // Attempts to assign resources to a planet or moon object (best-effort).
    private static void TryAssignResourcesToCelestial(UnityEngine.Object body, List<RevisedResourceData> resourcePool, RevisedFactionData owningFaction, CelestialEnvironment? env = null)
    {
        if (body == null || resourcePool == null || resourcePool.Count == 0) return;

        // filter by appliesTo and environment and faction
        var candidates = new List<RevisedResourceData>();
        foreach (var res in resourcePool)
        {
            if (res == null) continue;
            // check appliesTo: Planet/Moon/Asteroid
            bool applies = false;
            if (res.appliesTo != null)
            {
                foreach (var at in res.appliesTo)
                {
                    try
                    {
                        if (at.ToString().Equals("Planet", StringComparison.OrdinalIgnoreCase) ||
                            at.ToString().Equals("Moon", StringComparison.OrdinalIgnoreCase) ||
                            at.ToString().Equals("Asteroid", StringComparison.OrdinalIgnoreCase))
                        {
                            // float acceptance - we simply prefer planet/moon if asked by caller
                            applies = true;
                            break;
                        }
                    }
                    catch { applies = true; break; }
                }
            }
            else applies = true;

            // environment match if provided
            if (env.HasValue && res.environment != null && res.environment.Count > 0)
            {
                bool envMatch = false;
                foreach (var e in res.environment)
                {
                    if (e.ToString().Equals(env.Value.ToString(), StringComparison.OrdinalIgnoreCase)) { envMatch = true; break; }
                }
                if (!envMatch) applies = false;
            }

            // faction allowed?
            bool factionAllowed = res.allowedFactions == null || res.allowedFactions.Count == 0 ||
                                  res.allowedFactions.Contains("Universal") ||
                                  (owningFaction != null && res.allowedFactions.Contains(owningFaction.factionName));

            if (applies && factionAllowed) candidates.Add(res);
        }

        if (candidates.Count == 0) return;

        int toAssign = UnityEngine.Random.Range(1, Math.Min(3, candidates.Count) + 1);
        var assigned = new List<RevisedResourceData>();
        for (int i = 0; i < toAssign; ++i)
        {
            var choice = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            if (!assigned.Contains(choice)) assigned.Add(choice);
        }

        // Now attempt to set fields on the body
        var t = body.GetType();
        var fld = t.GetField("resources");
        if (fld != null)
        {
            var listObj = fld.GetValue(body);
            if (listObj == null)
            {
                // create appropriate list instance
                listObj = Activator.CreateInstance(fld.FieldType);
                fld.SetValue(body, listObj);
            }
            var ilist = listObj as System.Collections.IList;
            if (ilist != null)
            {
                var elementType = fld.FieldType.IsGenericType ? fld.FieldType.GetGenericArguments()[0] : null;
                foreach (var res in assigned)
                {
                    if (elementType == null)
                    {
                        // try to add resource name as string fallback
                        if (ilist.GetType().IsAssignableFrom(typeof(System.Collections.IList)))
                        {
                            ilist.Add(res.resourceName);
                        }
                    }
                    else
                    {
                        if (elementType == typeof(RevisedResourceData))
                            ilist.Add(res);
                        else if (elementType == typeof(string))
                            ilist.Add(res.resourceName);
                        else
                        {
                            // attempt to create an element and fill common fields: resource/resourceName/quantity
                            var element = Activator.CreateInstance(elementType);
                            var eType = element.GetType();
                            var fRes = eType.GetField("resource");
                            var fResName = eType.GetField("resourceName");
                            var fQty = eType.GetField("quantity");
                            if (fRes != null && fRes.FieldType.IsAssignableFrom(res.GetType()))
                            {
                                fRes.SetValue(element, res);
                                if (fQty != null) fQty.SetValue(element, UnityEngine.Random.Range(res.minQuantity, res.maxQuantity + 1));
                                ilist.Add(element);
                            }
                            else if (fResName != null && fResName.FieldType == typeof(string))
                            {
                                fResName.SetValue(element, res.resourceName);
                                if (fQty != null) fQty.SetValue(element, UnityEngine.Random.Range(res.minQuantity, res.maxQuantity + 1));
                                ilist.Add(element);
                            }
                            else
                            {
                                // unsupported element type - skip
                            }
                        }
                    }
                }
                fld.SetValue(body, listObj);
                EditorUtility.SetDirty(body);
                return;
            }
        }

        // if no 'resources' field, try 'primaryResource' or 'primaryResourceName'
        FieldInfo fldPrimary = t.GetField("primaryResource", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        PropertyInfo propPrimary = fldPrimary == null ? t.GetProperty("primaryResource", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static) : null;
        
        if (fldPrimary != null)
        {
            // we only set the first chosen resource
            try
            {
                var first = assigned[0];
                var f = t.GetField("primaryResource");
                if (f != null)
                {
                    if (f.FieldType == typeof(string)) f.SetValue(body, first.resourceName);
                    else if (f.FieldType.IsAssignableFrom(first.GetType())) f.SetValue(body, first);
                    EditorUtility.SetDirty(body);
                    return;
                }
                var p = t.GetProperty("primaryResource");
                if (p != null && p.CanWrite)
                {
                    if (p.PropertyType == typeof(string)) p.SetValue(body, first.resourceName, null);
                    else if (p.PropertyType.IsAssignableFrom(first.GetType())) p.SetValue(body, first, null);
                    EditorUtility.SetDirty(body);
                    return;
                }
            }
            catch { /* ignore */ }
        }

        // fallback: if object has any field named resourceName or primaryResourceName
        var fName = t.GetField("primaryResourceName") ?? t.GetField("resourceName");
        if (fName != null && fName.FieldType == typeof(string))
        {
            fName.SetValue(body, assigned[0].resourceName);
            EditorUtility.SetDirty(body);
            return;
        }

        // If we get here, we couldn't place resources automatically in a known field shape.
        Debug.Log($"Could not automatically assign resource(s) to {body.name}. If you want automatic population, tell me the concrete field names/types of the 'resources' field.");
    }

    // Populates planet/moon resources for a system (calls TryAssignResourcesToCelestial).
    private static void PopulatePlanetAndMoonResources(RevisedSystemData system)
    {
        if (system == null) return;

        // preload resource SOs
        var resourceGuids = AssetDatabase.FindAssets("t:RevisedResourceData", new[] { $"{OUTPUT_ROOT}/Resources" });
        var resourceAssets = new List<RevisedResourceData>();
        foreach (var g in resourceGuids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var rs = AssetDatabase.LoadAssetAtPath<RevisedResourceData>(p);
            if (rs != null) resourceAssets.Add(rs);
        }

        foreach (var planet in system.planets)
        {
            // assign resources to planet based on environment and faction
            TryAssignResourcesToCelestial(planet, resourceAssets, system.owningFaction, planet.environmentType);

            // assign to moons
            if (planet.moons != null)
            {
                foreach (var moon in planet.moons)
                {
                    TryAssignResourcesToCelestial(moon, resourceAssets, system.owningFaction, moon.environmentType);
                }
            }
        }

        AssetDatabase.SaveAssets();
    }

    private static void AssignSystemOwnership(RevisedSystemData system, RevisedFactionData faction)
    {
        if (system == null || faction == null) return;
        system.owningFaction = faction;
        EditorUtility.SetDirty(system);
    }

    private static void AssignStationOwnership(RevisedSystemData system)
    {
        if (system == null || system.owningFaction == null || system.stations == null) return;
        foreach (var station in system.stations)
        {
            if (station == null) continue;
            station.owningFaction = system.owningFaction;
            EditorUtility.SetDirty(station);
        }
    }

    private static void CreateAndLinkGates(RevisedFactionJson faction)
    {
        if (faction == null || faction.gates == null || faction.gates.Length == 0) return;

        string gateDir = $"{OUTPUT_ROOT}/Gates";
        if (!Directory.Exists(gateDir)) Directory.CreateDirectory(gateDir);

        foreach (var g in faction.gates)
        {
            var fromSO = FindSystemSOByName(g.from);
            var toSO = FindSystemSOByName(g.to);
            if (fromSO == null || toSO == null)
            {
                Debug.LogWarning($"Gate creation skipped: missing system '{g.from}' or '{g.to}' for faction {faction.factionName}");
                continue;
            }

            var gate = ScriptableObject.CreateInstance<RevisedGateData>();
            gate.gateName = $"{fromSO.systemName} <-> {toSO.systemName} Gate";
            gate.originSystem = fromSO;
            gate.targetSystem = toSO;
            gate.isBidirectional = true;

            // attach controlling faction SO (search in the same folder we created them in)
            var ctrl = FindFactionSOById(faction.factionId);
            if (ctrl != null) gate.controllingFaction = ctrl;

            string gatePath = $"{gateDir}/{MakeSafeFileName(gate.gateName)}.asset";
            AssetDatabase.CreateAsset(gate, gatePath);

            if (fromSO.gates == null) fromSO.gates = new List<RevisedGateData>();
            if (toSO.gates == null) toSO.gates = new List<RevisedGateData>();
            fromSO.gates.Add(gate);
            toSO.gates.Add(gate);

            EditorUtility.SetDirty(fromSO);
            EditorUtility.SetDirty(toSO);
            Debug.Log($"Created gate: {gatePath}");
        }

        AssetDatabase.SaveAssets();
    }

    private static RevisedFactionData FindFactionByStarName(string starName)
    {
        var guids = AssetDatabase.FindAssets("t:RevisedFactionData", new[] { OUTPUT_ROOT + "/Factions" });
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var faction = AssetDatabase.LoadAssetAtPath<RevisedFactionData>(path);
            if (faction != null && faction.starNames != null && faction.starNames.Contains(starName))
                return faction;
        }
        return null;
    }

    private static RevisedSystemData FindSystemSOByName(string name)
    {
        var guids = AssetDatabase.FindAssets("t:RevisedSystemData", new[] { OUTPUT_ROOT + "/Systems" });
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var so = AssetDatabase.LoadAssetAtPath<RevisedSystemData>(path);
            if (so != null && so.systemName.Equals(name, StringComparison.OrdinalIgnoreCase))
                return so;
        }
        return null;
    }

    private static void CreateFactionSOAssets(RevisedFactionJson[] factions)
    {
        string outDir = "Assets/RuleBasedGeneratedGalaxy/Factions";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        foreach (var f in factions)
        {
            // safe file name
            string safe = MakeSafeFileName($"Faction_{f.factionId}_{f.factionName}");
            string path = $"{outDir}/{safe}.asset";

            // If an asset already exists with the same factionId, skip creation
            var existing = FindFactionSOById(f.factionId);
            if (existing != null)
            {
                // update fields on existing if you want (optional)
                existing.factionName = f.factionName;
                existing.homeGalaxy = f.homeGalaxy;
                existing.requiresNebulas = f.requiresNebulas;
                existing.seed = f.seed;
                // copy lists - ensure RevisedFactionData exposes these fields
                existing.corporations = new List<string>(f.corporations ?? new string[0]);
                existing.leaderNames = new List<string>(f.leaderNames ?? new string[0]);
                existing.starNames = new List<string>(f.starNames ?? new string[0]);
                EditorUtility.SetDirty(existing);
                continue;
            }

            // Create new SO
            var so = ScriptableObject.CreateInstance<RevisedFactionData>();
            so.factionId = f.factionId;
            so.factionName = f.factionName;
            so.homeGalaxy = f.homeGalaxy;
            so.corporations = new List<string>(f.corporations ?? new string[0]);
            so.leaderNames = new List<string>(f.leaderNames ?? new string[0]);
            so.starNames = new List<string>(f.starNames ?? new string[0]);
            so.requiresNebulas = f.requiresNebulas;
            so.factionColor = f.factionColor;
            so.seed = f.seed;

            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"Created Faction SO: {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreateResourceSOAssets(RevisedResourceJson[] resources)
    {
        if (resources == null || resources.Length == 0) return;

        string outDir = $"{OUTPUT_ROOT}/Resources";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        foreach (var r in resources)
        {
            string safe = MakeSafeFileName(r.resourceName);
            string path = $"{outDir}/{safe}.asset";

            // skip if already exists
            var guids = AssetDatabase.FindAssets($"t:RevisedResourceData \"{r.resourceName}\"", new[] { outDir });
            bool exists = false;
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var so = AssetDatabase.LoadAssetAtPath<RevisedResourceData>(p);
                if (so != null && so.resourceName == r.resourceName) { exists = true; break; }
            }
            if (exists) continue;

            var soNew = ScriptableObject.CreateInstance<RevisedResourceData>();
            soNew.resourceName = r.resourceName;
            soNew.resourceDescription = r.resourceDescription;
            soNew.minQuantity = r.minQuantity;
            soNew.maxQuantity = r.maxQuantity;
            soNew.resourceWeight = r.resourceWeight;

            soNew.appliesTo = new List<CelestialType>();
            if (r.appliesTo != null)
                foreach (var s in r.appliesTo)
                    if (System.Enum.TryParse<CelestialType>(s, true, out var ct)) soNew.appliesTo.Add(ct);

            soNew.environment = new List<CelestialEnvironment>();
            if (r.environment != null)
                foreach (var s in r.environment)
                    if (System.Enum.TryParse<CelestialEnvironment>(s, true, out var ev)) soNew.environment.Add(ev);

            soNew.allowedFactions = new List<string>(r.allowedFactions ?? new string[0]);

            // Best-effort: set a 'rarity' field/property if any member contains "rarit" in its name
            var soType = soNew.GetType();
            var fields = soType.GetFields();
            bool setRarity = false;
            foreach (var fld in fields)
            {
                if (fld.Name.IndexOf("rarit", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (fld.FieldType.IsEnum)
                    {
                        try { var val = Enum.Parse(fld.FieldType, r.rarity, true); fld.SetValue(soNew, val); setRarity = true; break; }
                        catch { /* ignore parse error */ }
                    }
                    else if (fld.FieldType == typeof(string))
                    {
                        fld.SetValue(soNew, r.rarity);
                        setRarity = true;
                        break;
                    }
                }
            }
            if (!setRarity)
            {
                // try properties
                var props = soType.GetProperties();
                foreach (var p in props)
                {
                    if (p.Name.IndexOf("rarit", StringComparison.OrdinalIgnoreCase) >= 0 && p.CanWrite)
                    {
                        if (p.PropertyType.IsEnum)
                        {
                            try { var val = Enum.Parse(p.PropertyType, r.rarity, true); p.SetValue(soNew, val, null); setRarity = true; break; }
                            catch { }
                        }
                        else if (p.PropertyType == typeof(string))
                        {
                            p.SetValue(soNew, r.rarity, null);
                            setRarity = true;
                            break;
                        }
                    }
                }
            }

            AssetDatabase.CreateAsset(soNew, path);
            Debug.Log($"Created Resource SO: {path} (rarity set: {setRarity})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static RevisedSystemData CreateSystemAsset(RevisedGalaxyJson galaxy, GenerationRequest req, RevisedSystemGenResult r)
    {
        string safeName = MakeSafeFileName(req.requestedName ?? $"system_{r.systemIndex}");
        string path = $"{OUTPUT_ROOT}/Systems/{galaxy.name}_{safeName}.asset";

        // create a SystemData SO and populate only editor-level fields
        var sys = ScriptableObject.CreateInstance<RevisedSystemData>();
        sys.systemName = req.requestedName ?? $"System_{r.systemIndex}";
        sys.owningFaction = null;
        sys.hasNebula = r.hasNebula == 1;

        // if faction-owned, try to assign owningFaction by id
        if (r.factionId != 255)
        {
            var factionSO = FindFactionSOById(r.factionId);
            if (factionSO != null) sys.owningFaction = factionSO;
        }

        // create minimal star entries (inline)
        sys.stars = new List<RevisedStarData>();
        for (int i = 0; i < r.numStars; ++i)
        {
            var star = ScriptableObject.CreateInstance<RevisedStarData>();
            star.starName = $"{sys.systemName}_Star_{i}";
            star.mass = UnityEngine.Random.Range(0.5f, 3.0f);
            sys.stars.Add(star);
        }

        // planets + moons
        sys.planets = new List<RevisedPlanetData>();
        for (int i = 0; i < r.numPlanets; ++i)
        {
            var p = ScriptableObject.CreateInstance<RevisedPlanetData>();
            p.planetName = $"{sys.systemName}_P{i}";
            p.environmentType = (CelestialEnvironment)UnityEngine.Random.Range(0, Enum.GetNames(typeof(CelestialEnvironment)).Length);
            p.orbitRadius = UnityEngine.Random.Range(1000f, 10000f);

            // create moons for this planet (0..maxMoonsPerPlanet)
            p.moons = new List<RevisedMoonData>();
            int moonCount = UnityEngine.Random.Range(0, r.maxMoonsPerPlanet + 1);
            for (int m = 0; m < moonCount; ++m)
            {
                var moon = ScriptableObject.CreateInstance<RevisedMoonData>();
                moon.moonName = $"{p.planetName}_M{m}";
                moon.environmentType = p.environmentType; // default to planet environment
                moon.orbitRadius = p.orbitRadius + UnityEngine.Random.Range(50f, 500f);
                moon.parentPlanet = p;
                // ensure resource list exists if your SO serialized it
                // moon.resources will be populated later by PopulatePlanetAndMoonResources
                p.moons.Add(moon);
            }

            sys.planets.Add(p);
        }

        // asteroid belts: create the belts with empty asteroid lists (we populate later as separate assets)
        sys.asteroidBelts = new List<RevisedAsteroidBeltData>();
        for (int b = 0; b < r.numBelts; ++b)
        {
            var belt = ScriptableObject.CreateInstance<RevisedAsteroidBeltData>();
            belt.beltName = $"{sys.systemName}_Belt_{b}";
            belt.asteroids = new List<RevisedAsteroidData>(); // will be filled by PopulateAsteroidBelts
            sys.asteroidBelts.Add(belt);
        }

        // Stations - create for faction systems, at least 1
        sys.stations = new List<RevisedStationData>();
        if (r.numStations > 0)
        {
            for (int sidx = 0; sidx < r.numStations; ++sidx)
            {
                var st = ScriptableObject.CreateInstance<RevisedStationData>();
                st.stationName = $"{sys.systemName}_Station_{sidx}";
                st.stationDescription = $"Auto-generated station in {sys.systemName}";
                st.owningFaction = sys.owningFaction; // may be null for neutral
                st.playerOwned = false;
                st.parentSystem = sys;
                st.parentMoon = null; // assign below if there's a moon available

                // default resistances
                st.resistances = new RevisedResistanceProfile
                {
                    EM = 10f,
                    Thermal = 10f,
                    Kinetic = 10f,
                    Explosive = 10f
                };

                // default slots
                st.availableSlots = new List<RevisedSlotData>();
                var stationSlotSo = FindOrCreateDefaultStationSlot();
                st.availableSlots.Add(stationSlotSo);
                st.availableSlots.Add(stationSlotSo);

                // If system has planets with moons, randomly place station on a moon (for example)
                var planetsWithMoons = sys.planets.FindAll(p2 => p2.moons != null && p2.moons.Count > 0);
                if (planetsWithMoons.Count > 0)
                {
                    var chosenPlanet = planetsWithMoons[UnityEngine.Random.Range(0, planetsWithMoons.Count)];
                    var chosenMoon = chosenPlanet.moons[UnityEngine.Random.Range(0, chosenPlanet.moons.Count)];
                    st.parentMoon = chosenMoon;

                    // mark moon as having a station (try common field/property names)
                    TrySetBoolFieldIfExists(chosenMoon, new[] { "hasStation", "HasStation", "isStation", "Has_Station" }, true);
                    EditorUtility.SetDirty(chosenMoon);
                }

                // Save station as its own asset under OUTPUT_ROOT/Stations
                string stationDir = $"{OUTPUT_ROOT}/Stations";
                Directory.CreateDirectory(stationDir);
                string stationPath = $"{stationDir}/{MakeSafeFileName(st.stationName)}.asset";
                AssetDatabase.CreateAsset(st, stationPath);

                // install a market module if available and slot exists
                var marketModule = FindModuleByName("Market Module");
                if (marketModule != null && st.HasAvailableSlotFor(marketModule))
                    st.installedModules.Add(marketModule);

                sys.stations.Add(st);
            }
        }

        sys.gates = new List<RevisedGateData>();

        // create the system asset itself (after child subassets like moons so they can be added)
        AssetDatabase.CreateAsset(sys, path);

        // add star/planet/moon/belt objects as subassets of the system asset
        foreach (var star in sys.stars) AssetDatabase.AddObjectToAsset(star, sys);
        foreach (var planet in sys.planets)
        {
            AssetDatabase.AddObjectToAsset(planet, sys);
            if (planet.moons != null)
                foreach (var moon in planet.moons) AssetDatabase.AddObjectToAsset(moon, sys);
        }
        foreach (var belt in sys.asteroidBelts) AssetDatabase.AddObjectToAsset(belt, sys);

        // If the system needs a nebula, create a small Nebula SO and attach as subasset and assign system.nebula if that field exists
        if (sys.hasNebula)
        {
            var neb = ScriptableObject.CreateInstance<RevisedNebulaData>();
            // Try to fill some basic fields if present
            var nf = neb.GetType().GetField("nebulaName");
            if (nf != null) nf.SetValue(neb, $"{sys.systemName}_Nebula");
            AssetDatabase.AddObjectToAsset(neb, sys);

            // try to set sys.nebula field if it exists
            var sysNebField = sys.GetType().GetField("nebulaData");
            if (sysNebField != null && sysNebField.FieldType.IsAssignableFrom(neb.GetType()))
            {
                sysNebField.SetValue(sys, neb);
            }
        }

        EditorUtility.SetDirty(sys);
        Debug.Log($"Created System SO: {path}");
        return sys;
    }

    private static void TrySetBoolFieldIfExists(UnityEngine.Object obj, string[] candidateNames, bool value)
    {
        if (obj == null) return;
        var t = obj.GetType();
        foreach (var name in candidateNames)
        {
            var f = t.GetField(name);
            if (f != null && f.FieldType == typeof(bool)) { f.SetValue(obj, value); return; }
            var p = t.GetProperty(name);
            if (p != null && p.PropertyType == typeof(bool) && p.CanWrite) { p.SetValue(obj, value, null); return; }
        }
    }

    private static RevisedFactionData FindFactionSOById(int id)
    {
        var guids = AssetDatabase.FindAssets("t:RevisedFactionData", new[] { $"{OUTPUT_ROOT}/Factions" });
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var so = AssetDatabase.LoadAssetAtPath<RevisedFactionData>(path);
            if (so == null) continue;
            if (so.factionId == id) return so;
        }
        return null;
    }

    private static RevisedSlotData FindOrCreateDefaultStationSlot()
    {
        // Look for an existing SlotData asset named "Default Station Slot"
        var guids = AssetDatabase.FindAssets("Default Station Slot t:SlotData");
        if (guids != null && guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var so = AssetDatabase.LoadAssetAtPath<RevisedSlotData>(path);
            if (so != null) return so;
        }

        // Create a default station slot asset
        var defaultSlot = ScriptableObject.CreateInstance<RevisedSlotData>();
        defaultSlot.slotName = "Default Station Slot";
        defaultSlot.slotDescription = "Auto-generated default station slot (STATION)";
        defaultSlot.slotCategory = SlotCategory.STATION;
        defaultSlot.allowedModules = new List<ModuleCategory> { ModuleCategory.StationModule };

        string pathOut = $"{OUTPUT_ROOT}/Slots/DefaultStationSlot.asset";
        Directory.CreateDirectory($"{OUTPUT_ROOT}/Slots");
        AssetDatabase.CreateAsset(defaultSlot, pathOut);
        Debug.Log($"Created default slot at {pathOut}");
        return defaultSlot;
    }

    private static RevisedModuleData FindModuleByName(string moduleName)
    {
        var guids = AssetDatabase.FindAssets($"t:ModuleData \"{moduleName}\"");
        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var so = AssetDatabase.LoadAssetAtPath<RevisedModuleData>(p);
            if (so != null && so.moduleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                return so;
        }
        return null;
    }

    // ---------- JSON loaders ----------
    private static RevisedGalaxyJson[] LoadGalaxies()
    {
        string path = Path.Combine(JSON_DIR, "GalaxiesJSON.json");
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        var json = File.ReadAllText(path);
        var wrapper = JsonUtility.FromJson<RevisedGalaxyJsonWrapper>(json);
        return wrapper.galaxies ?? new RevisedGalaxyJson[0];
    }

    private static RevisedFactionJson[] LoadFactions()
    {
        string path = Path.Combine(JSON_DIR, "FactionsJSON.json");
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        var json = File.ReadAllText(path);
        var wrapper = JsonUtility.FromJson<RevisedFactionJsonWrapper>(json);
        return wrapper.factions ?? new RevisedFactionJson[0];
    }

    private static RevisedResourceJson[] LoadResources()
    {
        string path = Path.Combine(JSON_DIR, "ResourcesJSON.json");
        if (!File.Exists(path)) { Debug.LogWarning("No resources json found."); return new RevisedResourceJson[0]; }
        var json = File.ReadAllText(path);
        var wrapper = JsonUtility.FromJson<RevisedResourcesJsonWrapper>(json);
        return wrapper.resources ?? new RevisedResourceJson[0];
    }

    // ---------- Utilities ----------
    private static string MakeSafeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) name = "unnamed";
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Replace(" ", "_");
    }
}
#endif
