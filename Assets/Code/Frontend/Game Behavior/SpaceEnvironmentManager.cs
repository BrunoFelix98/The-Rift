using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

public class SpaceEnvironmentManager : MonoBehaviour
{
    [Header("Skybox Settings")]
    public Color backgroundColor = Color.black;
    public bool disableSkybox = true;

    [Header("Ambient Settings")]
    public AmbientMode ambientMode = AmbientMode.Flat;
    public Color ambientColor = new Color(0.05f, 0.05f, 0.05f, 1f);

    void Start()
    {
        Apply();
    }

    void Update()
    {
        // 1. Find all cameras including inactive ones
        var allCameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        foreach (var cam in allCameras)
        {
            // 2. Identify potential player cameras
            bool isPotentialMain = cam.CompareTag("MainCamera") || 
                                   (cam.transform.root.CompareTag("Player")) || 
                                   cam.name.Contains("Camera");

            if (isPotentialMain)
            {
                // Only enable and configure if it belongs to us (the local client)
                var netObj = cam.GetComponentInParent<NetworkObject>();
                if (netObj != null && !netObj.IsOwner) continue;

                bool wasActivated = false;

                // Force Tag if missing - critical for Unity's Camera.main
                if (!cam.CompareTag("MainCamera"))
                {
                    cam.tag = "MainCamera";
                    Debug.Log($"[SpaceEnvironmentManager] Tagged camera as MainCamera: {cam.gameObject.name}");
                }

                // Force Activation of GameObject
                if (!cam.gameObject.activeInHierarchy)
                {
                    cam.gameObject.SetActive(true);
                    wasActivated = true;
                }

                // Force Enable of Component
                if (!cam.enabled)
                {
                    cam.enabled = true;
                    wasActivated = true;
                }

                // Apply background settings
                if (wasActivated || cam.clearFlags != CameraClearFlags.SolidColor)
                {
                    ApplyToCamera(cam);
                    ApplyGlobalLighting();
                }
            }
        }
    }

    [ContextMenu("Apply Settings")]
    public void Apply()
    {
        ApplyGlobalLighting();
        
        if (Camera.main != null)
        {
            ApplyToCamera(Camera.main);
        }

        Debug.Log("[SpaceEnvironmentManager] Applied global space lighting.");
    }

    private void ApplyGlobalLighting()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientLight = ambientColor;
    }

    private void ApplyToCamera(Camera cam)
    {
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
        Debug.Log($"[SpaceEnvironmentManager] Applied background settings to camera: {cam.name}");
    }
}
