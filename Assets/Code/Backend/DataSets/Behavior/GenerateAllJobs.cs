using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct GenerateAllJobs : IJob
{
    public NativeList<GalaxyGenerationData> galaxies;
    public NativeList<SystemGenerationData> systems;
    public NativeList<StarGenerationData> stars;
    public NativeList<PlanetGenerationData> planets;
    public NativeList<MoonGenerationData> moons;
    public NativeList<AsteroidBeltGenerationData> asteroidBelts;
    public NativeList<AsteroidGenerationData> asteroids;
    public NativeList<StationGenerationData> stations;
    public NativeList<GateGenerationData> gates;

    [ReadOnly] public GenerationCosts costs;
    [ReadOnly] public NativeParallelHashMap<FixedString64Bytes, int> factionMap;

    [ReadOnly] public NativeArray<FixedString64Bytes> factionHomeGalaxy;
    [ReadOnly] public NativeArray<FixedList4096Bytes<FixedString64Bytes>> factionStarNames;
    [ReadOnly] public NativeArray<bool> factionRequiresNeb;
    [ReadOnly] public NativeArray<int> factionIds;

    public int maxStations;
    public int randomSeed;

    public void Execute()
    {
        var rnd = new Random((uint)randomSeed);
        for (int gi = 0; gi < galaxies.Length; gi++)
        {
            var g = galaxies[gi];
            int dust = g.dustParticles;
            int startSys = systems.Length;
            int sysCount = 0;

            for (int fi = 0; fi < factionHomeGalaxy.Length; fi++)
            {
                if (!factionHomeGalaxy[fi].Equals(g.galaxyName))
                {
                    continue;
                }
                else
                {
                    int factionId = factionIds[fi];
                    bool needsNeb = factionRequiresNeb[fi];
                    var names = factionStarNames[fi];

                    for (int s = 0; s < names.Length; s++)
                    {
                        // Build SystemGenerationData
                        SystemGenerationData sd = default;
                        sd.systemName = names[s];
                        sd.orbitParams = CalculateOrbitForSystem(g.orbitParams, sysCount);
                        sd.allegianceID = factionId;
                        sd.requiresNebula = needsNeb;

                        // Stars
                        sd.starsListStartIndex = stars.Length;
                        sd.starCount = 1;
                        CreateStars(ref sd, ref rnd);

                        // Planets & Moons
                        sd.planetsListStartIndex = planets.Length;
                        sd.planetCount = math.min(dust / costs.planet, 3);
                        CreatePlanets(ref sd, ref dust, ref rnd);

                        // Asteroid Belts & Asteroids
                        sd.asteroidBeltsListStartIndex = asteroidBelts.Length;
                        sd.asteroidBeltCount = rnd.NextInt(1, 4);
                        CreateBelts(ref sd, ref dust, ref rnd);

                        // Gates
                        sd.gatesListStartIndex = gates.Length;
                        sd.gateCount = rnd.NextInt(1, 3);
                        CreateGates(ref sd, ref rnd);

                        // Stations
                        sd.stationsListStartIndex = stations.Length;

                        // Always guarantee at least 1 station for faction systems
                        if (factionId != 0)
                        {
                            sd.stationCount = math.max(1, needsNeb ? 1 : math.min(dust / costs.planet, maxStations));
                        }
                        else
                        {
                            sd.stationCount = needsNeb ? 1 : math.min(dust / costs.planet, maxStations);
                        }

                        CreateStations(ref sd, ref rnd);


                        systems.Add(sd);
                        sysCount++;
                    }
                }
            }

            while (dust >= costs.system + costs.star && sysCount < 10)
            {
                dust -= costs.system + costs.star;
                SystemGenerationData sd = default;
                // use FixedString building for systemName
                FixedString64Bytes sysName = default;
                sysName.Append(g.galaxyName);
                sysName.Append(" System ");
                sysName.Append(sysCount + 1);
                sd.systemName = sysName;

                sd.orbitParams = CalculateOrbitForSystem(g.orbitParams, sysCount);
                sd.allegianceID = 0;
                sd.requiresNebula = rnd.NextFloat() < 0.2f;

                sd.starsListStartIndex = stars.Length;
                sd.starCount = 1;
                CreateStars(ref sd, ref rnd);

                sd.planetsListStartIndex = planets.Length;
                sd.planetCount = math.min(dust / costs.planet, 3);
                CreatePlanets(ref sd, ref dust, ref rnd);

                sd.asteroidBeltsListStartIndex = asteroidBelts.Length;
                sd.asteroidBeltCount = rnd.NextInt(1, 4);
                CreateBelts(ref sd, ref dust, ref rnd);

                sd.gatesListStartIndex = gates.Length;
                sd.gateCount = rnd.NextInt(1, 3);
                CreateGates(ref sd, ref rnd);

                sd.stationsListStartIndex = stations.Length;
                sd.stationCount = sd.requiresNebula ? 1 : math.min(dust / costs.planet, maxStations);
                CreateStations(ref sd, ref rnd);

                systems.Add(sd);
                sysCount++;
            }

            // 3. Update galaxy entry
            g.systemListStartIndex = startSys;
            g.systemCount = sysCount;
            g.dustParticles = dust;
            galaxies[gi] = g;
        }
    }

    // Helper methods to append bodies into the global lists
    void CreateStars(ref SystemGenerationData sd, ref Random rnd)
    {
        for (int i = 0; i < sd.starCount; i++)
        {
            StarGenerationData st = default;
            FixedString64Bytes name = default;
            name.Append(sd.systemName);
            name.Append(" Star ");
            name.Append(i + 1);
            st.starName = name;
            st.orbitParams = CalculateOrbitForStar(sd.orbitParams, i);
            stars.Add(st);
        }
    }

    void CreatePlanets(ref SystemGenerationData sd, ref int dust, ref Random rnd)
    {
        for (int i = 0; i < sd.planetCount; i++)
        {
            dust -= costs.planet;
            PlanetGenerationData pd = default;
            FixedString64Bytes pname = default;
            pname.Append(sd.systemName);
            pname.Append(" Planet ");
            pname.Append(i + 1);
            pd.planetName = pname;

            pd.orbitParams = CalculateOrbitForPlanet(sd.orbitParams, i);
            pd.moonListStartIndex = moons.Length;
            pd.moonCount = math.min(dust / costs.moon, 2);

            for (int m = 0; m < pd.moonCount; m++)
            {
                dust -= costs.moon;
                MoonGenerationData md = default;
                FixedString64Bytes mname = default;
                mname.Append(pd.planetName);
                mname.Append(" Moon ");
                mname.Append(m + 1);
                md.moonName = mname;
                md.orbitParams = CalculateOrbitForMoon(pd.orbitParams, m);
                moons.Add(md);
            }
            planets.Add(pd);
        }
    }

    void CreateBelts(ref SystemGenerationData sd, ref int dust, ref Random rnd)
    {
        for (int b = 0; b < sd.asteroidBeltCount; b++)
        {
            dust -= costs.asteroidBelt;
            AsteroidBeltGenerationData bd = default;
            FixedString64Bytes bname = default;
            bname.Append(sd.systemName);
            bname.Append(" Belt ");
            bname.Append(b + 1);
            bd.asteroidBeltName = bname;

            bd.orbitParams = CalculateOrbitForBelt(sd.orbitParams, b);
            bd.asteroidListStartIndex = asteroids.Length;
            bd.asteroidCount = rnd.NextInt(3, 6);
            for (int a = 0; a < bd.asteroidCount; a++)
            {
                dust -= costs.asteroid;
                AsteroidGenerationData ad = default;
                FixedString64Bytes aname = default;
                aname.Append(bd.asteroidBeltName);
                aname.Append(" Asteroid ");
                aname.Append(a + 1);
                ad.asteroidName = aname;
                ad.orbitParams = CalculateOrbitForAsteroid(bd.orbitParams, a);
                asteroids.Add(ad);
            }
            asteroidBelts.Add(bd);
        }
    }

    void CreateGates(ref SystemGenerationData sd, ref Random rnd)
    {
        for (int gI = 0; gI < sd.gateCount; gI++)
        {
            GateGenerationData gd = default;
            FixedString64Bytes gname = default;
            gname.Append(sd.systemName);
            gname.Append(" Gate ");
            gname.Append(gI + 1);
            gd.gateName = gname;
            gd.orbitParams = CalculateOrbitForGate(sd.orbitParams, gI);
            gates.Add(gd);
        }
    }

    void CreateStations(ref SystemGenerationData sd, ref Random rnd)
    {
        // Count total moons in this system
        int totalMoons = 0;
        for (int pi = sd.planetsListStartIndex; pi < sd.planetsListStartIndex + sd.planetCount; pi++)
        {
            var pd = planets[pi];
            totalMoons += pd.moonCount;
        }

        for (int sI = 0; sI < sd.stationCount; sI++)
        {
            StationGenerationData sdg = default;
            FixedString64Bytes sname = default;
            sname.Append(sd.systemName);
            sname.Append(" Station ");
            sname.Append(sI + 1);
            sdg.stationName = sname;

            sdg.allegianceID = sd.allegianceID;

            if (totalMoons > 0)
            {
                // pick a random moon index among the system's moons
                int pick = rnd.NextInt(0, totalMoons); // 0..totalMoons-1
                int foundMoonIndex = -1;

                // walk planets to locate the pick'th moon
                for (int pi = sd.planetsListStartIndex; pi < sd.planetsListStartIndex + sd.planetCount; pi++)
                {
                    var pd = planets[pi];
                    if (pick < pd.moonCount)
                    {
                        foundMoonIndex = pd.moonListStartIndex + pick;
                        break;
                    }
                    pick -= pd.moonCount;
                }

                if (foundMoonIndex >= 0)
                {
                    var moonOrbit = moons[foundMoonIndex].orbitParams;
                    sdg.orbitParams = CalculateOrbitForStation(moonOrbit, sI);
                    // CalculateOrbitForStation sets ParentId to moonOrbit.NetworkId
                }
                else
                {
                    // fallback to system orbit if something went wrong
                    sdg.orbitParams = CalculateOrbitForStation(sd.orbitParams, sI);
                }
            }
            else
            {
                // no moons: fallback (still create, parent to system)
                sdg.orbitParams = CalculateOrbitForStation(sd.orbitParams, sI);
            }

            stations.Add(sdg);
        }
    }

    private OrbitParams CalculateOrbitForSystem(OrbitParams parentOrbit, int index)
    {
        float radius = 16f + (index * 7.5f);
        float angularSpeed = 0.13f;
        float phaseOffset = 0f; // can randomize if desired
        return new OrbitParams(
            parentOrbit.NetworkId + 1u,    // networkId
            parentOrbit.NetworkId,         // parentId
            parentOrbit.CenterPos,
            radius,
            angularSpeed,
            phaseOffset,
            CelestialType.SYSTEM,
            true
        );
    }

    private OrbitParams CalculateOrbitForStar(OrbitParams parentOrbit, int index)
    {
        float radius = 4.5f;
        float angularSpeed = 0.17f;
        float phaseOffset = 0f;
        return new OrbitParams(
            parentOrbit.NetworkId + 10u + (ulong)index,
            parentOrbit.NetworkId,
            parentOrbit.CenterPos,
            radius,
            angularSpeed,
            phaseOffset,
            CelestialType.STAR,
            false
        );
    }

    private OrbitParams CalculateOrbitForPlanet(OrbitParams parentOrbit, int index)
    {
        float radius = 7f + (index * 3f);
        float angularSpeed = 7f / radius;
        float phaseOffset = 0f;
        return new OrbitParams(
            parentOrbit.NetworkId + 20u + (ulong)index,
            parentOrbit.NetworkId,
            parentOrbit.CenterPos,
            radius,
            angularSpeed,
            phaseOffset,
            CelestialType.PLANET,
            false
        );
    }

    private OrbitParams CalculateOrbitForMoon(OrbitParams parentOrbit, int index)
    {
        float radius = 2f + (index * 1.2f);
        float angularSpeed = 1.2f / radius;
        float phaseOffset = 0f;
        return new OrbitParams(
            parentOrbit.NetworkId + 30u + (ulong)index,
            parentOrbit.NetworkId,
            parentOrbit.CenterPos,
            radius,
            angularSpeed,
            phaseOffset,
            CelestialType.MOON,
            false
        );
    }

    private OrbitParams CalculateOrbitForBelt(OrbitParams parentOrbit, int index)
    {
        float radius = 12f + (index * 1.8f);
        float angularSpeed = 1.8f / radius;
        float phaseOffset = 0f;
        return new OrbitParams(
            parentOrbit.NetworkId + 40u + (ulong)index,
            parentOrbit.NetworkId,
            parentOrbit.CenterPos,
            radius,
            angularSpeed,
            phaseOffset,
            CelestialType.ASTEROIDBELT,
            false
        );
    }

    private OrbitParams CalculateOrbitForAsteroid(OrbitParams parentOrbit, int index)
    {
        float radius = 0.5f;
        float angularSpeed = 0.3f + (0.3f * (index % 2));
        float phaseOffset = 0f;
        return new OrbitParams(
            parentOrbit.NetworkId + 50u + (ulong)index,
            parentOrbit.NetworkId,
            parentOrbit.CenterPos,
            radius,
            angularSpeed,
            phaseOffset,
            CelestialType.ASTEROID,
            true
        );
    }

    private OrbitParams CalculateOrbitForGate(OrbitParams parentOrbit, int index)
    {
        float radius = 2f;
        float angularSpeed = 0f;
        float phaseOffset = 0f;
        return new OrbitParams(
            parentOrbit.NetworkId + 60u + (ulong)index,
            parentOrbit.NetworkId,
            parentOrbit.CenterPos,
            radius,
            angularSpeed,
            phaseOffset,
            CelestialType.GATE,
            true
        );
    }

    private OrbitParams CalculateOrbitForStation(OrbitParams parentOrbit, int index)
    {
        float radius = 0.4f;
        float angularSpeed = 0.13f;
        float phaseOffset = 0f;
        return new OrbitParams(
            parentOrbit.NetworkId + 70u + (ulong)index,
            parentOrbit.NetworkId,
            parentOrbit.CenterPos,
            radius,
            angularSpeed,
            phaseOffset,
            CelestialType.STATION,
            true
        );
    }
}
