using UnityEngine;

public class ShipStatsAndBehaviors : MonoBehaviour
{
    public ShipSO shipData;          // Read-only ScriptableObject template
    public ShipRuntimeData runtimeData;

    private bool destroyed = false;

    private void Awake()
    {
        InitializeShip();
    }

    public void InitializeShip()
    {
        // Create a new runtime data copy from the SO template
        runtimeData = new ShipRuntimeData(shipData);
        destroyed = false;

        // Reset visuals, enable components, etc.
        gameObject.SetActive(true);
    }

    private void OnTriggerEnter(Collider other)
    {

        // Try to get the ProjectileDamage component from collided object
        ProjectileDamage projectileDamage = other.GetComponent<ProjectileDamage>();

        if (projectileDamage != null)
        {
            TakeDamage(projectileDamage.damageAmount, projectileDamage.damageType);
            // Optionally deactivate or destroy projectile
            other.gameObject.SetActive(false);
        }

        // Handle explosions or other damage types similarly if needed
    }

    public void TakeDamage(float damage, DamageType damageType)
    {
        if (destroyed) return;

        float shieldDamage = damage * (1f - runtimeData.shieldResistances.GetResistance(damageType));
        float armorDamage = damage * (1f - runtimeData.armorResistances.GetResistance(damageType));

        if (runtimeData.shieldHP > 0)
        {
            runtimeData.shieldHP -= shieldDamage;
            if (runtimeData.shieldHP < 0)
            {
                armorDamage += -runtimeData.shieldHP; // Overflow to armor
                runtimeData.shieldHP = 0;
            }
        }

        if (runtimeData.shieldHP <= 0 && runtimeData.armorHP > 0)
        {
            runtimeData.armorHP -= armorDamage;
            if (runtimeData.armorHP < 0)
            {
                runtimeData.armorHP = 0;
                OnDestroyed();
            }
        }
    }

    private void OnDestroyed()
    {
        destroyed = true;
        ShipPooling.Instance.ReturnToPool(this);
    }
}
