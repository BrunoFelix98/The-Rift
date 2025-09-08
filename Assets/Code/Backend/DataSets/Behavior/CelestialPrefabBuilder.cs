using UnityEngine;

public static class CelestialPrefabBuilder
{
    public static void AddStarComponents(GameObject go, CelestialEnvironment environment, StarSO starSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var starData = go.AddComponent<StarData>();
        starData.data = starSO;
        var light = go.AddComponent<Light>();

        // Configure light based on star type
        switch (environment)
        {
            case CelestialEnvironment.TEMPERATE: // Hot stars
                light.color = Color.white;
                light.intensity = 3f;
                break;
            case CelestialEnvironment.GAS: // Very hot stars
                light.color = Color.blue;
                light.intensity = 5f;
                break;
            case CelestialEnvironment.ICE: // Cooler red dwarf
                light.color = Color.red;
                light.intensity = 1.5f;
                break;
            case CelestialEnvironment.ROCKY: // Old star
                light.color = Color.orange;
                light.intensity = 2f;
                break;
            default:
                light.color = Color.yellow;
                light.intensity = 2f;
                break;
        }

        light.type = LightType.Point;
        light.range = 100f;

        // Add particle system for solar flares/corona effects
        var particleSystem = go.AddComponent<ParticleSystem>();
        var main = particleSystem.main;
        main.startColor = light.color;
        main.startLifetime = 5f;
        main.startSpeed = 2f;
        main.maxParticles = 100;

        // Add audio source for ambient star sounds
        var audioSource = go.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = true;
        audioSource.volume = 0.3f;
        audioSource.spatialBlend = 1f; // 3D sound
    }

    public static void AddPlanetComponents(GameObject go, CelestialEnvironment environment, PlanetSO planetSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var sphereCollider = go.AddComponent<SphereCollider>();
        var planetData = go.AddComponent<PlanetData>();
        planetData.data = planetSO;

        // Configure scale based on environment
        float scale = environment switch
        {
            CelestialEnvironment.GAS => UnityEngine.Random.Range(3f, 6f),     // Gas giants are huge
            CelestialEnvironment.ICE => UnityEngine.Random.Range(0.8f, 1.5f),  // Ice planets are smaller
            CelestialEnvironment.ROCKY => UnityEngine.Random.Range(1f, 2f),   // Rocky planets are medium
            CelestialEnvironment.TEMPERATE => UnityEngine.Random.Range(2f, 2.5f), // Volcanic planets
            _ => UnityEngine.Random.Range(1f, 2f)
        };
        go.transform.localScale = Vector3.one * scale;

        // Add atmosphere effect for gas planets
        if (environment == CelestialEnvironment.GAS)
        {
            var atmosphereGO = new GameObject("Atmosphere");
            atmosphereGO.transform.SetParent(go.transform);
            var atmMeshFilter = atmosphereGO.AddComponent<MeshFilter>();
            var atmMeshRenderer = atmosphereGO.AddComponent<MeshRenderer>();
            atmosphereGO.transform.localScale = Vector3.one * 1.1f; // Slightly larger than planet
            atmosphereGO.transform.localPosition = Vector3.zero;
        }

        // Add particle effects for certain environments
        if (environment == CelestialEnvironment.ICE)
        {
            var particleSystem = go.AddComponent<ParticleSystem>();
            var main = particleSystem.main;

            main.startColor = Color.cyan;
            main.startLifetime = 10f;

            main.maxParticles = 50;
            main.startSpeed = 0.5f;
        }

        // Add rotation component for planet spinning
        var rotator = go.AddComponent<SimpleRotator>();
        rotator.rotationSpeed = new Vector3(0, UnityEngine.Random.Range(5f, 15f), 0);
    }

    public static void AddMoonComponents(GameObject go, CelestialEnvironment environment, MoonSO moonSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var sphereCollider = go.AddComponent<SphereCollider>();
        var moonData = go.AddComponent<MoonData>();
        moonData.data = moonSO;

        // Moons are generally smaller than planets
        float scale = environment switch
        {
            CelestialEnvironment.GAS => UnityEngine.Random.Range(0.3f, 0.6f),
            CelestialEnvironment.ICE => UnityEngine.Random.Range(0.2f, 0.5f),
            CelestialEnvironment.ROCKY => UnityEngine.Random.Range(0.3f, 0.7f),
            CelestialEnvironment.TEMPERATE => UnityEngine.Random.Range(0.2f, 0.3f),
            _ => UnityEngine.Random.Range(0.3f, 0.6f)
        };
        go.transform.localScale = Vector3.one * scale;

        // Add rotation (moons typically rotate slower than planets)
        var rotator = go.AddComponent<SimpleRotator>();
        rotator.rotationSpeed = new Vector3(0, UnityEngine.Random.Range(2f, 8f), 0);

        // Add crater effects for rocky moons
        if (environment == CelestialEnvironment.ROCKY)
        {
            // You could add crater decals or additional geometry here
        }
    }

    public static void AddNebulaComponents(GameObject go, NebulaSO nebulaSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var particleSystem = go.AddComponent<ParticleSystem>();
        var nebulaData = go.AddComponent<NebulaData>();
        nebulaData.data = nebulaSO;

        // Configure particle system for nebula effects
        var main = particleSystem.main;
        main.startLifetime = float.MaxValue; // Particles don't die
        main.startSpeed = 0.1f;
        main.maxParticles = 1000;
        main.startSize = UnityEngine.Random.Range(0.5f, 2f);
        main.startColor = new Color(
            UnityEngine.Random.Range(0.3f, 1f),
            UnityEngine.Random.Range(0.1f, 0.8f),
            UnityEngine.Random.Range(0.5f, 1f),
            0.3f // Semi-transparent
        );

        // Add shape module for volume emission
        var shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(20f, 10f, 20f);

        // Add velocity over lifetime for gentle movement
        var velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);

        // Scale up the nebula
        go.transform.localScale = Vector3.one * UnityEngine.Random.Range(5f, 15f);

        // Add audio for ambient nebula sounds
        var audioSource = go.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = true;
        audioSource.volume = 0.1f;
        audioSource.spatialBlend = 1f;
    }

    public static void AddAsteroidComponents(GameObject go, CelestialEnvironment environment, AsteroidSO asteroidSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var meshCollider = go.AddComponent<MeshCollider>();
        meshCollider.convex = true; // Required for rigidbodies

        // Add rigidbody for physics interactions
        var rigidbody = go.AddComponent<Rigidbody>();
        rigidbody.mass = UnityEngine.Random.Range(10f, 100f);
        rigidbody.linearDamping = 0.5f;
        rigidbody.angularDamping = 0.5f;

        // Add mining-related components
        var asteroidData = go.AddComponent<AsteroidData>();
        asteroidData.data = asteroidSO;

        // Vary size based on environment
        float scale = environment switch
        {
            CelestialEnvironment.ROCKY => UnityEngine.Random.Range(0.5f, 2f),
            CelestialEnvironment.GAS => UnityEngine.Random.Range(0.3f, 1.5f),
            CelestialEnvironment.ICE => UnityEngine.Random.Range(0.4f, 1.8f),
            CelestialEnvironment.TEMPERATE => UnityEngine.Random.Range(0.6f, 1.2f),
            _ => UnityEngine.Random.Range(0.5f, 1.5f)
        };
        go.transform.localScale = Vector3.one * scale;

        // Add rotation for tumbling effect
        var rotator = go.AddComponent<SimpleRotator>();
        rotator.rotationSpeed = new Vector3(
            UnityEngine.Random.Range(-30f, 30f),
            UnityEngine.Random.Range(-30f, 30f),
            UnityEngine.Random.Range(-30f, 30f)
        );
    }

    public static void AddStationComponents(GameObject go, StationSO stationSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var boxCollider = go.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true; // Allow ships to dock

        // Add multiple lights for station visibility
        var mainLight = go.AddComponent<Light>();
        mainLight.type = LightType.Point;
        mainLight.color = Color.cyan;
        mainLight.intensity = 2f;
        mainLight.range = 20f;

        // Add blinking navigation lights
        for (int i = 0; i < 4; i++)
        {
            var navLightGO = new GameObject($"NavLight_{i}");
            navLightGO.transform.SetParent(go.transform);
            navLightGO.transform.localPosition = new Vector3(
                UnityEngine.Random.Range(-2f, 2f),
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-2f, 2f)
            );

            var navLight = navLightGO.AddComponent<Light>();
            navLight.type = LightType.Point;
            navLight.color = Color.red;
            navLight.intensity = 0.5f;
            navLight.range = 5f;

            var blinker = navLightGO.AddComponent<LightBlinker>();
            blinker.blinkInterval = UnityEngine.Random.Range(1f, 3f);
        }

        // Add docking bay trigger
        var dockingBayGO = new GameObject("DockingBay");
        dockingBayGO.transform.SetParent(go.transform);
        dockingBayGO.transform.localPosition = new Vector3(0, -1f, 0);
        var dockingCollider = dockingBayGO.AddComponent<BoxCollider>();
        dockingCollider.isTrigger = true;
        dockingCollider.size = new Vector3(5f, 2f, 5f);

        // Add station behavior component
        var stationData = go.AddComponent<StationData>();
        stationData.data = stationSO;

        // Add audio for station ambience
        var audioSource = go.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = true;
        audioSource.volume = 0.4f;
        audioSource.spatialBlend = 1f;

        // Add rotation (stations typically rotate for artificial gravity)
        var rotator = go.AddComponent<SimpleRotator>();
        rotator.rotationSpeed = new Vector3(0, 5f, 0);
    }

    public static void AddGateComponents(GameObject go, GateSO gateSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var boxCollider = go.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true; // Gates should be triggers for travel
        boxCollider.size = new Vector3(10f, 10f, 2f); // Large trigger area

        // Add gate-specific visual effects
        var particleSystem = go.AddComponent<ParticleSystem>();
        var main = particleSystem.main;
        main.startColor = Color.magenta;
        main.startLifetime = 2f;
        main.startSpeed = 3f;
        main.maxParticles = 200;
        main.startSize = 0.5f;

        // Configure shape for gate effect
        var shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = 5f;
        shape.donutRadius = 0.5f;

        // Add distinctive lighting
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Color.magenta;
        light.intensity = 3f;
        light.range = 25f;

        // Add pulsing light effect
        var lightPulser = go.AddComponent<LightPulser>();
        lightPulser.pulseSpeed = 2f;
        lightPulser.minIntensity = 1f;
        lightPulser.maxIntensity = 5f;

        // Add gate behavior component
        var gateData = go.AddComponent<GateData>();
        gateData.data = gateSO;

        // Add audio for gate activation
        var audioSource = go.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; // Only play when activated
        audioSource.volume = 0.8f;
        audioSource.spatialBlend = 1f;

        // Add rotation for gate spinning effect
        var rotator = go.AddComponent<SimpleRotator>();
        rotator.rotationSpeed = new Vector3(0, 0, 20f); // Spin around Z-axis
    }

    public static void AddAsteroidBeltComponents(GameObject go, CelestialEnvironment environment, AsteroidBeltSO asteroidBeltSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var particleSystem = go.AddComponent<ParticleSystem>();

        // Configure belt particle system
        var main = particleSystem.main;
        main.startLifetime = float.MaxValue; // Particles persist
        main.startSpeed = 0f; // Stationary particles
        main.maxParticles = 2000;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);

        // Configure particles based on environment
        if (environment == CelestialEnvironment.ROCKY)
        {
            main.startColor = new Color(0.6f, 0.5f, 0.4f, 0.8f); // Rocky brown
        }
        else // GAS or other
        {
            main.startColor = new Color(0.8f, 0.6f, 1f, 0.5f); // Ethereal purple
        }

        // Configure belt shape
        var shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = 15f;
        shape.donutRadius = 3f;
        shape.arc = 360f;

        // Add slow rotation to the belt
        var velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.orbitalX = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);

        // Add belt behavior component
        var asteroidBeltData = go.AddComponent<AsteroidBeltData>();
        asteroidBeltData.data = asteroidBeltSO;

        // Scale the belt
        go.transform.localScale = Vector3.one * UnityEngine.Random.Range(1f, 2f);
    }

    public static void AddSystemComponents(GameObject go, SystemSO systemSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();

        // Systems are typically invisible organizational nodes, but add a subtle marker
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Color.white;
        light.intensity = 0.5f;
        light.range = 5f;
        light.enabled = false; // Disabled by default, can be enabled for debugging

        // Add system behavior for managing allegiances
        var systemData = go.AddComponent<SystemData>();
        systemData.data = systemSO;
        // Very small scale since it's just an organizational node
        go.transform.localScale = Vector3.one * 0.1f;
    }

    public static void AddGalaxyComponents(GameObject go, GalaxySO galaxySO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();

        // Galaxies are even more abstract - just organizational nodes
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Color.yellow;
        light.intensity = 0.3f;
        light.range = 10f;
        light.enabled = false; // Disabled by default

        // Add galaxy behavior for large-scale management
        var galaxyData = go.AddComponent<GalaxyData>();
        galaxyData.data = galaxySO;
        // Very small scale
        go.transform.localScale = Vector3.one * 0.05f;
    }
}