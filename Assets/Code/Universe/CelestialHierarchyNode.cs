using System;
using System.Collections.Generic;
using UnityEngine;

public class CelestialHierarchyNode
{
    public CelestialPersistenceData Data;
    public OrbitParams Params => Data.orbit;
    
    public CelestialHierarchyNode Parent;
    public List<CelestialHierarchyNode> Children = new List<CelestialHierarchyNode>();
    
    public Vector3 CurrentWorldPosition;
    public GameObject ActiveInstance;
    private bool lastIsVisible = true;
    public float visualRadius;

    public CelestialHierarchyNode(CelestialPersistenceData persistenceData, CelestialHierarchyNode parent = null)
    {
        Data = persistenceData;
        Parent = parent;
        InitializeVisualRadius();
    }

    private void InitializeVisualRadius()
    {
        // Use NetworkId as seed for consistent randomness across clients
        UnityEngine.Random.State oldState = UnityEngine.Random.state;
        UnityEngine.Random.InitState((int)Params.NetworkId);

        switch (Params.Type)
        {
            case CelestialType.STAR: visualRadius = UnityEngine.Random.Range(5000f, 10000f); break;
            case CelestialType.PLANET: visualRadius = UnityEngine.Random.Range(2000f, 2500f); break;
            case CelestialType.MOON: visualRadius = UnityEngine.Random.Range(1000f, 1500f); break;
            case CelestialType.ASTEROID: visualRadius = UnityEngine.Random.Range(10f, 150f); break;
            case CelestialType.NEBULA: visualRadius = 200000f; break;
            case CelestialType.STATION: visualRadius = 200f; break;
            case CelestialType.GATE: visualRadius = 300f; break;
            default: visualRadius = 100f; break;
        }

        UnityEngine.Random.state = oldState;
    }

    private UniverseVisualizationSettings GetSettings()
    {
        if (SystemCenteredVisualizer.Instance != null) return SystemCenteredVisualizer.Instance.settings;
        return new UniverseVisualizationSettings(); 
    }

    public void UpdatePositionOnly(float time)
    {
        Vector3 parentWorldPos = Parent != null ? Parent.CurrentWorldPosition : Params.CenterPos;
        CurrentWorldPosition = parentWorldPos + ComputeOrbitOffset(Params, time, GetSettings());
    }

    // Also used by the server, which has no nodes, to place spawns and validate gate use
    public static Vector3 ComputeOrbitOffset(OrbitParams p, float time, UniverseVisualizationSettings settings)
    {
        float t = time * p.AngularSpeed * settings.orbitSpeedMultiplier + p.PhaseOffset;
        float radius = p.Radius;
        
        if (p.Type != CelestialType.GALAXY && p.Type != CelestialType.STAR && p.Type != CelestialType.SYSTEM)
        {
            radius *= settings.systemRadiusMultiplier;
        }
        else
        {
            radius *= settings.orbitRadiusMultiplier;
        }

        Vector3 localOffset = new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t)) * radius;
        if (p.TiltAngle != 0)
        {
            localOffset = Quaternion.AngleAxis(p.TiltAngle, p.TiltAxis) * localOffset;
        }
        return localOffset;
    }

    public void UpdateRecursive(float time, Vector3 playerPos, float viewDistance, float starViewDistance, CelestialPool pool, Plane[] frustumPlanes, bool onlyLoadStars, int frameCounter)
    {
        var settings = GetSettings();
        
        // Throttling: Only update position every 2 frames for non-stars
        bool isStarOrNebula = Params.Type == CelestialType.STAR || Params.Type == CelestialType.NEBULA;
        float effectiveViewDist = isStarOrNebula ? starViewDistance : viewDistance;

        if (isStarOrNebula || (frameCounter + (int)Params.NetworkId) % 2 == 0)
        {
            UpdatePositionOnly(time);
        }

        // Culling Throttling: Check distance and frustum every 10 frames
        bool shouldUpdateVisibility = (frameCounter + (int)Params.NetworkId) % 10 == 0;
        
        bool isVisible = lastIsVisible;
        float distToPlayer = Vector3.Distance(CurrentWorldPosition, playerPos);

        if (shouldUpdateVisibility)
        {
            isVisible = true;

            // Frustum Culling Logic
            if (frustumPlanes != null && !isStarOrNebula) 
            {
                float baseScale = GetBaseScale(Params.Type).x;
                float checkRadius = baseScale * settings.modelScaleMultiplier;
                if (Params.Type == CelestialType.PLANET) checkRadius *= 1.5f;
                
                Bounds bounds = new Bounds(CurrentWorldPosition, Vector3.one * (checkRadius * 2f));
                isVisible = GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
                
                if (distToPlayer < checkRadius * 5f) isVisible = true;
            }

            bool typeAllowed = true;
            if (onlyLoadStars && !isStarOrNebula && Params.Type != CelestialType.SYSTEM) typeAllowed = false;

            // Effective distance includes the object's own size to prevent "popping" for massive planets
            float activationDist = effectiveViewDist + (visualRadius * settings.modelScaleMultiplier);
            bool shouldBeActive = (distToPlayer <= activationDist) && isVisible && typeAllowed;

            if (shouldBeActive && ActiveInstance == null && !string.IsNullOrEmpty(Params.TemplateName.ToString()))
            {
                ActiveInstance = pool.Get(Params.Type);
                if (ActiveInstance != null)
                {
                    ActiveInstance.name = $"{Params.Type}_{Params.Name}";
                    InitializeAssetData(ActiveInstance);
                }
            }
            else if (!shouldBeActive && ActiveInstance != null)
            {
                pool.ReturnToPool(Params.Type, ActiveInstance);
                ActiveInstance = null;
            }
            
            lastIsVisible = isVisible;
        }

        if (ActiveInstance != null)
        {
            ActiveInstance.transform.position = CurrentWorldPosition;
            ActiveInstance.transform.rotation = Quaternion.Euler(0, (time * Params.AngularSpeed * settings.orbitSpeedMultiplier + Params.PhaseOffset) * Mathf.Rad2Deg, 0);
            ActiveInstance.transform.localScale = GetBaseScale(Params.Type) * settings.modelScaleMultiplier;
        }

        // Optimization: stop recursion if parent is too far
        bool skipChildren = false;

        if (Params.Type == CelestialType.STAR && distToPlayer > effectiveViewDist + 10000f) skipChildren = true;
        if (Params.Type == CelestialType.GALAXY || Params.Type == CelestialType.SYSTEM) skipChildren = false; 

        if (!skipChildren)
        {
            foreach (var child in Children) child.UpdateRecursive(time, playerPos, viewDistance, starViewDistance, pool, frustumPlanes, onlyLoadStars, frameCounter);
        }
        else
        {
            DeactivateAllChildren(pool);
        }
    }

    private Vector3 GetBaseScale(CelestialType type)
    {
        return Vector3.one * visualRadius;
    }

    public void DeactivateAllChildren(CelestialPool pool)
    {
        foreach (var child in Children)
        {
            if (child.ActiveInstance != null)
            {
                pool.ReturnToPool(child.Params.Type, child.ActiveInstance);
                child.ActiveInstance = null;
            }
            child.DeactivateAllChildren(pool);
        }
    }

    private void InitializeAssetData(GameObject instance)
    {
        if (CelestialAssetRegistry.Instance == null) return;
        string templateName = Params.TemplateName.ToString();
        ScriptableObject template = CelestialAssetRegistry.Instance.GetTemplate(templateName);

        switch (Params.Type)
        {
            case CelestialType.STAR:
                if (template is StarSO sso) instance.GetComponent<StarData>()?.Initialize(sso, this);
                break;
            case CelestialType.PLANET:
                if (template is PlanetSO pso) instance.GetComponent<PlanetData>()?.Initialize(pso, this);
                break;
            case CelestialType.MOON:
                if (template is MoonSO mso) instance.GetComponent<MoonData>()?.Initialize(mso, this);
                break;
            case CelestialType.STATION:
                if (template is StationSO sso2) instance.GetComponent<StationData>()?.Initialize(sso2, this);
                break;
            case CelestialType.ASTEROID:
                if (template is AsteroidSO aso) instance.GetComponent<AsteroidData>()?.Initialize(aso, this);
                break;
            case CelestialType.GATE:
                if (template is GateSO gso) instance.GetComponent<GateData>()?.Initialize(gso, this);
                break;
            case CelestialType.NEBULA:
                if (template is NebulaSO nso) instance.GetComponent<NebulaData>()?.Initialize(nso, this);
                break;
        }
    }
}
