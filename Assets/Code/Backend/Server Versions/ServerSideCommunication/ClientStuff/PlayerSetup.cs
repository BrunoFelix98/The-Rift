using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class PlayerSetup : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Disable cameras on server
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

        // For the local owner, enable cameras
        var ownerCameras = GetComponentsInChildren<Camera>();
        foreach (var cam in ownerCameras)
        {
            cam.enabled = true;
        }

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