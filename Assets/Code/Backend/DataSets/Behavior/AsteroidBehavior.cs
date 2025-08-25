using UnityEngine;
using System;

public class AsteroidBehavior : MonoBehaviour
{
    public AsteroidSO asteroidSO;  // Use existing SO template
    public Asteroid asteroidEntity; // Mutable runtime data instance

    public event Action<AsteroidBehavior> OnAsteroidDepleted;

    void Start()
    {
        asteroidEntity = new Asteroid(asteroidSO.asteroidName);
        asteroidEntity.Resources.Clear();
        foreach (var resourceSO in asteroidSO.resources)
        {
            int initialQuantity = UnityEngine.Random.Range(resourceSO.minQuantity, resourceSO.maxQuantity + 1);
            Resource newResource = new Resource(resourceSO, initialQuantity);
            asteroidEntity.Resources.Add(newResource);
        }
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

        if (IsDepleted())
        {
            OnAsteroidDepleted?.Invoke(this);
            Destroy(gameObject);
        }
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
