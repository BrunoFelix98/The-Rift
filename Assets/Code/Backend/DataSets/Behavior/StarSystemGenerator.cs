using UnityEngine;

public class StarSystemGenerator : MonoBehaviour
{
    [Header("Star System Settings")]
    public int numberOfPlanets = 3;
    public int moonsPerPlanet = 2;
    public int asteroidCount = 50;
    public float starSize = 2f;
    public float planetSize = 1f;
    public float moonSize = 0.5f;
    public float stationSize = 0.8f;
    public float asteroidSize = 0.3f;

    public float planetOrbitRadius = 10f;
    public float moonOrbitRadius = 2f;
    public float asteroidBeltInnerRadius = 15f;
    public float asteroidBeltOuterRadius = 20f;

    void Start()
    {
        GenerateStarSystem();
    }

    void GenerateStarSystem()
    {
        // Create the star
        GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        star.transform.position = Vector3.zero;
        star.transform.localScale = Vector3.one * starSize;
        star.name = "Star";

        // Create planets
        for (int i = 0; i < numberOfPlanets; i++)
        {
            float angle = i * Mathf.PI * 2 / numberOfPlanets;
            Vector3 planetPosition = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * planetOrbitRadius;

            GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.transform.position = planetPosition;
            planet.transform.localScale = Vector3.one * planetSize;
            planet.name = $"Planet_{i + 1}";

            // Create moons for the planet
            for (int j = 0; j < moonsPerPlanet; j++)
            {
                float moonAngle = j * Mathf.PI * 2 / moonsPerPlanet;
                Vector3 moonPosition = planetPosition + new Vector3(Mathf.Cos(moonAngle), 0, Mathf.Sin(moonAngle)) * moonOrbitRadius;

                GameObject moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                moon.transform.position = moonPosition;
                moon.transform.localScale = Vector3.one * moonSize;
                moon.name = $"Planet_{i + 1}_Moon_{j + 1}";
            }

            // Create a station near the planet
            Vector3 stationPosition = planetPosition + Vector3.right * 2f;
            GameObject station = GameObject.CreatePrimitive(PrimitiveType.Cube);
            station.transform.position = stationPosition;
            station.transform.localScale = Vector3.one * stationSize;
            station.name = $"Planet_{i + 1}_Station";
        }

        // Create asteroid belt
        for (int k = 0; k < asteroidCount; k++)
        {
            float angle = Random.Range(0, Mathf.PI * 2);
            float radius = Random.Range(asteroidBeltInnerRadius, asteroidBeltOuterRadius);
            Vector3 asteroidPosition = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;

            GameObject asteroid = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            asteroid.transform.position = asteroidPosition;
            asteroid.transform.localScale = Vector3.one * asteroidSize;
            asteroid.name = $"Asteroid_{k + 1}";
        }
    }
}