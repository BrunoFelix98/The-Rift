using UnityEngine;

public class WarpEffectManager : MonoBehaviour
{
    public static WarpEffectManager Instance { get; private set; }

    [Header("Warp Visuals")]
    public ParticleSystem starTunnelParticles;
    public GameObject warpOverlayUI;
    
    [Header("Settings")]
    public float transitionDuration = 3.0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        if (starTunnelParticles != null) starTunnelParticles.Stop();
        if (warpOverlayUI != null) warpOverlayUI.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PlayWarpEffect()
    {
        if (starTunnelParticles != null)
        {
            starTunnelParticles.Play();
            var main = starTunnelParticles.main;
            main.simulationSpeed = 5.0f; // Fast stars
        }
        
        if (warpOverlayUI != null) warpOverlayUI.SetActive(true);
    }

    public void StopWarpEffect()
    {
        if (starTunnelParticles != null)
        {
            starTunnelParticles.Stop();
        }
        
        if (warpOverlayUI != null) warpOverlayUI.SetActive(false);
    }

    private void Update()
    {
        // Keep particles centered on the camera
        if (starTunnelParticles != null && Camera.main != null)
        {
            starTunnelParticles.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 10f;
            starTunnelParticles.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
