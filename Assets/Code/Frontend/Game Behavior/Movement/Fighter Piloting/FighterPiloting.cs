using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class FighterPiloting : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 0f;
    public float maxForwardSpeed = 20f;
    public float maxBackwardSpeed = -10f;
    public float acceleration = 5f;
    public float deceleration = 5f;
    public float strafeSpeed = 5f;
    public float pitchSpeed = 250f;
    public float yawSpeed = 250f;
    public float rollSpeed = 250f;

    public float mouseSensitivity = 0.2f;

    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference verticalStrafe;
    public InputActionReference rollAction;
    public InputActionReference lookAction;
    public InputActionReference accelerateAction;
    public InputActionReference brakeAction;

    private Rigidbody rb;
    private float currentSpeed;

    private NetworkVariable<Vector3> netPosition = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<Quaternion> netRotation = new NetworkVariable<Quaternion>(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<Vector3> netVelocity = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private const float MAX_ROTATION_PER_TICK = 360f;

    private Quaternion accumulatedRotation = Quaternion.identity;
    private float tickTimer = 0f;
    private const float serverTickRate = 1f / 50f; // 50 Hz

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        currentSpeed = moveSpeed;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            moveAction.action.Enable();
            verticalStrafe.action.Enable();
            rollAction.action.Enable();
            lookAction.action.Enable();
            accelerateAction.action.Enable();
            brakeAction.action.Enable();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            moveAction.action.Disable();
            verticalStrafe.action.Disable();
            rollAction.action.Disable();
            lookAction.action.Disable();
            accelerateAction.action.Disable();
            brakeAction.action.Disable();
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Get inputs
        Vector2 look = lookAction.action.ReadValue<Vector2>();
        float roll = rollAction.action.ReadValue<float>();

        // Build incremental quaternion from this frame
        float pitchDelta = -look.y * pitchSpeed * mouseSensitivity * Time.deltaTime;
        float yawDelta = look.x * yawSpeed * mouseSensitivity * Time.deltaTime;
        float rollDelta = roll * rollSpeed * Time.deltaTime;

        Quaternion frameRotation =
            Quaternion.Euler(pitchDelta, yawDelta, rollDelta);

        // Accumulate orientation locally
        accumulatedRotation *= frameRotation;
        transform.rotation = accumulatedRotation;

        // Send orientation to server at fixed tick rate
        tickTimer += Time.deltaTime;
        if (tickTimer >= serverTickRate)
        {
            tickTimer -= serverTickRate;
            SendTransformToServerRpc(transform.position, accumulatedRotation, rb.linearVelocity);
        }
    }

    private void FixedUpdate()
    {
        if (IsOwner)
        {
            HandleMovement();
        }
        else
        {
            // Remote clients follow server state (interpolated for smoothness)
            float lerpSpeed = 10f;
            transform.position = Vector3.Lerp(transform.position, netPosition.Value, lerpSpeed * Time.fixedDeltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, netRotation.Value, lerpSpeed * Time.fixedDeltaTime);
            rb.linearVelocity = netVelocity.Value;
        }
    }

    private void HandleMovement()
    {
        if (accelerateAction.action.IsPressed())
            currentSpeed = Mathf.Min(currentSpeed + acceleration * Time.fixedDeltaTime, maxForwardSpeed);
        else if (brakeAction.action.IsPressed())
            currentSpeed = Mathf.Max(currentSpeed - deceleration * Time.fixedDeltaTime, maxBackwardSpeed);
        else
            currentSpeed = Mathf.MoveTowards(currentSpeed, moveSpeed, deceleration * Time.fixedDeltaTime);

        Vector3 moveDir =
            transform.forward * currentSpeed +
            transform.right * moveAction.action.ReadValue<float>() * strafeSpeed +
            transform.up * verticalStrafe.action.ReadValue<float>() * strafeSpeed;

        rb.linearVelocity = moveDir;
    }

    [ServerRpc(RequireOwnership = true)]
    private void SendTransformToServerRpc(Vector3 pos, Quaternion rot, Vector3 velocity)
    {
        // Server clamps extreme deltas to prevent abuse
        Quaternion lastRot = netRotation.Value;
        float angle;
        Vector3 axis;
        Quaternion deltaRot = rot * Quaternion.Inverse(lastRot);
        deltaRot.ToAngleAxis(out angle, out axis);

        if (angle > MAX_ROTATION_PER_TICK)
        {
            angle = MAX_ROTATION_PER_TICK;
            rot = Quaternion.AngleAxis(angle, axis) * lastRot;
        }

        // Update authoritative state
        netPosition.Value = pos;
        netRotation.Value = rot;
        netVelocity.Value = velocity;

        // Server also moves its own rigidbody
        transform.position = pos;
        transform.rotation = rot;
        rb.linearVelocity = velocity;
    }
}