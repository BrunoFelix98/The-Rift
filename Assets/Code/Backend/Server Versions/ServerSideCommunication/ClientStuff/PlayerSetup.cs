using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class PlayerSetup : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsServer && !IsClient)
        {
            // Disable cameras on a dedicated server; a host renders its own player below
            var cameras = GetComponentsInChildren<Camera>();
            foreach (var cam in cameras)
            {
                cam.enabled = false;
            }
            return;
        }

        if (!IsOwner)
        {
            // Disable cameras for non-owners
            var cameras = GetComponentsInChildren<Camera>();
            foreach (var cam in cameras)
            {
                cam.enabled = false;
            }
            return;
        }

        // Fixed-size markers for other players too small or off-screen to see
        PlayerMarkerOverlay.EnsureExists();

        // For the local owner, enable cameras
        var ownerCameras = GetComponentsInChildren<Camera>(true);
        foreach (var cam in ownerCameras)
        {
            cam.enabled = true;
            // Ensure we can see the massive systems (1 million units far clip)
            cam.farClipPlane = 1000000f;

            if (cam.gameObject.name.Contains("Main") || ownerCameras.Length == 1)
            {
                cam.tag = "MainCamera";
            }
        }

        // Enable Cinemachine Brain if present on the player or children
        var brains = GetComponentsInChildren<Unity.Cinemachine.CinemachineBrain>(true);
        foreach (var brain in brains) brain.enabled = true;

        // Set cursor based on ship type
        SetCursorForShipType();
    }

    private void SetCursorForShipType()
    {
        // Check what type of ship this is based on components
        if (GetComponent<BigShipMovement>() != null)
        {
            // BigShip needs cursor visible for clicking
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // FPS and Fighter need cursor locked
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}