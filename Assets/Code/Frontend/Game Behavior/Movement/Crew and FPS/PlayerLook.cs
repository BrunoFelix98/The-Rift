using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : NetworkBehaviour
{
    [Header("Camera")]
    public Transform cam;
    public float mouseSensitivity = 2f;

    [Header("Input Actions")]
    public InputActionReference lookAction; // expects Vector2

    private float xRotation = 0f;

    // NetworkVariable to synchronize yaw across clients
    private NetworkVariable<float> yaw = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void OnEnable()
    {
        if (!Application.isFocused || !IsOwner) return;

        lookAction.action.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        if (!Application.isFocused || !IsOwner) return;

        lookAction.action.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (!Application.isFocused || !IsOwner) return;

        // Read input from the Input System
        Vector2 lookInput = lookAction.action.ReadValue<Vector2>();

        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        // Update vertical rotation (pitch)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Update horizontal rotation (yaw) locally
        transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y + mouseX, 0f);

        // Send yaw to the server for synchronization
        UpdateYawServerRpc(transform.rotation.eulerAngles.y);
    }

    private void FixedUpdate()
    {
        // Apply the synchronized yaw on all non-owning clients
        if (!Application.isFocused || !IsOwner)
        {
            transform.rotation = Quaternion.Euler(0f, yaw.Value, 0f);
        }
    }

    [ServerRpc]
    private void UpdateYawServerRpc(float newYaw)
    {
        yaw.Value = newYaw;

        // Update the server's rotation
        transform.rotation = Quaternion.Euler(0f, yaw.Value, 0f);
    }
}