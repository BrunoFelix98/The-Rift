using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles the procedural generation of the entire universe, including galaxies, star systems, and celestial bodies.
/// This script is intended to be used in the 'GalaxyBuilder' scene to bake the universe into 'UniverseMap.bin'.
/// </summary>
public class UniverseGenerator : MonoBehaviour
{
    [Header("Generator Blueprints (ScriptableObjects)")]
    [Tooltip("Blueprints for factions and corporations, imported from FactionsJSON.json")]
    public List<OrganizationSO> organizationBlueprints = new List<OrganizationSO>();
    
    [Tooltip("Blueprints for resources, imported from ResourcesJSON.json")]
    public List<ResourceSO> resourceBlueprints = new List<ResourceSO>();
    
    [Tooltip("Blueprints for galaxies, imported from GalaxiesJSON.json")]
    public List<GalaxyDataSO> galaxyBlueprints = new List<GalaxyDataSO>();
    
    [Header("Universe Scale Settings")]
    [Tooltip("Radius within which galaxies are placed.")]
    public float universeRadius; 
    
    [Tooltip("Radius within which star systems are placed inside a galaxy.")]
    public float galaxyRadius;
    
    [Tooltip("Minimum distance between star systems to prevent overlaps.")]
    public float minSystemDistance; 

    [Header("Resource & Generation Costs (Dust)")]
    [Tooltip("Each galaxy has a limited amount of 'Dust' to spend on generating objects.")]
    public int costSystem;
    public int costStar;
    public int costPlanet;
    public int costMoon;
    public int costAsteroid;

    [Header("System Tuning")]
    [Tooltip("Minimum radius for a star's visual representation.")]
    public float minStarVisRadius;
    [Tooltip("Maximum radius for a star's visual representation.")]
    public float maxStarVisRadius;
    [Tooltip("Planets will be placed within this multiple of the star's radius.")]
    public float systemScaleFactor = 10.0f;

    // Internal State
    private ulong currentId = 1;
    private CelestialAssetRegistry registry;
    private GameObject universeRoot;
    private Dictionary<string, ulong> starNameToId = new Dictionary<string, ulong>();
    private Dictionary<string, Transform> starNameToTransform = new Dictionary<string, Transform>();
    private Dictionary<int, OrganizationSO> factionMap = new Dictionary<int, OrganizationSO>();

    [Header("JSON Data Sources")]
    public string factionsJSONPath = "Assets/Code/Backend/DataSets/Behavior/JSONData/FactionsJSON.json";
    public string galaxiesJSONPath = "Assets/Code/Backend/DataSets/Behavior/JSONData/GalaxiesJSON.json";
    public string resourcesJSONPath = "Assets/Code/Backend/DataSets/Behavior/JSONData/ResourcesJSON.json";
    public string assetOutputFolder = "Assets/Code/Backend/DataSets/GameAssets";

    [ContextMenu("0. Auto-Load Blueprints")]
    public void LoadBlueprints()
    {
#if UNITY_EDITOR
        // 2. Load the newly created/updated assets into the generator's lists
        organizationBlueprints = FindAssetsByType<OrganizationSO>();
        resourceBlueprints = FindAssetsByType<ResourceSO>();
        galaxyBlueprints = FindAssetsByType<GalaxyDataSO>();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[UniverseGenerator] Loaded {organizationBlueprints.Count} Organizations, {resourceBlueprints.Count} Resources, {galaxyBlueprints.Count} Galaxies.");
#endif
    }

#if UNITY_EDITOR
    private List<T> FindAssetsByType<T>() where T : UnityEngine.Object
    {
        List<T> assets = new List<T>();
        string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        foreach (string guid in guids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null) assets.Add(asset);
        }
        return assets;
    }
#endif

    [ContextMenu("1. Generate Scene Hierarchy")]
    public void GenerateUniverseHierarchy()
    {
        registry = FindObjectOfType<CelestialAssetRegistry>();
        if (registry == null) { Debug.LogError("[UniverseGenerator] CelestialAssetRegistry not found in scene!"); return; }

        // Clean up previous generation
        if (universeRoot != null) DestroyImmediate(universeRoot);
        universeRoot = new GameObject("Universe_Root");
        
        // Reset state
        currentId = 1;
        starNameToId.Clear();
        starNameToTransform.Clear();
        factionMap.Clear();

        // Map factions for easy lookup
        var factions = organizationBlueprints.Where(o => o.type == OrganizationType.Faction).ToList();
        foreach (var faction in factions) factionMap[faction.organizationId] = faction;

        // Step 1: Generate Galaxies
        for (int i = 0; i < galaxyBlueprints.Count; i++)
        {
            var galaxy = galaxyBlueprints[i];
            
            // Random position for galaxy in the universe
            Vector3 centerPos = UnityEngine.Random.insideUnitSphere * universeRadius;
            
            // Create Galaxy Object (Root level in Universe_Root)
            var galaxyObj = CreateCelestialObject(universeRoot.transform, 0, CelestialType.GALAXY, galaxy.galaxyName, centerPos.magnitude, 0.000005f / (i + 1), -1, Vector3.zero, "Default", 0, CelestialEnvironment.GAS, "", "", UnityEngine.Random.Range(-20f, 20f), UnityEngine.Random.onUnitSphere);
            galaxyObj.transform.position = centerPos; // Visual helper in editor

            UnityEngine.Random.InitState(galaxy.seed);
            int remainingDust = galaxy.dustParticles;

            var factionsInGalaxy = factions.Where(f => f.homeGalaxy.Equals(galaxy.galaxyName)).ToList();
            int systemsGenerated = 0;
            List<Vector3> systemPositions = new List<Vector3>();

            // Step 2: Generate Named Systems from Factions (Hand-crafted via JSON)
            foreach (var faction in factionsInGalaxy)
            {
                for (int s = 0; s < faction.starNames.Count; s++)
                {
                    if (systemsGenerated >= galaxy.maxSystems) break;
                    if (remainingDust < costSystem + costStar) break;

                    Vector3 systemLocalPos = GetValidSystemPosition(systemPositions, galaxyRadius);
                    if (systemLocalPos != Vector3.zero)
                    {
                        GenerateStarSystem(galaxyObj.transform, faction.organizationId, faction.starNames[s], faction.requiresNebulas, ref remainingDust, faction.organizationName, "Leader", true, systemLocalPos);
                        systemsGenerated++;
                    }
                }
            }

            // Step 3: Fill remaining space with random systems
            int rndIndex = 0;
            while (remainingDust >= (costSystem + costStar + costPlanet) && systemsGenerated < galaxy.maxSystems && rndIndex < 3000)
            {
                Vector3 systemLocalPos = GetValidSystemPosition(systemPositions, galaxyRadius);
                if (systemLocalPos != Vector3.zero)
                {
                    GenerateStarSystem(galaxyObj.transform, -1, $"{galaxy.galaxyName}_Rnd_{rndIndex}", UnityEngine.Random.value > 0.9f, ref remainingDust, "", "", false, systemLocalPos);
                    systemsGenerated++;
                }
                rndIndex++;
            }
            
            Debug.Log($"[UniverseGenerator] Generated galaxy '{galaxy.galaxyName}' with {systemsGenerated} systems. Remaining dust: {remainingDust}");
        }

        // Step 4: Connect Faction Gates (Inter-system travel)
        foreach (var faction in factions)
        {
            if (faction.gates == null) continue;
            foreach (var gateConn in faction.gates)
            {
                if (starNameToId.ContainsKey(gateConn.fromStar) && starNameToId.ContainsKey(gateConn.toStar))
                {
                    CreateGate(gateConn.fromStar, gateConn.toStar, faction.organizationId);
                }
            }
        }
        
        Debug.Log("[UniverseGenerator] Scene Hierarchy Generation Complete.");
    }

    private void CreateGate(string fromStar, string toStar, int factionId)
    {
        if (!starNameToTransform.TryGetValue(fromStar, out var starFrom) || !starNameToTransform.TryGetValue(toStar, out var starTo)) return;

        ulong fromStarId = starNameToId[fromStar];
        ulong toStarId = starNameToId[toStar];

        // Gate placement: typically outside the main planetary orbits (Majestic range)
        float gateRadius = UnityEngine.Random.Range(20000f, 40000f); 

        // 1. Create Gate A (in From system, pointing to To system)
        CreateGateObject(starFrom, fromStarId, toStarId, $"Gate to {toStar}", gateRadius, factionId);

        // 2. Create Gate B (in To system, pointing to From system)
        CreateGateObject(starTo, toStarId, fromStarId, $"Gate to {fromStar}", gateRadius, factionId);

        Debug.Log($"[UniverseGenerator] Connected {fromStar} <-> {toStar} via bidirectional gates.");
    }

    private void CreateGateObject(Transform parent, ulong parentId, ulong destinationId, string name, float radius, int factionId)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        ulong id = currentId++;

        if (parent != null)
        {
            var parentData = parent.GetComponent<CelestialData>();
            if (parentData != null) parentData.data.childIds.Add(id);
        }

        var op = new OrbitParams(id, parentId, Vector3.zero, radius, 0.005f, UnityEngine.Random.Range(0f, 6.28f), CelestialType.GATE, true, name, factionId, GetTemplateName(registry.gateTemplates), 0, CelestialEnvironment.GAS, UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.onUnitSphere);
        var comp = obj.AddComponent<CelestialData>();

        var data = new CelestialPersistenceData(op) {
            destinationId = destinationId,
            ownership = { 
                factionId = factionId, 
                factionName = factionMap.ContainsKey(factionId) ? factionMap[factionId].organizationName : "Neutral"
            }
        };

        comp.Initialize(data);
    }
    [ContextMenu("2. Bake Scene to Binary")]
    public void BakeToBinary()
    {
        if (universeRoot == null) universeRoot = GameObject.Find("Universe_Root");
        if (universeRoot == null) { Debug.LogError("[UniverseGenerator] No universe hierarchy found to bake!"); return; }

        List<CelestialPersistenceData> allData = new List<CelestialPersistenceData>();
        var components = universeRoot.GetComponentsInChildren<CelestialData>();
        foreach (var comp in components)
        {
            allData.Add(comp.data);
        }
        
        UniversePersistenceManager.Save(allData);
        Debug.Log($"[UniverseGenerator] Successfully baked {allData.Count} objects to UniverseMap.bin.");
    }

    private Vector3 GetValidSystemPosition(List<Vector3> existing, float galaxyRad)
    {
        // Simple rejection sampling for system placement
        for (int i = 0; i < 200; i++)
        {
            Vector3 pos = UnityEngine.Random.insideUnitSphere * galaxyRad;
            bool valid = true;
            foreach (var ex in existing)
            {
                if (Vector3.Distance(pos, ex) < minSystemDistance) { valid = false; break; }
            }
            if (valid) { existing.Add(pos); return pos; }
        }
        return Vector3.zero;
    }

    private GameObject GenerateStarSystem(Transform galaxyTransform, int factionId, string name, bool hasNebula, ref int dust, string corp, string leader, bool isFactionHome, Vector3 localPos)
    {
        dust -= costSystem;
        
        float orbitRadius = localPos.magnitude;
        float orbitSpeed = 0.001f * (galaxyRadius / Mathf.Max(1000f, orbitRadius));
        ulong galaxyId = galaxyTransform.GetComponent<CelestialData>().data.orbit.NetworkId;

        // Create System Container
        var systemObj = CreateCelestialObject(galaxyTransform, galaxyId, CelestialType.SYSTEM, $"{name} System", orbitRadius, orbitSpeed, factionId, null, "Default", 0, CelestialEnvironment.GAS, corp, leader, UnityEngine.Random.Range(-15f, 15f), UnityEngine.Random.onUnitSphere);
        ulong systemId = systemObj.GetComponent<CelestialData>().data.orbit.NetworkId;
        
        float starVisRadius = UnityEngine.Random.Range(minStarVisRadius, maxStarVisRadius);
        float maxSystemRadius = starVisRadius * systemScaleFactor;

        // 1. Star (Center of system)
        dust -= costStar;
        var starObj = CreateCelestialObject(systemObj.transform, systemId, CelestialType.STAR, name, 0, 0, factionId, null, GetTemplateName(registry.starTemplates), UnityEngine.Random.Range(0, 1000000), CelestialEnvironment.GAS, corp, leader);
        ulong starId = starObj.GetComponent<CelestialData>().data.orbit.NetworkId;
        starNameToId[name] = starId;
        starNameToTransform[name] = starObj.transform;

        // 2. Nebula (Optional)
        if (hasNebula) CreateCelestialObject(starObj.transform, starId, CelestialType.NEBULA, $"{name} Nebula", 0, 0, factionId, null, GetTemplateName(registry.nebulaTemplates), 0, CelestialEnvironment.GAS);

        // Track placed objects to avoid collision
        List<(float radius, float visRadius, bool isPlanet)> placedObjects = new List<(float, float, bool)>();

        // 3. Planets
        int planetAttempts = UnityEngine.Random.Range(3, 10);
        int planetsPlaced = 0;
        for (int i = 0; i < planetAttempts; i++)
        {
            if (dust < costPlanet) break;
            
            float pVisRadius = UnityEngine.Random.Range(2000f, 2500f);
            float pRad = UnityEngine.Random.Range(starVisRadius * 1.5f, maxSystemRadius - pVisRadius);

            bool collision = false;
            foreach (var obj in placedObjects)
            {
                float requiredGap = (pVisRadius + obj.visRadius) * 1.5f;
                if (Mathf.Abs(pRad - obj.radius) < requiredGap) { collision = true; break; }
            }
            if (collision) continue;

            dust -= costPlanet;
            planetsPlaced++;
            placedObjects.Add((pRad, pVisRadius, true));

            CelestialEnvironment pEnv = GetRandomEnvironment(factionId, CelestialType.PLANET);

            var pObj = CreateCelestialObject(starObj.transform, starId, CelestialType.PLANET, $"{name} P{planetsPlaced}", pRad, 0.1f/Mathf.Sqrt(pRad), factionId, null, GetTemplateName(registry.planetTemplates), UnityEngine.Random.Range(0, 1000000), pEnv, corp, leader, UnityEngine.Random.Range(-15f, 15f), UnityEngine.Random.onUnitSphere);
            ulong pId = pObj.GetComponent<CelestialData>().data.orbit.NetworkId;

            // 3a. Moons
            int moonCount = UnityEngine.Random.Range(0, 4);
            for (int m = 0; m < moonCount; m++)
            {
                if (dust < costMoon) break;
                float mVisRadius = UnityEngine.Random.Range(1000f, 1500f);
                float mOrbit = UnityEngine.Random.Range(pVisRadius * 2.0f, pVisRadius * 3.0f);
                
                dust -= costMoon;
                CelestialEnvironment mEnv = GetRandomEnvironment(factionId, CelestialType.MOON);

                var moonObj = CreateCelestialObject(pObj.transform, pId, CelestialType.MOON, $"{name} P{planetsPlaced}-M{m+1}", mOrbit, 0.5f/Mathf.Sqrt(mOrbit), factionId, null, GetTemplateName(registry.moonTemplates), 0, mEnv, corp, leader, UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.onUnitSphere);
                ulong mId = moonObj.GetComponent<CelestialData>().data.orbit.NetworkId;

                // 3b. Stations (near moons or planets)
                if (UnityEngine.Random.value > 0.6f)
                {
                    float sOrbit = UnityEngine.Random.Range(mVisRadius * 1.5f, mVisRadius * 1.7f);
                    CreateCelestialObject(moonObj.transform, mId, CelestialType.STATION, $"{name} STN", sOrbit, 1.0f/Mathf.Sqrt(sOrbit), factionId, null, GetTemplateName(registry.stationTemplates), 0, CelestialEnvironment.GAS, corp, leader, UnityEngine.Random.Range(-60f, 60f), UnityEngine.Random.onUnitSphere);
                }
            }
        }

        // 4. Asteroids (Scatter in belts or randomly)
        int asteroidTarget = UnityEngine.Random.Range(50, 120);
        for (int a = 0; a < asteroidTarget; a++)
        {
            if (dust < costAsteroid) break;
            
            float astRad = UnityEngine.Random.Range(starVisRadius * 2.0f, maxSystemRadius);
            
            bool collisionWithPlanet = false;
            foreach (var obj in placedObjects)
            {
                if (obj.isPlanet && Mathf.Abs(astRad - obj.radius) < obj.visRadius * 2.0f) { collisionWithPlanet = true; break; }
            }
            if (collisionWithPlanet) continue;

            dust -= costAsteroid;
            CelestialEnvironment aEnv = GetRandomEnvironment(factionId, CelestialType.ASTEROID);

            CreateCelestialObject(starObj.transform, starId, CelestialType.ASTEROID, $"{name} Ast {a}", astRad, 0.05f/Mathf.Sqrt(astRad), factionId, null, "Default", UnityEngine.Random.Range(0, 1000000), aEnv, corp, leader, UnityEngine.Random.Range(-45f, 45f), UnityEngine.Random.onUnitSphere);
        }

        return systemObj;
    }

    private CelestialEnvironment GetRandomEnvironment(int factionId, CelestialType celestialType)
    {
        OrganizationSO faction = null;
        if (factionId != -1) factionMap.TryGetValue(factionId, out faction);
        
        // Filter resources allowed for this faction (or Universal) AND for this celestial type
        var allowedResources = resourceBlueprints.Where(r => (faction == null || r.allowedFactions.Contains(faction.organizationName) || r.allowedFactions.Contains("Universal")) && r.appliesTo.Contains(celestialType)).ToList();

        if (allowedResources.Count > 0 && UnityEngine.Random.Range(0f, 100f) <= 80f) // 80% chance to follow faction-resource logic
        {
            // Pick a random resource from the filtered list
            var randomRes = allowedResources[UnityEngine.Random.Range(0, allowedResources.Count)];
            
            // Pick a random environment from that resource's allowed list
            if (randomRes.environments.Count > 0)
            {
                return randomRes.environments[UnityEngine.Random.Range(0, randomRes.environments.Count)];
            }
        }

        // Fallback or 20% pure randomness: Pick any environment valid for this celestial type across ALL resources
        var allTypeResources = resourceBlueprints.Where(r => r.appliesTo.Contains(celestialType)).ToList();
        if (allTypeResources.Count > 0)
        {
            var randomRes = allTypeResources[UnityEngine.Random.Range(0, allTypeResources.Count)];
            if (randomRes.environments.Count > 0)
            {
                return randomRes.environments[UnityEngine.Random.Range(0, randomRes.environments.Count)];
            }
        }

        return CelestialEnvironment.ROCKY;
    }

    private GameObject CreateCelestialObject(Transform parent, ulong parentId, CelestialType type, string name, float radius, float speed, int factionId, Vector3? center = null, string template = "Default", int resSeed = 0, CelestialEnvironment envType = CelestialEnvironment.ROCKY, string corp = "", string leader = "", float tilt = 0, Vector3? tiltAxis = null)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        ulong id = currentId++;
        
        if (parent != null)
        {
            var parentData = parent.GetComponent<CelestialData>();
            if (parentData != null) parentData.data.childIds.Add(id);
        }
        
        var op = new OrbitParams(id, parentId, center ?? Vector3.zero, radius, speed, UnityEngine.Random.Range(0f, 6.28f), type, true, name, factionId, template, resSeed, envType, tilt, tiltAxis ?? Vector3.up);
        var comp = obj.AddComponent<CelestialData>();
        
        var data = new CelestialPersistenceData(op) {
            ownership = { 
                factionId = factionId, 
                factionName = factionMap.ContainsKey(factionId) ? factionMap[factionId].organizationName : "Neutral", 
                corporationName = corp, 
                leaderName = leader 
            }
        };

        // Populate resources based on rarity and territory
        PopulateResources(data, type, envType, factionId);

        comp.Initialize(data);
        
        return obj;
    }

    private void PopulateResources(CelestialPersistenceData data, CelestialType type, CelestialEnvironment environment, int factionId)
    {
        // Skip celestial types that shouldn't have mineable resources
        if (type == CelestialType.GALAXY || type == CelestialType.SYSTEM || type == CelestialType.STAR || type == CelestialType.NEBULA || type == CelestialType.GATE) return;

        OrganizationSO faction = null;
        if (factionId != -1) factionMap.TryGetValue(factionId, out faction);
        string factionName = faction != null ? faction.organizationName : "Neutral";

        foreach (var resource in resourceBlueprints)
        {
            // 1. Check if resource applies to this celestial type and environment
            if (!resource.appliesTo.Contains(type) || !resource.environments.Contains(environment)) continue;

            // 2. Faction Filtering and Rarity Adjustment
            ResourceRarity effectiveRarity = resource.rarity;
            bool canSpawn = false;

            // If allowedFactions is empty or contains "Universal", it's a general resource
            bool isUniversal = resource.allowedFactions.Count == 0 || resource.allowedFactions.Contains("Universal");

            if (!isUniversal)
            {
                // Restricted resource: Only spawns in specific faction territory
                if (resource.allowedFactions.Contains(factionName))
                {
                    canSpawn = true;
                    effectiveRarity = ResourceRarity.COMMON; // Unique to this faction territory, make it common
                }
                else
                {
                    canSpawn = false; // Restricted to other factions
                }
            }
            else
            {
                // General resource: Can spawn anywhere with its natural rarity
                canSpawn = true;
                effectiveRarity = resource.rarity;
            }

            if (!canSpawn) continue;

            // 3. Roll for spawn based on rarity
            float spawnProbability = GetSpawnProbability(effectiveRarity);
            if (UnityEngine.Random.Range(0f, 100f) <= spawnProbability)
            {
                data.resources.Add(new ResourcePersistenceData
                {
                    id = resource.resourceId,
                    name = resource.resourceName,
                    quantity = UnityEngine.Random.Range(resource.minQuantity, resource.maxQuantity + 1),
                    minQuantity = resource.minQuantity,
                    maxQuantity = resource.maxQuantity,
                    weight = resource.resourceWeight,
                    rarity = effectiveRarity.ToString()
                });
            }
        }
    }

    private float GetSpawnProbability(ResourceRarity rarity)
    {
        switch (rarity)
        {
            case ResourceRarity.COMMON: return 80f;
            case ResourceRarity.UNCOMMON: return 40f;
            case ResourceRarity.RARE: return 15f;
            case ResourceRarity.VERYRARE: return 5f;
            case ResourceRarity.UNIQUE: return 1f;
            default: return 0f;
        }
    }

    private string GetTemplateName<T>(List<T> list) where T : ScriptableObject => (list == null || list.Count == 0) ? "Default" : list[UnityEngine.Random.Range(0, list.Count)].name;
}
