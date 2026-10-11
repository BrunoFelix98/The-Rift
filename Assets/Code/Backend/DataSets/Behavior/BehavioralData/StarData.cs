using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class StarData : MonoBehaviour
{
    public StarSO data;
    private Light starLight;
    private MeshRenderer starRenderer;
    private CelestialHierarchyNode _node;
    private float _baseIntensity = 5000000f;

    public void Initialize(StarSO template, CelestialHierarchyNode node)
    {
        data = template; 
        _node = node;
        
        if (starLight == null) starLight = GetComponent<Light>();
        if (starLight == null) starLight = gameObject.AddComponent<Light>();

        starRenderer = GetComponent<MeshRenderer>();
        if (starRenderer != null)
        {
            starRenderer.shadowCastingMode = ShadowCastingMode.Off;
            starRenderer.receiveShadows = false;
        }

        // Star Color calculation
        ApplyStarVisuals(node.Params);

        starLight.type = LightType.Point;
        starLight.color = _starColor;
        starLight.intensity = _baseIntensity;
        starLight.range = 300000f; 
        starLight.shadows = LightShadows.Soft; // Enable shadows now that global light is gone
#if UNITY_EDITOR
        starLight.lightmapBakeType = LightmapBakeType.Realtime;
#endif
        starLight.renderMode = LightRenderMode.ForcePixel;
        starLight.enabled = true;
    }

    private Color _starColor;

    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int SpeedId = Shader.PropertyToID("_Speed");
    private static readonly int SpotAmountId = Shader.PropertyToID("_SpotAmount");
    private static readonly int CoronaColorId = Shader.PropertyToID("_CoronaColor");
    private static readonly int SizeId = Shader.PropertyToID("_Size");

    private MaterialPropertyBlock _surfaceBlock;
    private MaterialPropertyBlock _coronaBlock;

    // Every star in the map data carries the same environment, so the class comes from the
    // template name and the body's seed instead
    private void ApplyStarVisuals(OrbitParams p)
    {
        var rng = new CelestialVisuals.Rng(p.NetworkId, p.ResourceSeed);
        string template = p.TemplateName.ToString();

        Color core, edge, corona;
        float spots = rng.Range(0.3f, 0.7f);
        float speed = rng.Range(0.3f, 0.5f);
        float coronaSize = rng.Range(3.2f, 4.2f);

        if (p.EnvironmentType == CelestialEnvironment.LAVA)
        {
            core = new Color(1f, 0.62f, 0.30f); edge = new Color(0.9f, 0.28f, 0.05f); corona = new Color(1f, 0.42f, 0.15f);
        }
        else if (template.Contains("Red Giant"))
        {
            core = new Color(1f, 0.62f, 0.38f); edge = new Color(0.85f, 0.25f, 0.08f); corona = new Color(1f, 0.45f, 0.2f);
            spots = rng.Range(0.5f, 0.85f); speed = 0.22f; coronaSize = rng.Range(4f, 5f);
        }
        else if (template.Contains("Dwarf"))
        {
            if (rng.Chance(0.5f))
            {
                core = new Color(1f, 0.52f, 0.32f); edge = new Color(0.75f, 0.18f, 0.06f); corona = new Color(1f, 0.35f, 0.15f);
            }
            else
            {
                core = new Color(0.9f, 0.95f, 1f); edge = new Color(0.6f, 0.7f, 1f); corona = new Color(0.7f, 0.8f, 1f);
                spots = 0.1f; coronaSize = rng.Range(2.6f, 3.2f);
            }
        }
        else
        {
            float spectral = rng.Next();
            if (spectral < 0.2f) { core = new Color(0.75f, 0.85f, 1f); edge = new Color(0.45f, 0.6f, 1f); corona = new Color(0.55f, 0.7f, 1f); spots = 0.15f; }
            else if (spectral < 0.55f) { core = new Color(1f, 0.97f, 0.88f); edge = new Color(1f, 0.72f, 0.4f); corona = new Color(1f, 0.8f, 0.5f); }
            else if (spectral < 0.85f) { core = new Color(1f, 0.8f, 0.55f); edge = new Color(1f, 0.5f, 0.2f); corona = new Color(1f, 0.6f, 0.3f); }
            else { core = new Color(1f, 0.55f, 0.35f); edge = new Color(0.8f, 0.22f, 0.07f); corona = new Color(1f, 0.38f, 0.16f); }
        }

        _starColor = core;

        if (starRenderer != null)
        {
            _surfaceBlock ??= new MaterialPropertyBlock();
            starRenderer.sharedMaterial = CelestialVisuals.GetMaterial("CelestialStar");
            _surfaceBlock.Clear();
            _surfaceBlock.SetFloat(SeedId, rng.Next());
            _surfaceBlock.SetColor(CoreColorId, core);
            _surfaceBlock.SetColor(EdgeColorId, edge);
            _surfaceBlock.SetFloat(SpeedId, speed);
            _surfaceBlock.SetFloat(SpotAmountId, spots);
            starRenderer.SetPropertyBlock(_surfaceBlock);
        }

        _coronaBlock ??= new MaterialPropertyBlock();
        var coronaRenderer = CelestialVisuals.EnsureChild(transform, "Corona", CelestialVisuals.QuadMesh, CelestialVisuals.GetMaterial("CelestialCorona"));
        coronaRenderer.transform.localPosition = Vector3.zero;
        coronaRenderer.transform.localRotation = Quaternion.identity;
        coronaRenderer.transform.localScale = Vector3.one;
        _coronaBlock.Clear();
        _coronaBlock.SetFloat(SeedId, rng.Next());
        _coronaBlock.SetColor(CoronaColorId, corona);
        _coronaBlock.SetFloat(SizeId, coronaSize);
        coronaRenderer.SetPropertyBlock(_coronaBlock);
        coronaRenderer.enabled = true;

        PublishStarGlobals();
    }

    // Planet, atmosphere and ring shaders light themselves toward this position
    private void PublishStarGlobals()
    {
        Shader.SetGlobalVector(CelestialVisuals.StarPosId, transform.position);
        Shader.SetGlobalVector(CelestialVisuals.StarColorId, _starColor);
    }

    void Update()
    {
        PublishStarGlobals();
    }
}
