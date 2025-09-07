using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class NetworkedGalaxyVisualizer : NetworkBehaviour
{
    [Header("Galaxy Data")]
    public List<GalaxySO> galaxyAssets = new List<GalaxySO>();

    [Header("Prefab References")]
    public GameObject starPrefab;
    public GameObject planetPrefab;
    public GameObject moonPrefab;
    public GameObject stationPrefab;
    public GameObject asteroidPrefab;
    public GameObject nebulaPrefab;
    public GameObject asteroidBeltPrefab;
    public GameObject gatePrefab;
    public GameObject centerPrefab;

    [Header("Orbit Parameters")]
    public float galaxyRadiusMul = 1f, galaxySpeedMin = 0.01f, galaxySpeedMax = 0.045f;
    public float systemBase = 16f, systemStep = 7.5f, systemSpeed = 0.13f;
    public float starRadius = 4.5f, starSpeed = 0.17f;
    public float planetBase = 7f, planetStep = 3f;
    public float moonBase = 2f, moonStep = 1.2f;
    public float stationRadius = 0.4f;
    public float beltBase = 12f, beltStep = 1.8f;
    public float nebulaRadius = 25f, nebulaSpeed = 0.05f;

    [Header("LOD Settings")]
    public float viewDistance = 100f;

    public List<OrbitParams> allOrbits = new List<OrbitParams>();
    private Dictionary<ulong, GameObject> clientInstances = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, NetworkObject> networkedObjects = new(); // Only for networked objects
    private Dictionary<ulong, GameObject> staticObjects = new();       // For non-networked visuals

    private HashSet<ulong> visibleOnClient = new();

    // Map OrbitParams by ID for quick lookup
    private Dictionary<ulong, OrbitParams> orbitById;
    // Track spawned NetworkObjects on server
    private Dictionary<ulong, NetworkObject> serverSpawned = new();

    public static NetworkedGalaxyVisualizer Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            BuildAllOrbits();                       // fills allOrbits & orbitById
            StartCoroutine(SpawnObjectsInBatches());
        }
    }

    private IEnumerator SpawnObjectsInBatches()
    {
        const int batchSize = 50;
        int index = 0;

        while (index < allOrbits.Count)
        {
            int end = Mathf.Min(index + batchSize, allOrbits.Count);

            for (int i = index; i < end; i++)
            {
                var orbit = allOrbits[i];
                SpawnCelestialObject(orbit);
            }

            index = end;
            yield return null;
        }
    }

    private void SpawnCelestialObject(OrbitParams orbit)
    {
        // Get parent transform
        Transform parent = GetParentTransform(orbit.ParentId);

        // Get the prefab from the orbit data
        GameObject prefab = GetPrefabForOrbit(orbit); // You'll implement this based on your SO references

        if (prefab == null) return;

        // Instantiate
        GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
        instance.name = $"{orbit.Type}_{orbit.NetworkId}";

        if (orbit.RequiresNetworking)
        {
            // Network objects (asteroids, stations, gates)
            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                networkedObjects[orbit.NetworkId] = netObj;
            }
        }
        else
        {
            // Static objects (stars, planets, moons, nebulas)
            staticObjects[orbit.NetworkId] = instance;
        }
    }

    private void UpdateOrbitalPositions()
    {
        foreach (var orbit in allOrbits)
        {
            GameObject obj = null;

            if (orbit.RequiresNetworking && networkedObjects.TryGetValue(orbit.NetworkId, out var netObj))
            {
                obj = netObj.gameObject;
            }
            else if (!orbit.RequiresNetworking && staticObjects.TryGetValue(orbit.NetworkId, out var staticObj))
            {
                obj = staticObj;
            }

            if (obj != null)
            {
                // Calculate orbital position
                float t = Time.time * orbit.AngularSpeed + orbit.PhaseOffset;
                Vector3 offset = new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t)) * orbit.Radius;
                obj.transform.localPosition = offset;
                obj.transform.localRotation = Quaternion.Euler(0, t * Mathf.Rad2Deg, 0);
            }
        }
    }

    private GameObject PrefabFor(CelestialType type) => type switch
    {
        CelestialType.STAR => starPrefab,
        CelestialType.PLANET => planetPrefab,
        CelestialType.MOON => moonPrefab,
        CelestialType.ASTEROIDBELT => asteroidBeltPrefab,
        CelestialType.ASTEROID => asteroidPrefab,
        CelestialType.STATION => stationPrefab,
        CelestialType.GATE => gatePrefab,
        CelestialType.NEBULA => nebulaPrefab,
        CelestialType.SYSTEM => centerPrefab,
        CelestialType.GALAXY => centerPrefab,
        _ => centerPrefab
    };

    [ClientRpc]
    public void SendOrbitBatchClientRpc(OrbitParams[] orbits, ClientRpcParams rpcParams)
    {
        if (allOrbits == null) allOrbits = new List<OrbitParams>();
        allOrbits.AddRange(orbits);
    }

    private void LateUpdate()
    {
        // Update positions for all objects (both networked and static)
        UpdateOrbitalPositions();

        if (IsClient)
        {
            UpdateLOD();
        }
    }

    private ulong AddOrbit(ulong parentId, GameObject prefab, CelestialType type, bool requiresNetworking)
    {
        ulong newId = (ulong)allOrbits.Count + 1;

        // Calculate orbit parameters based on type and hierarchy
        var orbitParams = CalculateOrbitParams(newId, parentId, prefab, type, requiresNetworking);

        allOrbits.Add(orbitParams);
        orbitById[newId] = orbitParams;

        return newId;
    }

    private void BuildAllOrbits()
    {
        orbitById = new Dictionary<ulong, OrbitParams>();

        foreach (var galaxy in galaxyAssets)
        {
            foreach (var system in galaxy.systems)
            {
                // Add stars (not networked)
                foreach (var star in system.stars)
                {
                    AddOrbit(0, star.prefabReference, CelestialType.STAR, requiresNetworking: false);
                }

                // Add planets (not networked)
                foreach (var planet in system.planets)
                {
                    ulong planetId = AddOrbit(0, planet.prefabReference, CelestialType.PLANET, requiresNetworking: false);

                    // Add moons (not networked)
                    foreach (var moon in planet.moons)
                    {
                        ulong moonId = AddOrbit(planetId, moon.prefabReference, CelestialType.MOON, requiresNetworking: false);

                        // Add stations (networked)
                        foreach (var station in moon.stations)
                        {
                            AddOrbit(moonId, station.prefabReference, CelestialType.STATION, requiresNetworking: true);
                        }
                    }
                }

                // Add asteroid belts and asteroids (networked)
                foreach (var belt in system.asteroidBelts)
                {
                    ulong beltId = AddOrbit(0, belt.prefabReference, CelestialType.ASTEROIDBELT, requiresNetworking: false);

                    foreach (var asteroid in belt.asteroids)
                    {
                        AddOrbit(beltId, asteroid.prefabReference, CelestialType.ASTEROID, requiresNetworking: true);
                    }
                }

                // Add gates (networked)
                foreach (var gate in system.gates)
                {
                    AddOrbit(0, gate.prefabReference, CelestialType.GATE, requiresNetworking: true);
                }
            }
        }
    }

    private void UpdateLOD()
    {
        if (NetworkManager.Singleton.LocalClient?.PlayerObject == null) return;

        Vector3 playerPos = NetworkManager.Singleton.LocalClient.PlayerObject.transform.position;

        // Enable/disable renderers based on distance
        foreach (var orbit in allOrbits)
        {
            float dist = Vector3.Distance(orbit.CenterPos, playerPos);
            bool shouldBeVisible = dist <= viewDistance;

            GameObject obj = GetObjectForOrbit(orbit);
            if (obj != null)
            {
                var renderers = obj.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    renderer.enabled = shouldBeVisible;
                }
            }
        }
    }

    private Transform GetParentTransform(ulong parentId)
    {
        if (parentId == 0) return null; // Root level

        // Check networked objects first
        if (networkedObjects.TryGetValue(parentId, out var netObj))
            return netObj.transform;

        // Check static objects
        if (staticObjects.TryGetValue(parentId, out var staticObj))
            return staticObj.transform;

        return null;
    }

    private GameObject GetPrefabForOrbit(OrbitParams orbit)
    {
        // Since your SOs likely don't have prefabReference yet, use the existing prefab system
        return PrefabFor(orbit.Type);
    }

    private OrbitParams CalculateOrbitParams(ulong newId, ulong parentId, GameObject prefab, CelestialType type, bool requiresNetworking)
    {
        // Get parent position for center calculation
        Vector3 centerPos = Vector3.zero;
        if (parentId != 0 && orbitById.TryGetValue(parentId, out var parent))
        {
            centerPos = parent.CenterPos;
        }

        // Calculate orbit parameters based on type
        float radius = 0f;
        float angularSpeed = 0f;

        switch (type)
        {
            case CelestialType.GALAXY:
                radius = UnityEngine.Random.Range(100f, 1000f) * galaxyRadiusMul;
                angularSpeed = UnityEngine.Random.Range(galaxySpeedMin, galaxySpeedMax);
                break;
            case CelestialType.SYSTEM:
                radius = systemBase + (GetSystemIndex() * systemStep);
                angularSpeed = systemSpeed;
                break;
            case CelestialType.STAR:
                radius = starRadius;
                angularSpeed = starSpeed;
                break;
            case CelestialType.PLANET:
                radius = planetBase + (GetPlanetIndex() * planetStep);
                angularSpeed = planetBase / radius;
                break;
            case CelestialType.MOON:
                radius = moonBase + (GetMoonIndex() * moonStep);
                angularSpeed = moonStep / radius;
                break;
            case CelestialType.STATION:
                radius = stationRadius;
                angularSpeed = systemSpeed;
                break;
            case CelestialType.ASTEROIDBELT:
                radius = beltBase + (GetBeltIndex() * beltStep);
                angularSpeed = beltStep / radius;
                break;
            case CelestialType.ASTEROID:
                radius = 0.5f;
                angularSpeed = UnityEngine.Random.Range(0.3f, 0.6f);
                break;
            case CelestialType.GATE:
                radius = 2f;
                angularSpeed = 0f;
                break;
            case CelestialType.NEBULA:
                radius = nebulaRadius;
                angularSpeed = nebulaSpeed;
                break;
        }

        return new OrbitParams
        {
            NetworkId = newId,
            ParentId = parentId,
            CenterPos = centerPos,
            Radius = radius,
            AngularSpeed = angularSpeed,
            PhaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
            Type = type,
            RequiresNetworking = requiresNetworking
        };
    }

    private int GetSystemIndex()
    {
        return allOrbits.Count(o => o.Type == CelestialType.SYSTEM);
    }

    private int GetPlanetIndex()
    {
        return allOrbits.Count(o => o.Type == CelestialType.PLANET);
    }

    private int GetMoonIndex()
    {
        return allOrbits.Count(o => o.Type == CelestialType.MOON);
    }

    private int GetBeltIndex()
    {
        return allOrbits.Count(o => o.Type == CelestialType.ASTEROIDBELT);
    }

    private GameObject GetObjectForOrbit(OrbitParams orbit)
    {
        if (orbit.RequiresNetworking && networkedObjects.TryGetValue(orbit.NetworkId, out var netObj))
            return netObj.gameObject;

        if (!orbit.RequiresNetworking && staticObjects.TryGetValue(orbit.NetworkId, out var staticObj))
            return staticObj;

        return null;
    }
}