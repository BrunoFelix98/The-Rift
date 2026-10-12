using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Space look for the local view: procedural star/nebula skybox (Resources/Celestial/SpaceSkybox),
/// dim flat ambient, and making sure the local player's camera is the active main camera.
/// </summary>
public class SpaceEnvironmentManager : MonoBehaviour
{
    [Header("Skybox Settings")]
    [Tooltip("Used only if the skybox material can't be loaded.")]
    public Color backgroundColor = Color.black;

    [Header("Ambient Settings")]
    public AmbientMode ambientMode = AmbientMode.Flat;
    public Color ambientColor = new Color(0.008f, 0.008f, 0.01f, 1f);

    [Tooltip("Seconds between checks for the local player's camera.")]
    public float cameraCheckInterval = 0.5f;

    private Material skyboxMaterial;
    private float nextCameraCheck;

    void Start()
    {
        skyboxMaterial = CelestialVisuals.GetMaterial("SpaceSkybox");
        Apply();
    }

    void Update()
    {
        // Scanning every camera each frame was expensive; the player camera only changes on (re)spawn
        if (Time.time < nextCameraCheck) return;
        nextCameraCheck = Time.time + cameraCheckInterval;

        var allCameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cam in allCameras)
        {
            bool isPotentialMain = cam.CompareTag("MainCamera") ||
                                   cam.transform.root.CompareTag("Player") ||
                                   cam.name.Contains("Camera");
            if (!isPotentialMain) continue;

            // Only the local client's camera
            var netObj = cam.GetComponentInParent<NetworkObject>();
            if (netObj != null && !netObj.IsOwner) continue;

            if (!cam.CompareTag("MainCamera")) cam.tag = "MainCamera";
            if (!cam.gameObject.activeInHierarchy) cam.gameObject.SetActive(true);
            if (!cam.enabled) cam.enabled = true;

            ApplyToCamera(cam);
        }
    }

    [ContextMenu("Apply Settings")]
    public void Apply()
    {
        ApplyGlobalLighting();
        if (Camera.main != null) ApplyToCamera(Camera.main);
    }

    private void ApplyGlobalLighting()
    {
        RenderSettings.skybox = skyboxMaterial;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientLight = ambientColor;
    }

    private void ApplyToCamera(Camera cam)
    {
        CameraClearFlags wanted = skyboxMaterial != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        if (cam.clearFlags != wanted) cam.clearFlags = wanted;
        cam.backgroundColor = backgroundColor;
        if (RenderSettings.skybox != skyboxMaterial) ApplyGlobalLighting();
    }
}
