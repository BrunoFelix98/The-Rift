using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// FPS crew movement with the same prediction model as FighterPiloting. Each tick's input
/// (move, jump, yaw delta) is simulated by the owner and the server with the same step;
/// the owner reconciles against the server state by replaying unacknowledged inputs.
/// </summary>
public class PlayerMovement : NetworkBehaviour, ITeleportable
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpHeight = 2.5f;
    public float gravityValue = -9.81f;

    [Header("Jump Buffer Settings")]
    public float jumpBufferTime = 0.15f;
    public float groundedGracePeriod = 0.2f;

    public InputActionReference moveAction;
    public InputActionReference jumpAction;

    private CharacterController controller;
    private PlayerLook playerLook;

    private bool jumpRequested;
    private float jumpRequestTime;

    private struct InputData : INetworkSerializable
    {
        public int tick;
        public Vector2 move;
        public bool jump;
        public float yawDelta;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref move);
            serializer.SerializeValue(ref jump);
            serializer.SerializeValue(ref yawDelta);
        }
    }

    public struct CrewState : INetworkSerializeByMemcpy
    {
        public int tick;
        public Vector3d position; // World space, double so it stays exact far from the origin
        public float yaw;
        public float verticalVelocity;
        public Vector3 velocity;
        public int lastGroundedTick;
        public int lastJumpTick;
    }

    private NetworkVariable<CrewState> netState = new NetworkVariable<CrewState>(
        new CrewState { tick = -1, lastGroundedTick = -1000, lastJumpTick = -1000 },
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private CrewState simState;
    private readonly List<InputData> pendingInputs = new List<InputData>();
    private const int MaxPendingInputs = 500;
    private int localTick = 0;

    private int lastProcessedTick = -1;
    // Refills a bit faster than the tick rate: an exact match hovered near zero and dropped legit
    // inputs whenever packets arrived bunched, which the owner then saw as rubberbanding
    private const float MaxInputBurst = 50f;
    private const float InputRateAllowance = 1.25f;
    private float inputBudget = MaxInputBurst;

    private CrewState remoteState;
    private float remoteStateTime;
    private const float MaxExtrapolation = 0.25f;
    private const float RemoteSmoothing = 20f;

    private const float ReconcilePositionTolerance = 0.01f;
    private const int JumpCooldownTicks = 5;

    public Vector3d WorldPosition => IsOwner || IsServer ? simState.position : remoteState.position;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerLook = GetComponent<PlayerLook>();
    }

    public override void OnNetworkSpawn()
    {
        netState.OnValueChanged += OnServerStateChanged;
        FloatingOrigin.Shifted += OnOriginShifted;

        if (IsServer)
        {
            simState = new CrewState
            {
                tick = -1,
                position = FloatingOrigin.ToWorld(transform.position),
                yaw = transform.eulerAngles.y,
                lastGroundedTick = -1000,
                lastJumpTick = -1000
            };
            netState.Value = simState;
        }

        if (IsOwner)
        {
            if (!IsServer) simState = netState.Value;
            moveAction.action.Enable();
            jumpAction.action.Enable();
        }
        else if (!IsServer)
        {
            // Remote copies are only displayed; a live CharacterController would fight the smoothing
            controller.enabled = false;
            remoteState = netState.Value;
            remoteStateTime = Time.time;
            transform.SetPositionAndRotation(FloatingOrigin.ToRender(remoteState.position), Quaternion.Euler(0f, remoteState.yaw, 0f));
        }
    }

    private void OnOriginShifted(Vector3 delta)
    {
        if (IsOwner || IsServer) SetControllerPosition(FloatingOrigin.ToRender(simState.position));
        else transform.position -= delta;
    }

    public override void OnNetworkDespawn()
    {
        netState.OnValueChanged -= OnServerStateChanged;
        FloatingOrigin.Shifted -= OnOriginShifted;

        if (IsOwner)
        {
            moveAction.action.Disable();
            jumpAction.action.Disable();
        }
    }

    void Update()
    {
        if (IsServer)
        {
            inputBudget = Mathf.Min(inputBudget + InputRateAllowance * Time.deltaTime / Time.fixedDeltaTime, MaxInputBurst);
        }

        if (!IsOwner)
        {
            if (!IsServer) FollowRemoteState();
            return;
        }

        if (Application.isFocused && jumpAction.action.WasPressedThisFrame())
        {
            jumpRequested = true;
            jumpRequestTime = Time.time;
        }
        if (jumpRequested && Time.time - jumpRequestTime > jumpBufferTime) jumpRequested = false;

        // Yaw not yet consumed by a tick is shown immediately so looking stays responsive
        float pendingYaw = playerLook != null ? playerLook.PendingYawDelta : 0f;
        transform.rotation = Quaternion.Euler(0f, simState.yaw + pendingYaw, 0f);
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        localTick++;
        InputData input = new InputData
        {
            tick = localTick,
            move = Application.isFocused ? moveAction.action.ReadValue<Vector2>() : Vector2.zero,
            jump = jumpRequested,
            yawDelta = playerLook != null ? playerLook.ConsumeYawDelta() : 0f
        };

        simState = Step(simState, input, Time.fixedDeltaTime);
        // Keep a buffered jump alive until a tick could actually use it (or it expires in Update)
        if (simState.lastJumpTick == input.tick) jumpRequested = false;

        if (IsServer)
        {
            lastProcessedTick = input.tick;
            netState.Value = simState;
            return;
        }

        pendingInputs.Add(input);
        if (pendingInputs.Count > MaxPendingInputs) pendingInputs.RemoveAt(0);
        SendInputServerRpc(input);
    }

    // Shared by owner prediction, server simulation and reconcile replay. Moves the CharacterController
    // in render space and reads the result back into the world-space state.
    private CrewState Step(CrewState state, InputData input, float dt)
    {
        Vector3 startRender = FloatingOrigin.ToRender(state.position);
        if ((transform.position - startRender).sqrMagnitude > 1e-6f) SetControllerPosition(startRender);

        state.yaw = Mathf.Repeat(state.yaw + input.yawDelta, 360f);
        Quaternion rotation = Quaternion.Euler(0f, state.yaw, 0f);

        int graceTicks = Mathf.CeilToInt(groundedGracePeriod / dt);
        bool canJump = input.tick - state.lastGroundedTick <= graceTicks;
        bool cooledDown = input.tick - state.lastJumpTick > JumpCooldownTicks;
        if (input.jump && canJump && cooledDown)
        {
            state.verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravityValue);
            state.lastJumpTick = input.tick;
        }

        Vector3 move = rotation * Vector3.ClampMagnitude(new Vector3(input.move.x, 0f, input.move.y), 1f) * moveSpeed;
        state.verticalVelocity += gravityValue * dt;
        Vector3 velocity = move + Vector3.up * state.verticalVelocity;

        transform.rotation = rotation;
        CollisionFlags flags = controller.Move(velocity * dt);
        if ((flags & CollisionFlags.Below) != 0)
        {
            state.lastGroundedTick = input.tick;
            // Accumulated fall speed would otherwise launch the player down the next ledge
            if (state.verticalVelocity < 0f) state.verticalVelocity = -1f;
        }

        Vector3d newPosition = FloatingOrigin.ToWorld(transform.position);
        state.velocity = (newPosition - state.position).ToVector3() / dt;
        state.position = newPosition;
        state.tick = input.tick;
        return state;
    }

    [ServerRpc]
    void SendInputServerRpc(InputData input)
    {
        if (input.tick <= lastProcessedTick) return;
        if (inputBudget < 1f) return;
        inputBudget -= 1f;

        lastProcessedTick = input.tick;
        simState = Step(simState, input, Time.fixedDeltaTime);
        netState.Value = simState;
    }

    private void OnServerStateChanged(CrewState previous, CrewState current)
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

    private void Reconcile(CrewState serverState)
    {
        pendingInputs.RemoveAll(i => i.tick <= serverState.tick);

        // Predict the replay without disturbing the shown pose unless it actually differs
        Vector3d shownPosition = simState.position;
        SetControllerPosition(FloatingOrigin.ToRender(serverState.position));

        CrewState replayed = serverState;
        foreach (var input in pendingInputs)
        {
            replayed = Step(replayed, input, Time.fixedDeltaTime);
        }
        replayed.tick = simState.tick;

        bool off = Vector3d.Distance(replayed.position, shownPosition) > ReconcilePositionTolerance
                   || Mathf.Abs(Mathf.DeltaAngle(replayed.yaw, simState.yaw)) > 0.1f;
        simState = off ? replayed : simState;
        SetControllerPosition(FloatingOrigin.ToRender(simState.position));
    }

    private void FollowRemoteState()
    {
        float age = Mathf.Min(Time.time - remoteStateTime, MaxExtrapolation);
        Vector3 target = FloatingOrigin.ToRender(remoteState.position) + remoteState.velocity * age;
        float t = 1f - Mathf.Exp(-RemoteSmoothing * Time.deltaTime);

        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, target, t),
            Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, remoteState.yaw, 0f), t));
    }

    private void SetControllerPosition(Vector3 position)
    {
        // CharacterController ignores direct transform writes while enabled
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.position = position;
        controller.enabled = wasEnabled;
    }

    public void ServerTeleport(Vector3 position, Quaternion rotation)
    {
        if (!IsServer) return;

        simState.position = new Vector3d(position);
        simState.yaw = rotation.eulerAngles.y;
        simState.verticalVelocity = 0f;
        simState.velocity = Vector3.zero;
        SetControllerPosition(FloatingOrigin.ToRender(simState.position));
        transform.rotation = Quaternion.Euler(0f, simState.yaw, 0f);
        netState.Value = simState;
    }

    public void OwnerTeleport(Vector3 position, Quaternion rotation)
    {
        if (!IsOwner) return;

        simState.position = new Vector3d(position);
        simState.yaw = rotation.eulerAngles.y;
        simState.verticalVelocity = 0f;
        simState.velocity = Vector3.zero;
        SetControllerPosition(FloatingOrigin.ToRender(simState.position));
        transform.rotation = Quaternion.Euler(0f, simState.yaw, 0f);
    }
}
