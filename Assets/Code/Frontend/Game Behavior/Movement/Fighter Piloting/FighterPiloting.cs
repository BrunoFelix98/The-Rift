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

    private struct InputData : INetworkSerializable
    {
        public float strafe;
        public float strafeV;
        public float roll;
        public Vector2 look;
        public bool accelerate;
        public bool brake;
        public int tick;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref strafe);
            serializer.SerializeValue(ref strafeV);
            serializer.SerializeValue(ref roll);
            serializer.SerializeValue(ref look);
            serializer.SerializeValue(ref accelerate);
            serializer.SerializeValue(ref brake);
            serializer.SerializeValue(ref tick);
        }
    }

    private List<InputData> inputBuffer = new List<InputData>();
    private int lastProcessedTick = -1;
    private int localTick = 0;

    private NetworkVariable<Vector3> netPosition = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<Quaternion> netRotation = new NetworkVariable<Quaternion>(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<Vector3> netVelocity = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Vector2 lookInput;
    private float rollInput;

    private Vector3 lastValidPosition;
    private Quaternion lastValidRotation;

    private const float MAX_ROTATION_PER_TICK = 360f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        currentSpeed = moveSpeed;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        lastValidPosition = transform.position;
        lastValidRotation = transform.rotation;
    }

    private void OnEnable()
    {
        if (IsOwner)
        {
            moveAction.action.Enable();
            verticalStrafe.action.Enable();
            rollAction.action.Enable();
            lookAction.action.Enable();
            accelerateAction.action.Enable();
            brakeAction.action.Enable();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable()
    {
        if (IsOwner)
        {
            moveAction.action.Disable();
            verticalStrafe.action.Disable();
            rollAction.action.Disable();
            lookAction.action.Disable();
            accelerateAction.action.Disable();
            brakeAction.action.Disable();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Read inputs every frame for responsiveness
        lookInput = lookAction.action.ReadValue<Vector2>();
        rollInput = rollAction.action.ReadValue<float>();
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
        {
            // Non-owners interpolate/network sync
            transform.position = netPosition.Value;
            transform.rotation = netRotation.Value;
            rb.linearVelocity = netVelocity.Value;
            return;
        }

        localTick++;

        InputData input = new InputData
        {
            look = lookInput,
            roll = rollInput,
            strafe = moveAction.action.ReadValue<float>(),
            strafeV = verticalStrafe.action.ReadValue<float>(),
            accelerate = accelerateAction.action.IsPressed(),
            brake = brakeAction.action.IsPressed(),
            tick = localTick
        };

        // Calculate rotation delta based on input
        float pitchDelta = -input.look.y * pitchSpeed * mouseSensitivity * Time.fixedDeltaTime;
        float yawDelta = input.look.x * yawSpeed * mouseSensitivity * Time.fixedDeltaTime;
        float rollDelta = input.roll * rollSpeed * mouseSensitivity * Time.fixedDeltaTime;

        Vector3 rotationDelta = new Vector3(pitchDelta, yawDelta, rollDelta);

        // Clamp rotation delta magnitude and revert if exceeding max
        if (rotationDelta.magnitude > MAX_ROTATION_PER_TICK)
        {
            // Revert to last valid transform to prevent runaway rotation
            transform.position = lastValidPosition;
            transform.rotation = lastValidRotation;
            // Optional: reset velocity or skip movement this tick
            rb.linearVelocity = Vector3.zero;
        }
        else
        {
            // Apply rotation
            transform.Rotate(rotationDelta, Space.Self);

            // Store last valid state
            lastValidPosition = transform.position;
            lastValidRotation = transform.rotation;

            // Apply movement according to input
            HandleMovement(input);
        }

        // Buffer input for server reconciliation (optional)
        inputBuffer.Add(input);

        // Send processed input to server
        SendInputToServerRpc(input);
    }

    private void HandleMovement(InputData input)
    {
        if (input.accelerate)
            currentSpeed = Mathf.Min(currentSpeed + acceleration * Time.fixedDeltaTime, maxForwardSpeed);
        else if (input.brake)
            currentSpeed = Mathf.Max(currentSpeed - deceleration * Time.fixedDeltaTime, maxBackwardSpeed);
        else
            currentSpeed = Mathf.MoveTowards(currentSpeed, moveSpeed, deceleration * Time.fixedDeltaTime);

        Vector3 moveDir = transform.forward * currentSpeed +
                          transform.right * input.strafe * strafeSpeed +
                          transform.up * input.strafeV * strafeSpeed;

        rb.linearVelocity = moveDir;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendInputToServerRpc(InputData input, ServerRpcParams rpcParams = default)
    {
        if (input.tick <= lastProcessedTick) return;
        lastProcessedTick = input.tick;

        // Calculate rotation delta on server side (same as client)
        float pitchDelta = -input.look.y * pitchSpeed * mouseSensitivity * Time.fixedDeltaTime;
        float yawDelta = input.look.x * yawSpeed * mouseSensitivity * Time.fixedDeltaTime;
        float rollDelta = input.roll * rollSpeed * mouseSensitivity * Time.fixedDeltaTime * 2.0f; // roll multiplier

        Vector3 rotationDelta = new Vector3(pitchDelta, yawDelta, rollDelta);

        // Clamp rotation on server as well
        if (rotationDelta.magnitude > MAX_ROTATION_PER_TICK)
        {
            // Revert server state to last valid
            transform.position = lastValidPosition;
            transform.rotation = lastValidRotation;
            rb.linearVelocity = Vector3.zero;
        }
        else
        {
            transform.Rotate(rotationDelta, Space.Self);

            lastValidPosition = transform.position;
            lastValidRotation = transform.rotation;

            HandleMovement(input);
        }

        netPosition.Value = transform.position;
        netRotation.Value = transform.rotation;
        netVelocity.Value = rb.linearVelocity;

        // Send authoritative state back to client for reconciliation (could be improved with explicit tick)
        RotationAckClientRpc(transform.position, transform.rotation, input.tick);
    }

    [ClientRpc]
    private void RotationAckClientRpc(Vector3 authoritativePos, Quaternion authoritativeRot, int lastServerTick)
    {
        if (!IsOwner) return;

        // Remove acknowledged inputs if you buffer them (optional)
        inputBuffer.RemoveAll(i => i.tick <= lastServerTick);

        // Set authoritative position and rotation from server
        transform.position = authoritativePos;
        transform.rotation = authoritativeRot;
        lastValidPosition = authoritativePos;
        lastValidRotation = authoritativeRot;
    }
}