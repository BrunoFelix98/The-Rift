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
        _starColor = Color.white;
        if (node.Params.EnvironmentType == CelestialEnvironment.LAVA)
        {
            _starColor = new Color(1f, 0.4f, 0.1f); 
        }
        else
        {
            _starColor = new Color(0.8f, 0.9f, 1f); 
        }

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

    void Update()
    {
        // Real-time updates for intensity or color could go here if needed
        // For now, we just ensure it stays active and correct
    }
}
