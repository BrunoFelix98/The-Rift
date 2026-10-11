using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Server-authoritative fighter movement with client-side prediction.
/// The owner simulates its inputs immediately and sends them to the server, which runs the exact same
/// deterministic step and publishes the resulting state. The owner reconciles against that state by
/// replaying the inputs the server hasn't processed yet; other clients smooth towards it.
/// No physics integration is involved, so the result doesn't depend on frame timing.
/// </summary>
public class FighterPiloting : NetworkBehaviour, ITeleportable, IShipHudSource
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

    public float mouseSensitivity = 0.6f;

    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference verticalStrafe;
    public InputActionReference rollAction;
    public InputActionReference lookAction;
    public InputActionReference accelerateAction;
    public InputActionReference brakeAction;

    private Rigidbody rb;

    public Transform HudTransform => transform;
    public Vector3 HudVelocity => IsOwner ? simState.velocity : remoteState.velocity;
    public Vector3d WorldPosition => IsOwner || IsServer ? simState.position : remoteState.position;

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

    public struct FighterState : INetworkSerializeByMemcpy
    {
        public int tick; // Last input tick folded into this state
        public Vector3d position; // World space, double so it stays exact far from the origin
        public Quaternion rotation;
        public Vector3 velocity;
        public float speed;
    }

    private NetworkVariable<FighterState> netState = new NetworkVariable<FighterState>(
        new FighterState { tick = -1, rotation = Quaternion.identity },
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Owner: predicted state. Server: authoritative state.
    private FighterState simState;
    // Owner: pose before the last tick, rendered in between so motion is smooth at any frame rate
    private Vector3d prevPosition;
    private Quaternion prevRotation = Quaternion.identity;

    // Owner: inputs the server hasn't acknowledged yet, replayed on every reconcile
    private readonly List<InputData> pendingInputs = new List<InputData>();
    private const int MaxPendingInputs = 500;
    private int localTick = 0;

    // Server: last processed tick plus a budget so a client can't move faster by flooding inputs
    private int lastProcessedTick = -1;
    private float inputBudget;
    private const float MaxInputBurst = 25f;

    // Remote clients: latest server state and when it arrived, for smoothing/extrapolation
    private FighterState remoteState;
    private float remoteStateTime;
    private const float MaxExtrapolation = 0.25f;
    private const float RemoteSmoothing = 20f;

    private const float ReconcilePositionTolerance = 0.01f;
    private const float ReconcileAngleTolerance = 0.1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Pose is driven by code; a dynamic interpolated body made Transform writes timing-dependent
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;

        // Ensure we start unfrozen locally
        ignoreServerSync = false;
    }

    public override void OnNetworkSpawn()
    {
        netState.OnValueChanged += OnServerStateChanged;
        FloatingOrigin.Shifted += OnOriginShifted;

        if (IsServer)
        {
            simState = new FighterState
            {
                tick = -1,
                position = FloatingOrigin.ToWorld(transform.position),
                rotation = transform.rotation,
                speed = moveSpeed
            };
            netState.Value = simState;
        }

        if (IsOwner)
        {
            if (!IsServer) simState = netState.Value;
            simState.speed = moveSpeed;
            prevPosition = simState.position;
            prevRotation = simState.rotation;

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
                if (SystemCenteredVisualizer.Instance != null && SystemCenteredVisualizer.Instance.LocalSystemId != 0)
                {
                    // If we just joined/spawned and data is already there, we should be playable
                    SetIgnoreServerSync(false);
                }
            }
        }
        else if (!IsServer)
        {
            remoteState = netState.Value;
            remoteStateTime = Time.time;
            transform.SetPositionAndRotation(FloatingOrigin.ToRender(remoteState.position), remoteState.rotation);
        }
    }

    private void OnOriginShifted(Vector3 delta)
    {
        // Authoritative/predicted copies are re-placed from their world state; smoothed remote
        // copies keep their in-between position, just moved with the origin
        // The owner's pose is rebuilt from world space every frame, nothing to do for it
        if (IsOwner) return;
        if (IsServer) SnapPose(simState);
        else transform.position -= delta;
    }

    public override void OnNetworkDespawn()
    {
        netState.OnValueChanged -= OnServerStateChanged;
        FloatingOrigin.Shifted -= OnOriginShifted;

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
    private float safetyTimeout = 0f;

    private void Update()
    {
        if (IsServer)
        {
            inputBudget = Mathf.Min(inputBudget + Time.deltaTime / Time.fixedDeltaTime, MaxInputBurst);
        }

        if (!IsOwner)
        {
            if (!IsServer) FollowRemoteState();
            return;
        }

        ApplyOwnerPose();

        // Safety fallback: if we've been frozen for more than 10s, auto-unfreeze
        if (ignoreServerSync && Time.time > safetyTimeout)
        {
            Debug.LogWarning("[Fighter] Safety Timeout reached! Auto-unfreezing.");
            SetIgnoreServerSync(false);
        }

        if (ignoreServerSync)
        {
            accumulatedLookInput = Vector2.zero;
            return;
        }

        // Mouse delta is per frame, so it must be summed until the next simulation tick
        accumulatedLookInput += lookAction.action.ReadValue<Vector2>();
    }

    private bool ignoreServerSync = false;

    public void SetIgnoreServerSync(bool ignore)
    {
        Debug.Log($"[Fighter] SetIgnoreServerSync({ignore}) for {gameObject.name}");
        ignoreServerSync = ignore;
        if (!ignore)
        {
            simState.velocity = Vector3.zero;
            simState.speed = moveSpeed; // Reset speed after jump, matches ServerTeleport
            prevPosition = simState.position;
            prevRotation = simState.rotation;
        }
        else
        {
            accumulatedLookInput = Vector2.zero;
            safetyTimeout = Time.time + 10f; // 10 second safety cap
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;
        if (ignoreServerSync)
        {
            prevPosition = simState.position;
            prevRotation = simState.rotation;
            return;
        }

        localTick++;

        InputData input = new InputData
        {
            look = accumulatedLookInput,
            // Held keys are sampled per tick; summing them per frame made roll speed depend on frame rate
            roll = rollAction.action.ReadValue<float>(),
            strafe = moveAction.action.ReadValue<float>(),
            strafeV = verticalStrafe.action.ReadValue<float>(),
            accelerate = accelerateAction.action.IsPressed(),
            brake = brakeAction.action.IsPressed(),
            tick = localTick
        };
        accumulatedLookInput = Vector2.zero;

        prevPosition = simState.position;
        prevRotation = simState.rotation;
        simState = Step(simState, input, Time.fixedDeltaTime);

        if (IsServer)
        {
            // Host: its own ship is authoritative already, an RPC to itself would simulate twice
            lastProcessedTick = input.tick;
            netState.Value = simState;
            return;
        }

        pendingInputs.Add(input);
        if (pendingInputs.Count > MaxPendingInputs) pendingInputs.RemoveAt(0);

        SendInputToServerRpc(input);
    }

    // Shared by owner prediction, server simulation and reconcile replay; must stay deterministic
    private FighterState Step(FighterState state, InputData input, float dt)
    {
        float pitchDelta = -input.look.y * pitchSpeed * mouseSensitivity * dt;
        float yawDelta = input.look.x * yawSpeed * mouseSensitivity * dt;
        float rollDelta = input.roll * rollSpeed * dt;
        state.rotation = (state.rotation * Quaternion.Euler(pitchDelta, yawDelta, rollDelta)).normalized;

        if (input.accelerate)
            state.speed = Mathf.Min(state.speed + acceleration * dt, maxForwardSpeed);
        else if (input.brake)
            state.speed = Mathf.Max(state.speed - deceleration * dt, maxBackwardSpeed);
        else
            state.speed = Mathf.MoveTowards(state.speed, moveSpeed, deceleration * dt);

        state.velocity = state.rotation * Vector3.forward * state.speed +
                         state.rotation * Vector3.right * (input.strafe * strafeSpeed) +
                         state.rotation * Vector3.up * (input.strafeV * strafeSpeed);
        state.position = state.position + new Vector3d(state.velocity * dt);
        state.tick = input.tick;
        return state;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendInputToServerRpc(InputData input, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (input.tick <= lastProcessedTick) return;

        // Dropped inputs show up as a correction on the owner instead of extra speed
        if (inputBudget < 1f) return;
        inputBudget -= 1f;

        lastProcessedTick = input.tick;
        simState = Step(simState, input, Time.fixedDeltaTime);
        SnapPose(simState);
        netState.Value = simState;
    }

    private void OnServerStateChanged(FighterState previous, FighterState current)
    {
        if (IsServer) return;

        if (IsOwner)
        {
            Reconcile(current);
        }
        else
        {
            remoteState = current;
            remoteStateTime = Time.time;
        }
    }

    private void Reconcile(FighterState serverState)
    {
        pendingInputs.RemoveAll(i => i.tick <= serverState.tick);

        FighterState replayed = serverState;
        foreach (var input in pendingInputs)
        {
            replayed = Step(replayed, input, Time.fixedDeltaTime);
        }
        replayed.tick = simState.tick;

        bool positionOff = Vector3d.Distance(replayed.position, simState.position) > ReconcilePositionTolerance;
        bool rotationOff = Quaternion.Angle(replayed.rotation, simState.rotation) > ReconcileAngleTolerance;
        if (positionOff || rotationOff)
        {
            simState = replayed;
        }
    }

    private void FollowRemoteState()
    {
        float age = Mathf.Min(Time.time - remoteStateTime, MaxExtrapolation);
        Vector3 target = FloatingOrigin.ToRender(remoteState.position) + remoteState.velocity * age;
        float t = 1f - Mathf.Exp(-RemoteSmoothing * Time.deltaTime);

        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, target, t),
            Quaternion.Slerp(transform.rotation, remoteState.rotation, t));
    }

    // Between the last two ticks, converted to render space each frame
    private void ApplyOwnerPose()
    {
        float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
        Vector3d position = prevPosition + (simState.position - prevPosition) * alpha;
        Vector3 renderPos = FloatingOrigin.ToRender(position);
        Quaternion rotation = Quaternion.Slerp(prevRotation, simState.rotation, alpha);
        transform.SetPositionAndRotation(renderPos, rotation);
        rb.position = renderPos;
        rb.rotation = rotation;
    }

    // Instant placement without interpolating through the gap
    private void SnapPose(FighterState state)
    {
        Vector3 renderPos = FloatingOrigin.ToRender(state.position);
        transform.SetPositionAndRotation(renderPos, state.rotation);
        rb.position = renderPos;
        rb.rotation = state.rotation;
    }

    public void ServerTeleport(Vector3 position, Quaternion rotation)
    {
        if (!IsServer) return;

        // Keep the tick: the owner replays its unacknowledged inputs from the new pose
        simState.position = new Vector3d(position);
        simState.rotation = rotation;
        simState.velocity = Vector3.zero;
        simState.speed = moveSpeed;
        prevPosition = simState.position;
        prevRotation = rotation;
        SnapPose(simState);
        netState.Value = simState;
    }

    public void OwnerTeleport(Vector3 position, Quaternion rotation)
    {
        if (!IsOwner) return;

        simState.position = new Vector3d(position);
        simState.rotation = rotation;
        simState.velocity = Vector3.zero;
        simState.speed = moveSpeed;
        prevPosition = simState.position;
        prevRotation = rotation;
        SnapPose(simState);
    }
}
