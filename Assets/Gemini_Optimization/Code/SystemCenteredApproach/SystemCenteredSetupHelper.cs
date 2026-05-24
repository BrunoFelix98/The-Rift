using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

#if UNITY_EDITOR
public static class SystemCenteredSetupHelper
{
    [MenuItem("The Rift/Setup System-Centered Scene")]
    public static void SetupScene()
    {
        // 1. Create Managers Object
        GameObject mgr = GameObject.Find("SystemCenteredUniverseManager");
        if (mgr == null) mgr = new GameObject("SystemCenteredUniverseManager");

        // 2. Setup Camera & Cinemachine
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            var allCams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var c in allCams)
            {
                if (c.name.Contains("Main")) { mainCam = c; break; }
            }
        }

        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCam = camObj.AddComponent<Camera>();
        }
        else
        {
            mainCam.tag = "MainCamera";
        }
        
        var brain = mainCam.GetComponent<Unity.Cinemachine.CinemachineBrain>() ?? mainCam.gameObject.AddComponent<Unity.Cinemachine.CinemachineBrain>();
        brain.enabled = true;
        mainCam.farClipPlane = 1000000f; // High far clip for the dynamic skybox

        // 3. Add NetworkManager if missing
        var netMgr = GameObject.FindObjectOfType<NetworkManager>();
        if (netMgr == null)
        {
            // Second check by name just in case FindObjectOfType missed it
            GameObject existing = GameObject.Find("NetworkManager");
            if (existing != null) netMgr = existing.GetComponent<NetworkManager>();
            
            if (netMgr == null)
            {
                GameObject nObj = new GameObject("NetworkManager");
                netMgr = nObj.AddComponent<NetworkManager>();
            }
        }
        
        // Ensure persistence and singleton behavior
        if (netMgr.GetComponent<NetworkManagerPersistence>() == null)
        {
            netMgr.gameObject.AddComponent<NetworkManagerPersistence>();
        }

        // Add AutoStart and Debugger scripts
        if (mgr.GetComponent<NetworkAutoStart>() == null) mgr.AddComponent<NetworkAutoStart>();
        if (mgr.GetComponent<NetworkDebuggerUI>() == null) mgr.AddComponent<NetworkDebuggerUI>();

        // 4. Configure NetworkManager Player Prefab
        string playerPath = "Assets/Prefabs/Player Prefabs/PlayerFighterPiloting.prefab";
        string bigShipPath = "Assets/Prefabs/Player Prefabs/PlayerBigShipPiloting.prefab";
        
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
        GameObject bigShipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bigShipPath);

        void EnsureJumpController(GameObject prefab)
        {
            if (prefab != null && prefab.GetComponent<PlayerJumpController>() == null)
            {
                prefab.AddComponent<PlayerJumpController>();
                EditorUtility.SetDirty(prefab);
                Debug.Log($"[Setup] Added PlayerJumpController to {prefab.name}");
            }
        }

        EnsureJumpController(playerPrefab);
        EnsureJumpController(bigShipPrefab);

        if (playerPrefab != null)
        {
            netMgr.NetworkConfig.PlayerPrefab = playerPrefab;
            var networkPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (networkPrefabs != null && !networkPrefabs.Contains(playerPrefab))
            {
                networkPrefabs.Add(new NetworkPrefab { Prefab = playerPrefab });
                EditorUtility.SetDirty(networkPrefabs);
            }
        }

        if (netMgr.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>() == null)
        {
            netMgr.gameObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        }

        // 5. Add System-Centered Components
        var registry = mgr.GetComponent<CelestialAssetRegistry>() ?? mgr.AddComponent<CelestialAssetRegistry>();
        var pool = mgr.GetComponent<CelestialPool>() ?? mgr.AddComponent<CelestialPool>();
        var visualizer = mgr.GetComponent<SystemCenteredVisualizer>() ?? mgr.AddComponent<SystemCenteredVisualizer>();
        
        // CLEANUP: Remove accidental components that shouldn't be on the manager
        System.Type[] accidentalTypes = new System.Type[] {
            System.Type.GetType("NetworkedCelestial"),
            System.Type.GetType("NetworkedPlanetData"),
            System.Type.GetType("NetworkedMoonData"),
            System.Type.GetType("NetworkedAsteroidData"),
            System.Type.GetType("NetworkedStationData")
        };

        foreach (var type in accidentalTypes)
        {
            if (type == null) continue;
            var component = mgr.GetComponent(type);
            if (component != null) 
            {
                Object.DestroyImmediate(component);
                Debug.Log($"[Setup] Removed accidental {type.Name} from UniverseManager");
            }
        }
        
        // New Components for Scene-per-System
        if (mgr.GetComponent<GalacticTimeManager>() == null) mgr.AddComponent<GalacticTimeManager>();
        if (mgr.GetComponent<JumpManager>() == null) mgr.AddComponent<JumpManager>();
        if (mgr.GetComponent<WarpEffectManager>() == null) mgr.AddComponent<WarpEffectManager>();
        
        // Balanced Scaling: Majestically large objects, but small enough to fit in the requested orbits
        if (visualizer.settings == null) visualizer.settings = new UniverseVisualizationSettings();
        visualizer.settings.starScale = 500f;
        visualizer.settings.planetScale = 100f;
        visualizer.settings.moonScale = 30f;
        visualizer.settings.nebulaScale = 5000f;
        visualizer.settings.systemRadius = 2000f; // System radius increased to accommodate the larger star
        visualizer.localViewDistance = 10000f;

        // Environment & Visuals
        if (mgr.GetComponent<SpaceEnvironmentManager>() == null) mgr.AddComponent<SpaceEnvironmentManager>();

        // Setup Dynamic Skybox
        GameObject skyboxObj = GameObject.Find("DynamicSkybox");
        if (skyboxObj == null)
        {
            skyboxObj = new GameObject("DynamicSkybox");
            // DO NOT set parent to mgr, must be root for DontDestroyOnLoad
        }
        var skybox = skyboxObj.GetComponent<DynamicSkyboxManager>() ?? skyboxObj.AddComponent<DynamicSkyboxManager>();
        skybox.skyboxRadius = 50000f;
        skybox.starSize = 200f;

        // Auto-populate Registry Templates
        registry.starTemplates = FindAssets<StarSO>("t:StarSO");
        registry.planetTemplates = FindAssets<PlanetSO>("t:PlanetSO");
        registry.moonTemplates = FindAssets<MoonSO>("t:MoonSO");
        registry.stationTemplates = FindAssets<StationSO>("t:StationSO");
        registry.asteroidTemplates = FindAssets<AsteroidSO>("t:AsteroidSO");
        registry.gateTemplates = FindAssets<GateSO>("t:GateSO");
        registry.nebulaTemplates = FindAssets<NebulaSO>("t:NebulaSO");

        // 6. Configure Celestial Pool
        string prefabPath = "Assets/Prefabs/Celestial Prefabs/";
        pool.poolConfigs = new List<CelestialPool.PoolConfig>
        {
            CreateConfig(CelestialType.STAR, prefabPath + "StarPrefab.prefab", 5),
            CreateConfig(CelestialType.PLANET, prefabPath + "PlanetPrefab.prefab", 15),
            CreateConfig(CelestialType.MOON, prefabPath + "MoonPrefab.prefab", 25),
            CreateConfig(CelestialType.ASTEROID, prefabPath + "AsteroidPrefab.prefab", 100),
            CreateConfig(CelestialType.STATION, prefabPath + "StationPrefab.prefab", 5),
            CreateConfig(CelestialType.GATE, prefabPath + "GatePrefab.prefab", 2),
            CreateConfig(CelestialType.NEBULA, prefabPath + "NebulaPrefab.prefab", 2)
        };

        // 7. Link References
        skybox.starMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Celestial Prefabs/Materials/Stars/WhiteDwarf.mat");
        skybox.visualizer = visualizer; 

        // 8. Cleanup Scene
        GameObject oldLight = GameObject.Find("Directional Light");
        if (oldLight != null)
        {
            Object.DestroyImmediate(oldLight);
            Debug.Log("Cleaned up global Directional Light as stars now provide local system lighting.");
        }

        EditorUtility.SetDirty(mgr);
        EditorUtility.SetDirty(pool);
        EditorUtility.SetDirty(registry);
        EditorUtility.SetDirty(netMgr);

        Debug.Log("System-Centered Scene Setup Complete! \nPlease click 'Generate Universe V2' on the Generator.");
        Selection.activeObject = mgr;
    }

    private static List<T> FindAssets<T>(string filter) where T : UnityEngine.Object
    {
        List<T> assets = new List<T>();
        string[] guids = AssetDatabase.FindAssets(filter);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) assets.Add(asset);
        }
        return assets;
    }

    private static CelestialPool.PoolConfig CreateConfig(CelestialType type, string path, int size)
    {
        return new CelestialPool.PoolConfig
        {
            type = type,
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path),
            initialSize = size
        };
    }
}
#endif
