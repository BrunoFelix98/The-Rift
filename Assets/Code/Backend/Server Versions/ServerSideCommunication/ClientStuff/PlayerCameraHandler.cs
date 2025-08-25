using Unity.Netcode;
using UnityEngine;

public class PlayerCameraHandler : NetworkBehaviour
{
    public Camera playerCamera;

    void OnEnable()
    {
        if (IsOwner)
        {
            playerCamera.enabled = true;
            playerCamera.tag = "MainCamera";
        }
        else
        {
            playerCamera.enabled = false;
            if (playerCamera.tag == "MainCamera") playerCamera.tag = "Untagged";
        }
    }

    void OnDisable()
    {
        // Always disable camera on disable to avoid stray active cameras.
        if (playerCamera != null) playerCamera.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // Enable camera for local player
            playerCamera.enabled = true;
            playerCamera.gameObject.tag = "MainCamera";

            // Lock cursor, etc.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // Disable camera for other clients and server
            playerCamera.enabled = false;
            // Optionally reset tag to avoid conflicts
            if (playerCamera.gameObject.tag == "MainCamera")
                playerCamera.gameObject.tag = "Untagged";
        }
    }
}