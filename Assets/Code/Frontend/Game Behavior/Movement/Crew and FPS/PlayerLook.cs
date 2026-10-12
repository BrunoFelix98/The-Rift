using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Local mouse look for the FPS crew. Pitch only moves the owner's camera; yaw is collected here and
/// consumed by PlayerMovement's simulation tick, so it is predicted and reconciled like movement.
/// </summary>
public class PlayerLook : NetworkBehaviour
{
    [Header("Camera")]
    public Transform cam;
    public float mouseSensitivity = 2f;

    [Header("Input Actions")]
    public InputActionReference lookAction; // expects Vector2

    private float xRotation = 0f;

    // Yaw (degrees) gathered since the last simulation tick
    public float PendingYawDelta { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (IsOwner) lookAction.action.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner) lookAction.action.Disable();
    }

    public float ConsumeYawDelta()
    {
        float delta = PendingYawDelta;
        PendingYawDelta = 0f;
        return delta;
    }

    private void Update()
    {
        if (!Application.isFocused || !IsOwner) return;

        Vector2 lookInput = lookAction.action.ReadValue<Vector2>();

        xRotation -= lookInput.y * mouseSensitivity;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        PendingYawDelta += lookInput.x * mouseSensitivity;
    }
}
