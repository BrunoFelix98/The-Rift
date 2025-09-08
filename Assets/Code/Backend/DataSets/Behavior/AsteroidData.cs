using UnityEngine;
using System;

public class AsteroidData : MonoBehaviour
{
    public AsteroidSO data;  // Use existing SO template
    public Asteroid asteroidEntity; // Mutable runtime data instance

    public event Action<AsteroidData> OnAsteroidDepleted;

    private Vector3 originalScale;
    private int totalOriginalResources;

    void Start()
    {
        // Store original scale
        originalScale = transform.localScale;

        asteroidEntity = new Asteroid(data.asteroidName);
        asteroidEntity.Resources.Clear();

        foreach (var resourceSO in data.resources)
        {
            int initialQuantity = UnityEngine.Random.Range(resourceSO.minQuantity, resourceSO.maxQuantity + 1);
            Resource newResource = new Resource(resourceSO, initialQuantity);
            asteroidEntity.Resources.Add(newResource);
            totalOriginalResources += initialQuantity;
        }
    }

    public void Initialize(AsteroidSO so)
    {
        data = so;
    }

    public void TakeDamage(int damageAmount)
    {
        int remainingDamage = damageAmount;

        foreach (var resource in asteroidEntity.Resources)
        {
            if (resource.Quantity <= 0) continue;

            int amountTaken = Mathf.Min(resource.Quantity, remainingDamage);
            resource.Quantity -= amountTaken;
            remainingDamage -= amountTaken;

            Debug.Log($"Mined {amountTaken} from {resource.ResourceName}, remaining: {resource.Quantity}");

            if (remainingDamage <= 0) break;
        }

        // Update scale based on remaining resources
        UpdateScale();

        if (IsDepleted())
        {
            OnAsteroidDepleted?.Invoke(this);
            Destroy(gameObject);
        }
    }

    private void UpdateScale()
    {
        // Calculate remaining resources as a percentage
        int currentResources = 0;
        foreach (var res in asteroidEntity.Resources)
        {
            currentResources += res.Quantity;
        }

        float resourcePercentage = (float)currentResources / totalOriginalResources;

        // Scale between a minimum size (e.g., 20% of original) and original size
        float minScalePercentage = 0.2f;
        float scaleMultiplier = Mathf.Lerp(minScalePercentage, 1f, resourcePercentage);

        // Apply the new scale
        transform.localScale = originalScale * scaleMultiplier;

        // Optional: Add visual feedback for heavily mined asteroids
        if (resourcePercentage < 0.3f)
        {
            // Could add particle effects, change material, etc.
            AddMiningEffects();
        }
    }

    private void AddMiningEffects()
    {
        // Add particle effects or visual changes for heavily mined asteroids
        var particleSystem = GetComponent<ParticleSystem>();
        if (particleSystem == null)
        {
            particleSystem = gameObject.AddComponent<ParticleSystem>();
            var main = particleSystem.main;
            main.startColor = Color.gray;
            main.startLifetime = 1f;
            main.startSpeed = 0.5f;
            main.maxParticles = 20;
            main.startSize = 0.1f;
        }

        // Optional: Change material to show damage
        var renderer = GetComponent<Renderer>();
        if (renderer != null && renderer.material.HasProperty("_Color"))
        {
            // Darken the asteroid as it gets more mined
            float resourcePercentage = GetResourcePercentage();
            Color originalColor = Color.gray;
            Color minedColor = Color.gray * 0.5f;
            renderer.material.color = Color.Lerp(minedColor, originalColor, resourcePercentage);
        }
    }

    private float GetResourcePercentage()
    {
        if (totalOriginalResources == 0) return 0f;

        int currentResources = 0;
        foreach (var res in asteroidEntity.Resources)
        {
            currentResources += res.Quantity;
        }

        return (float)currentResources / totalOriginalResources;
    }

    public string GetResourceStatus()
    {
        float percentage = GetResourcePercentage();
        return $"Resources: {Mathf.RoundToInt(percentage * 100)}% remaining";
    }

    private bool IsDepleted()
    {
        foreach (var res in asteroidEntity.Resources)
        {
            if (res.Quantity > 0)
                return false;
        }
        return true;
    }
}
