using System.Collections.Generic;
using UnityEngine;
public class GalaxyVisualizer : MonoBehaviour
{
    [Header("Galaxy Data")]
    public List<GalaxySO> galaxyAssets = new List<GalaxySO>();
    private List<OrbitingObject> orbitingObjects = new List<OrbitingObject>();
    private GameObject universeCenterObj;
    [Header("Orbit Parameters")]
    // Controls how far galaxies orbit from universe center (multiplies generated radius)
    public float galaxyOrbitRadiusMultiplier = 1f;
    // Controls speed galaxies orbit around universe center
    public float galaxyOrbitSpeedMin = 0.01f;
    public float galaxyOrbitSpeedMax = 0.045f;
    // Controls how far systems orbit from their galaxy center
    public float systemOrbitRadiusBase = 16f;
    public float systemOrbitRadiusStep = 7.5f;
    // Controls speed systems orbit around galaxy center
    public float systemOrbitSpeed = 0.13f;
    // Controls how far stars orbit around system star cluster center
    public float starOrbitRadius = 4.5f;
    // Controls speed stars orbit
    public float starOrbitSpeed = 0.17f;
    // Controls how far planets orbit star cluster center
    public float planetOrbitRadiusBase = 7f;
    public float planetOrbitRadiusStep = 3f;
    // Controls planet orbit speed scaling
    public float planetOrbitSpeedScale = 0.2f;
    // Controls how far moons orbit their planets
    public float moonOrbitRadiusBase = 2f;
    public float moonOrbitRadiusStep = 1.2f;
    // Controls moon orbit speed scaling
    public float moonOrbitSpeedScale = 0.3f;
    // Station orbit parameters
    public float stationOrbitRadius = 0.4f;
    public float stationOrbitSpeed = 0.4f;
    // Asteroid belt orbit radius base and step
    public float beltOrbitRadiusBase = 12f;
    public float beltOrbitRadiusStep = 1.8f;
    // Asteroid belt orbit speed scaling
    public float beltOrbitSpeedScale = 0.1f;
    void Start()
    {
        VisualizeAllGalaxies();
    }
    private void Update()
    {
        float deltaTime = Time.deltaTime;
        foreach (var orbitObj in orbitingObjects)
        {
            orbitObj.UpdatePosition(deltaTime * 20f);
        }
    }
    private void VisualizeAllGalaxies()
    {
        if (universeCenterObj == null)
        {
            universeCenterObj = new GameObject("UniverseCenter");
            universeCenterObj.transform.position = Vector3.zero;
        }
        List<Vector3> galaxyPositions = new List<Vector3>();
        for (int g = 0; g < galaxyAssets.Count; g++)
        {
            float theta = Mathf.Deg2Rad * (360f * g / Mathf.Max(1, galaxyAssets.Count));
            float radius = Random.Range(25f, 100f) * galaxyOrbitRadiusMultiplier;
            Vector3 pos = new Vector3(
                Mathf.Cos(theta) * radius + Random.Range(-10f, 10f),
                Random.Range(-10f, 10f),
                Mathf.Sin(theta) * radius + Random.Range(-10f, 10f)
            );
            galaxyPositions.Add(pos);
        }
        for (int g = 0; g < galaxyAssets.Count; g++)
        {
            GalaxySO galaxySO = galaxyAssets[g];
            Vector3 galaxyPos = galaxyPositions[g];
            GameObject galaxyObj = new GameObject(galaxySO.galaxyName + "_Center");
            galaxyObj.transform.parent = universeCenterObj.transform;
            galaxyObj.transform.position = galaxyPos;
            float orbitRadius = galaxyObj.transform.localPosition.magnitude;
            float orbitSpeed = Random.Range(galaxyOrbitSpeedMin, galaxyOrbitSpeedMax);
            orbitingObjects.Add(new OrbitingObject(galaxyObj, universeCenterObj, orbitRadius, orbitSpeed));
            float systemAngleOffset = 360f / Mathf.Max(1, galaxySO.systems.Count);
            for (int s = 0; s < galaxySO.systems.Count; s++)
            {
                SystemSO systemSO = galaxySO.systems[s];
                float systemTheta = Mathf.Deg2Rad * (systemAngleOffset * s);
                float systemOrbitRadius = systemOrbitRadiusBase + s * systemOrbitRadiusStep;
                Vector3 systemLocalPos = new Vector3(
                    Mathf.Cos(systemTheta) * systemOrbitRadius,
                    0,
                    Mathf.Sin(systemTheta) * systemOrbitRadius
                );
                Vector3 systemWorldPos = galaxyObj.transform.position + systemLocalPos;
                GameObject systemObj = new GameObject(systemSO.systemName + "_SystemCenter");
                systemObj.transform.parent = galaxyObj.transform;
                systemObj.transform.position = systemWorldPos;
                orbitingObjects.Add(new OrbitingObject(systemObj, galaxyObj, systemOrbitRadius, systemOrbitSpeed));
                GameObject starClusterObj = new GameObject(systemSO.systemName + "_StarClusterCenter");
                starClusterObj.transform.parent = systemObj.transform;
                starClusterObj.transform.position = systemObj.transform.position;
                List<GameObject> starObjs = new List<GameObject>();
                for (int st = 0; st < systemSO.stars.Count; st++)
                {
                    StarSO starSO = systemSO.stars[st];
                    float starAngle = Mathf.Deg2Rad * (360f * st / Mathf.Max(1, systemSO.stars.Count));
                    Vector3 starLocalPos = new Vector3(
                        Mathf.Cos(starAngle) * starOrbitRadius,
                        0,
                        Mathf.Sin(starAngle) * starOrbitRadius
                    );
                    Vector3 starWorldPos = starClusterObj.transform.position + starLocalPos;
                    GameObject starObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    starObj.name = starSO.starName;
                    starObj.transform.parent = starClusterObj.transform;
                    starObj.transform.position = starWorldPos;
                    starObj.GetComponent<Renderer>().material.color = Color.yellow;
                    var starData = starObj.AddComponent<StarData>();
                    starData.data = starSO;
                    orbitingObjects.Add(new OrbitingObject(starObj, starClusterObj, starOrbitRadius, starOrbitSpeed));
                    starObjs.Add(starObj);
                }
                int planetIndex = 0;
                foreach (var planetSO in systemSO.planets)
                {
                    float planetOrbitRadius = planetOrbitRadiusBase + planetIndex * planetOrbitRadiusStep;
                    float planetOrbitSpeed = planetOrbitSpeedScale / planetOrbitRadius;
                    GameObject planetObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    planetObj.name = planetSO.planetName;
                    planetObj.transform.parent = starClusterObj.transform;
                    planetObj.transform.localScale *= 0.6f;
                    planetObj.transform.position = starClusterObj.transform.position + new Vector3(planetOrbitRadius, 0, 0);
                    planetObj.GetComponent<Renderer>().material.color = Color.blue;
                    var planetData = planetObj.AddComponent<PlanetData>();
                    planetData.data = planetSO;
                    orbitingObjects.Add(new OrbitingObject(planetObj, starClusterObj, planetOrbitRadius, planetOrbitSpeed));
                    int moonIndex = 0;
                    foreach (var moonSO in planetSO.moons)
                    {
                        float moonOrbitRadius = moonOrbitRadiusBase + moonIndex * moonOrbitRadiusStep;
                        float moonOrbitSpeed = moonOrbitSpeedScale / moonOrbitRadius;
                        GameObject moonObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        moonObj.name = moonSO.moonName;
                        moonObj.transform.parent = planetObj.transform;
                        moonObj.transform.localScale *= 0.3f;
                        moonObj.transform.localPosition = new Vector3(moonOrbitRadius, 0, 0);
                        moonObj.GetComponent<Renderer>().material.color = Color.gray;
                        var moonData = moonObj.AddComponent<MoonData>();
                        moonData.data = moonSO;
                        orbitingObjects.Add(new OrbitingObject(moonObj, planetObj, moonOrbitRadius, moonOrbitSpeed));
                        float stationOrbitRadiusLocal = stationOrbitRadius;
                        float stationOrbitSpeedLocal = stationOrbitSpeed;
                        foreach (var stationSO in moonSO.stations)
                        {
                            GameObject stationObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            stationObj.name = stationSO.stationName;
                            stationObj.transform.parent = moonObj.transform;
                            stationObj.transform.localScale *= 0.15f;
                            stationObj.transform.localPosition = new Vector3(stationOrbitRadiusLocal, 0, 0);
                            stationObj.GetComponent<Renderer>().material.color = Color.green;
                            var stationData = stationObj.AddComponent<StationData>();
                            stationData.data = stationSO;
                            orbitingObjects.Add(new OrbitingObject(stationObj, moonObj, stationOrbitRadiusLocal, stationOrbitSpeedLocal));
                        }
                        moonIndex++;
                    }
                    planetIndex++;
                }
                int beltIndex = 0;
                foreach (var beltSO in systemSO.asteroidBelts)
                {
                    float beltOrbitRadius = beltOrbitRadiusBase + beltIndex * beltOrbitRadiusStep;
                    float beltOrbitSpeed = beltOrbitSpeedScale / beltOrbitRadius;
                    GameObject beltCenterObj = new GameObject(beltSO.name + "_Center");
                    beltCenterObj.transform.parent = starClusterObj.transform;
                    beltCenterObj.transform.position = starClusterObj.transform.position + new Vector3(beltOrbitRadius, 0, 0);
                    var beltData = beltCenterObj.AddComponent<AsteroidBeltData>();
                    beltData.data = beltSO;
                    orbitingObjects.Add(new OrbitingObject(beltCenterObj, starClusterObj, beltOrbitRadius, beltOrbitSpeed));
                    int asteroidCount = beltSO.asteroids.Count;
                    float minRadius = 0.3f;
                    float maxRadius = 0.6f;
                    for (int asteroidIndex = 0; asteroidIndex < asteroidCount; asteroidIndex++)
                    {
                        var asteroidSO = beltSO.asteroids[asteroidIndex];
                        float angle = Mathf.Deg2Rad * (360f * asteroidIndex / asteroidCount);
                        float scatterRadius = Random.Range(minRadius, maxRadius);
                        GameObject asteroidObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        asteroidObj.name = asteroidSO.asteroidName;
                        asteroidObj.transform.parent = beltCenterObj.transform;
                        asteroidObj.transform.localScale *= 0.15f;
                        asteroidObj.transform.localPosition = new Vector3(
                            Mathf.Cos(angle) * scatterRadius,
                            Random.Range(-0.15f, 0.15f),
                            Mathf.Sin(angle) * scatterRadius
                        );
                        asteroidObj.GetComponent<Renderer>().material.color = Color.white;
                        var asteroidBehavior = asteroidObj.AddComponent<AsteroidBehavior>();
                        asteroidBehavior.asteroidSO = asteroidSO;
                    }
                    beltIndex++;
                }
                // Gates and Nebula - remain stationary relative to system
                float gateOffset = 10f;
                foreach (var gateSO in systemSO.gates)
                {
                    Vector3 gatePos = systemObj.transform.position + new Vector3(gateOffset, 0, -4);
                    GameObject gateObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    gateObj.transform.parent = systemObj.transform;
                    gateObj.transform.position = gatePos;
                    gateObj.name = gateSO.gateName;
                    gateObj.GetComponent<Renderer>().material.color = Color.magenta;
                }
                if (systemSO.nebula != null)
                {
                    GameObject nebulaObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    nebulaObj.transform.parent = systemObj.transform;
                    nebulaObj.transform.position = systemObj.transform.position;
                    float nebulaDiameter = Mathf.Max(systemSO.nebula.size.x, systemSO.nebula.size.y, systemSO.nebula.size.z) * 0.02f;
                    nebulaObj.transform.localScale = new Vector3(nebulaDiameter, nebulaDiameter, nebulaDiameter);
                    nebulaObj.name = systemSO.nebula.nebulaName ?? systemSO.systemName + "_Nebula";
                    Renderer renderer = nebulaObj.GetComponent<Renderer>();
                    renderer.material = new Material(Shader.Find("Standard")) { color = new Color(0.6f, 0.2f, 0.7f, 0.28f) };
                    renderer.material.SetFloat("_Mode", 3);
                    renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    renderer.material.SetInt("_ZWrite", 0);
                    renderer.material.DisableKeyword("_ALPHATEST_ON");
                    renderer.material.EnableKeyword("_ALPHABLEND_ON");
                    renderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    renderer.material.renderQueue = 3000;
                }
            }
        }
    }
    // Utility: Get mean position of a group of GameObjects
    private Vector3 MeanPosition(List<GameObject> objs)
    {
        if (objs == null || objs.Count == 0) return Vector3.zero;
        Vector3 sum = Vector3.zero;
        foreach (var obj in objs) sum += obj.transform.position;
        return sum / objs.Count;
    }
    public GameObject FindClosestMoonOrStar(Vector3 structurePos, List<GameObject> moons, GameObject star, float moonRadiusThreshold)
    {
        GameObject closestMoon = null;
        float closestDist = float.MaxValue;
        foreach (var moon in moons)
        {
            float dist = Vector3.Distance(structurePos, moon.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestMoon = moon;
            }
        }
        return (closestDist <= moonRadiusThreshold) ? closestMoon : star;
    }
    public void OrbitTemp(GameObject starObj, List<GameObject> moonGameObjects, SystemSO systemSO)
    {
        float structureOrbitSpeed = 0.3f;
        Vector3 structurePos = starObj.transform.position + new Vector3(Random.Range(6f, 14f), 0, Random.Range(-5f, 5f));
        Vector3 structurePos2;
        if (moonGameObjects.Count > 0)
        {
            GameObject firstMoon = moonGameObjects[0];
            float maxOffset = 2f;
            Vector2 randomCircle = Random.insideUnitCircle * maxOffset;
            structurePos2 = firstMoon.transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
        }
        else
        {
            structurePos2 = starObj.transform.position + new Vector3(Random.Range(6f, 14f), 0, Random.Range(-5f, 5f));
        }
        Debug.Log($"[Structure Build] Attempting to place structure in system {systemSO.systemName} at {structurePos}");
        Debug.Log($"[Structure Build] Attempting to place structure 2 near moon in system {systemSO.systemName} at {structurePos2}");
        GameObject structureObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        structureObj.name = "PlayerStructure_Example";
        structureObj.transform.position = structurePos;
        structureObj.transform.localScale *= 0.12f;
        structureObj.GetComponent<Renderer>().material.color = Color.cyan;
        GameObject structureObj2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        structureObj2.name = "PlayerStructure2_Example";
        structureObj2.transform.position = structurePos2;
        structureObj2.transform.localScale *= 0.12f;
        structureObj2.GetComponent<Renderer>().material.color = Color.gray;
        GameObject orbitCenter = FindClosestMoonOrStar(structurePos, moonGameObjects, starObj, 3f);
        GameObject orbitCenter2 = FindClosestMoonOrStar(structurePos2, moonGameObjects, starObj, 3f);
        float orbitRadius = Mathf.Max(0.28f, Vector3.Distance(structurePos, orbitCenter.transform.position));
        float orbitRadius2 = Mathf.Max(0.28f, Vector3.Distance(structurePos2, orbitCenter2.transform.position));
        Debug.Log($"[Structure Build] Structure will orbit {(orbitCenter != null ? orbitCenter.name : "NONE")}, at radius {orbitRadius:F2}");
        Debug.Log($"[Structure Build] Structure 2 will orbit {(orbitCenter2 != null ? orbitCenter2.name : "NONE")}, at radius {orbitRadius2:F2}");
        orbitingObjects.Add(new OrbitingObject(structureObj, orbitCenter, orbitRadius, structureOrbitSpeed));
        orbitingObjects.Add(new OrbitingObject(structureObj2, orbitCenter2, orbitRadius2, structureOrbitSpeed));
    }
}