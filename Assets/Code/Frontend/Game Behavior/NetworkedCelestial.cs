using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public enum CelestialType
{
    STAR,
    PLANET,
    MOON,
    ASTEROIDBELT,
    ASTEROID,
    STATION,
    GATE,
    NEBULA,
    SYSTEM,
    GALAXY
}

public class NetworkedCelestial : NetworkBehaviour
{
    [Header("Celestial Settings")]
    public CelestialType celestialType;

    [Header("LOD Settings")]
    public float viewDistance = 100f;
    public float nearDistance = 50f;

    private Renderer[] renderers;
    private Collider[] colliders;
    private bool isCurrentlyVisible = false;

    public override void OnNetworkSpawn()
    {
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();

        // Server: disable rendering
        if (IsServer)
        {
            SetRenderersEnabled(false);
        }

        // Client: disable all colliders immediately, then handle rendering LOD
        if (IsClient)
        {
            // Disable physics overhead
            foreach (var col in colliders)
            {
                if (col != null)
                    col.enabled = false;
            }

            // Start invisible until within viewDistance
            SetRenderersEnabled(false);
        }
    }

    public void Initialize(CelestialType type, float viewDist, float nearDist)
    {
        celestialType = type;
        viewDistance = viewDist;
        nearDistance = nearDist;
    }

    public void UpdateLOD(List<Vector3> playerPositions)
    {
        // Only clients do rendering LOD checks
        if (!IsClient) return;

        bool shouldBeVisible = false;
        float closestDistance = float.MaxValue;

        foreach (Vector3 playerPos in playerPositions)
        {
            float distance = Vector3.Distance(transform.position, playerPos);
            closestDistance = Mathf.Min(closestDistance, distance);

            if (distance <= viewDistance)
            {
                shouldBeVisible = true;
                // Don't break - we want to find the closest distance for detail level
            }
        }

        // Update visibility if changed
        if (shouldBeVisible != isCurrentlyVisible)
        {
            SetRenderersEnabled(shouldBeVisible);
            isCurrentlyVisible = shouldBeVisible;

            if (shouldBeVisible)
            {
                OnBecameVisible();
            }
            else
            {
                OnBecameInvisible();
            }
        }

        // Adjust detail level based on distance (optional)
        if (shouldBeVisible)
        {
            bool isNear = closestDistance <= nearDistance;
            SetDetailLevel(isNear);
        }
    }

    void SetRenderersEnabled(bool enabled)
    {
        foreach (var renderer in renderers)
        {
            if (renderer != null)
                renderer.enabled = enabled;
        }
    }

    void SetDetailLevel(bool isNear)
    {
        // Optional: Adjust quality based on distance
        // For example, reduce particle density, disable expensive shaders, etc.
        foreach (var renderer in renderers)
        {
            if (renderer != null)
            {
                // Example: Use simpler materials for distant objects
                // renderer.material = isNear ? highDetailMaterial : lowDetailMaterial;
            }
        }
    }

    void OnBecameVisible()
    {
        // Called when celestial becomes visible on THIS client
        // Enable particle systems, animations, etc.
        var particles = GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            if (ps != null && !ps.isPlaying)
                ps.Play();
        }

        // Enable animations
        var animators = GetComponentsInChildren<Animator>();
        foreach (var anim in animators)
        {
            if (anim != null)
                anim.enabled = true;
        }
    }

    void OnBecameInvisible()
    {
        // Called when celestial becomes invisible on THIS client
        // Disable expensive systems to save performance
        var particles = GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            if (ps != null && ps.isPlaying)
                ps.Stop();
        }

        // Disable animations
        var animators = GetComponentsInChildren<Animator>();
        foreach (var anim in animators)
        {
            if (anim != null)
                anim.enabled = false;
        }
    }

    public bool IsVisibleToThisClient()
    {
        return IsClient && isCurrentlyVisible;
    }
}