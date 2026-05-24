using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class FighterPiloting : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 0f;
    public float maxForwardSpeed = 100f; // Restored to original
    public float maxBackwardSpeed = -100f; // Restored to original
    public float acceleration = 50f; // Restored to original
    public float deceleration = 50f; // Restored to original
    public float strafeSpeed = 20f; // Restored to original
    public float pitchSpeed = 20f; // Restored to original (prefab value)
    public float yawSpeed = 20f; // Restored to original (prefab value)
    public float rollSpeed = 50f; // Restored to original (prefab value)

    public float mouseSensitivity = 1.0f; // Restored to original

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

        // Ensure we start unfrozen locally
        ignoreServerSync = false;
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

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Subscribe to Jump Events for robust state management
            if (JumpManager.Instance != null)
            {
                JumpManager.Instance.OnJumpStarted += HandleJumpStarted;
                JumpManager.Instance.OnJumpCompleted += HandleJumpCompleted;
                
                // If we spawned and no jump is actually happening in the visualizer, 
                // ensure we are unfrozen.
                if (SystemCenteredVisualizer.Instance != null && SystemCenteredVisualizer.Instance.currentSystemId.Value != 0)
                {
                    // If we just joined/spawned and data is already there, we should be playable
                    SetIgnoreServerSync(false);
                }
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            if (JumpManager.Instance != null)
            {
                JumpManager.Instance.OnJumpStarted -= HandleJumpStarted;
                JumpManager.Instance.OnJumpCompleted -= HandleJumpCompleted;
            }

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

    private void HandleJumpStarted(ulong id) => SetIgnoreServerSync(true);
    private void HandleJumpCompleted(ulong id) => SetIgnoreServerSync(false);

    private Vector2 accumulatedLookInput;
    private float accumulatedRollInput;
    private float safetyTimeout = 0f;

    private void Update()
    {
        if (!IsOwner) return;

        // Safety fallback: if we've been frozen for more than 10s, auto-unfreeze
        if (ignoreServerSync && Time.time > safetyTimeout)
        {
            Debug.LogWarning("[Fighter] Safety Timeout reached! Auto-unfreezing.");
            SetIgnoreServerSync(false);
        }

        if (ignoreServerSync)
        {
            accumulatedLookInput = Vector2.zero;
            accumulatedRollInput = 0f;
            return;
        }

        // Accumulate input every frame for maximum precision
        accumulatedLookInput += lookAction.action.ReadValue<Vector2>();
        accumulatedRollInput += rollAction.action.ReadValue<float>();
    }

    private bool ignoreServerSync = false;

    public void SetIgnoreServerSync(bool ignore)
    {
        Debug.Log($"[Fighter] SetIgnoreServerSync({ignore}) for {gameObject.name}");
        ignoreServerSync = ignore;
        if (rb != null) 
        {
            rb.isKinematic = ignore;
            if (!ignore)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                currentSpeed = 0f; // Reset speed after jump
            }
        }
        if (ignore)
        {
            accumulatedLookInput = Vector2.zero;
            accumulatedRollInput = 0f;
            safetyTimeout = Time.time + 10f; // 10 second safety cap
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
        {
            if (ignoreServerSync) return;
            // Non-owners follow server state smoothly
            transform.position = Vector3.Lerp(transform.position, netPosition.Value, 15f * Time.fixedDeltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, netRotation.Value, 15f * Time.fixedDeltaTime);
            rb.linearVelocity = netVelocity.Value;
            return;
        }

        if (ignoreServerSync) return;

        localTick++;

        // Consume the accumulated input for this simulation tick
        InputData input = new InputData
        {
            look = accumulatedLookInput,
            roll = accumulatedRollInput,
            strafe = moveAction.action.ReadValue<float>(),
            strafeV = verticalStrafe.action.ReadValue<float>(),
            accelerate = accelerateAction.action.IsPressed(),
            brake = brakeAction.action.IsPressed(),
            tick = localTick
        };

        // Reset accumulation for the next tick
        accumulatedLookInput = Vector2.zero;
        accumulatedRollInput = 0f;

        // Perform simulation
        ApplyRotation(input.look, input.roll, Time.fixedDeltaTime);
        HandleMovement(input);

        // Sync to server for parsing
        SendInputToServerRpc(input);
    }

    private void ApplyRotation(Vector2 look, float roll, float deltaTime)
    {
        float pitchDelta = -look.y * pitchSpeed * mouseSensitivity * deltaTime;
        float yawDelta = look.x * yawSpeed * mouseSensitivity * deltaTime;
        float rollDelta = roll * rollSpeed * deltaTime;

        transform.Rotate(new Vector3(pitchDelta, yawDelta, rollDelta), Space.Self);
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

        // Server-side authoritative simulation (The "Parsing")
        ApplyRotation(input.look, input.roll, Time.fixedDeltaTime);
        HandleMovement(input);

        // Update the NetworkVariables that other clients (and the owner) use
        netPosition.Value = transform.position;
        netRotation.Value = transform.rotation;
        netVelocity.Value = rb.linearVelocity;
    }

    // Removed RotationAckClientRpc's owner-side snapping logic to eliminate "drag"
}
