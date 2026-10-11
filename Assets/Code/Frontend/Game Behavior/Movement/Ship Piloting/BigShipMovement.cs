using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Click-to-move big ship with the same prediction model as FighterPiloting: the owner simulates
/// every tick and sends the tick's input, the server runs the identical deterministic step and
/// publishes the state, the owner reconciles by replaying unacknowledged inputs.
/// </summary>
public class BigShipMovement : NetworkBehaviour, ITeleportable, IShipHudSource
{
    [Header("Movement Settings")]
    public Camera mainCamera;
    public float moveSpeed = 40f;
    public float rotationSpeed = 120f;
    public Transform shipModel;
    public float stopDistance = 1f;
    public float doubleClickThreshold = 0.3f;
    private float lastClickTime = -10f;
    private float targetDistance = 150f;

    private struct InputData : INetworkSerializable
    {
        public int tick;
        public bool hasNewTarget;
        public Vector3d newTarget; // World space

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref hasNewTarget);
            serializer.SerializeValue(ref newTarget);
        }
    }

    public struct ShipState : INetworkSerializeByMemcpy
    {
        public int tick;
        public Vector3d position; // World space, double so it stays exact far from the origin
        public Quaternion rotation; // Applied to shipModel; the root only translates
        public Vector3 velocity;
        public Vector3d target;
        public bool hasTarget;
    }

    private NetworkVariable<ShipState> netState = new NetworkVariable<ShipState>(
        new ShipState { tick = -1, rotation = Quaternion.identity },
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private ShipState simState;
    private readonly List<InputData> pendingInputs = new List<InputData>();
    private const int MaxPendingInputs = 500;
    private int localTick = 0;
    private bool hasQueuedTarget;
    private Vector3d queuedTarget;
    // Owner: pose before the last tick, rendered in between
    private Vector3d prevPosition;
    private Quaternion prevRotation = Quaternion.identity;

    private int lastProcessedTick = -1;
    private float inputBudget;
    private const float MaxInputBurst = 25f;

    private ShipState remoteState;
    private float remoteStateTime;
    private const float MaxExtrapolation = 0.25f;
    private const float RemoteSmoothing = 15f;

    private const float ReconcilePositionTolerance = 0.01f;
    private const float ReconcileAngleTolerance = 0.1f;

    private Transform RotationTarget => shipModel != null ? shipModel : transform;

    public Transform HudTransform => RotationTarget;
    public Vector3 HudVelocity => IsOwner ? simState.velocity : remoteState.velocity;
    public Vector3d WorldPosition => IsOwner || IsServer ? simState.position : remoteState.position;

    private void Awake()
    {
        // The model's rigidbody would otherwise simulate on its own under the moving root
        foreach (var body in GetComponentsInChildren<Rigidbody>())
        {
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
        }
    }

    public override void OnNetworkSpawn()
    {
        netState.OnValueChanged += OnServerStateChanged;
        FloatingOrigin.Shifted += OnOriginShifted;

        if (IsServer)
        {
            simState = new ShipState { tick = -1, position = FloatingOrigin.ToWorld(transform.position), rotation = RotationTarget.rotation };
            netState.Value = simState;
        }

        if (IsOwner)
        {
            if (!IsServer) simState = netState.Value;
            prevPosition = simState.position;
            prevRotation = simState.rotation;
        }
        else if (!IsServer)
        {
            remoteState = netState.Value;
            remoteStateTime = Time.time;
            ApplyPose(remoteState.position, remoteState.rotation);
        }
    }

    public override void OnNetworkDespawn()
    {
        netState.OnValueChanged -= OnServerStateChanged;
        FloatingOrigin.Shifted -= OnOriginShifted;
    }

    private void OnOriginShifted(Vector3 delta)
    {
        if (IsOwner) return; // Rebuilt from world space every frame
        if (IsServer) ApplyPose(simState.position, simState.rotation);
        else transform.position -= delta;
    }

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
        if (Application.isFocused) HandleInput();
    }

    private void HandleInput()
    {
        if (!Input.GetMouseButtonDown(0) || mainCamera == null) return;

        if (Time.time - lastClickTime <= doubleClickThreshold)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            queuedTarget = FloatingOrigin.ToWorld(ray.origin + ray.direction * targetDistance);
            hasQueuedTarget = true;
        }
        lastClickTime = Time.time;
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        // Idle ticks change nothing, so neither side needs to step them
        if (!hasQueuedTarget && !simState.hasTarget)
        {
            prevPosition = simState.position;
            prevRotation = simState.rotation;
            return;
        }

        localTick++;
        InputData input = new InputData { tick = localTick, hasNewTarget = hasQueuedTarget, newTarget = queuedTarget };
        hasQueuedTarget = false;

        prevPosition = simState.position;
        prevRotation = simState.rotation;
        simState = Step(simState, input, Time.fixedDeltaTime);

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

    // Shared by owner prediction, server simulation and reconcile replay; must stay deterministic
    private ShipState Step(ShipState state, InputData input, float dt)
    {
        if (input.hasNewTarget)
        {
            state.target = input.newTarget;
            state.hasTarget = true;
        }

        state.velocity = Vector3.zero;
        if (state.hasTarget)
        {
            Vector3d direction = state.target - state.position;
            double distance = direction.magnitude;
            if (distance <= stopDistance)
            {
                state.hasTarget = false;
            }
            else
            {
                Vector3 dir = (direction * (1.0 / distance)).ToVector3().normalized;
                float angleToTarget = Vector3.Angle(state.rotation * Vector3.forward, dir);
                float speedFactor = Mathf.Lerp(1f, 0.3f, angleToTarget / 180f);

                // Never overshoot the target in one tick
                float stepLength = (float)System.Math.Min(moveSpeed * speedFactor * dt, distance);
                state.velocity = dir * (stepLength / dt);
                state.position = state.position + new Vector3d(dir * stepLength);
                state.rotation = Quaternion.RotateTowards(state.rotation, Quaternion.LookRotation(dir, Vector3.up), rotationSpeed * dt);
            }
        }

        state.tick = input.tick;
        return state;
    }

    [ServerRpc]
    private void SendInputServerRpc(InputData input)
    {
        if (input.tick <= lastProcessedTick) return;
        if (inputBudget < 1f) return;
        inputBudget -= 1f;

        lastProcessedTick = input.tick;
        simState = Step(simState, input, Time.fixedDeltaTime);
        ApplyPose(simState.position, simState.rotation);
        netState.Value = simState;
    }

    private void OnServerStateChanged(ShipState previous, ShipState current)
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

    private void Reconcile(ShipState serverState)
    {
        pendingInputs.RemoveAll(i => i.tick <= serverState.tick);

        ShipState replayed = serverState;
        foreach (var input in pendingInputs)
        {
            replayed = Step(replayed, input, Time.fixedDeltaTime);
        }
        replayed.tick = simState.tick;

        bool positionOff = Vector3d.Distance(replayed.position, simState.position) > ReconcilePositionTolerance;
        bool rotationOff = Quaternion.Angle(replayed.rotation, simState.rotation) > ReconcileAngleTolerance;
        if (positionOff || rotationOff || replayed.hasTarget != simState.hasTarget)
        {
            simState = replayed;
        }
    }

    private void FollowRemoteState()
    {
        float age = Mathf.Min(Time.time - remoteStateTime, MaxExtrapolation);
        Vector3 target = FloatingOrigin.ToRender(remoteState.position) + remoteState.velocity * age;
        float t = 1f - Mathf.Exp(-RemoteSmoothing * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, target, t);
        RotationTarget.rotation = Quaternion.Slerp(RotationTarget.rotation, remoteState.rotation, t);
    }

    private void ApplyOwnerPose()
    {
        float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
        Vector3d position = prevPosition + (simState.position - prevPosition) * alpha;
        ApplyPose(position, Quaternion.Slerp(prevRotation, simState.rotation, alpha));
    }

    private void ApplyPose(Vector3d worldPosition, Quaternion rotation)
    {
        transform.position = FloatingOrigin.ToRender(worldPosition);
        RotationTarget.rotation = rotation;
    }

    public void ServerTeleport(Vector3 position, Quaternion rotation)
    {
        if (!IsServer) return;

        // Keep the tick: the owner replays its unacknowledged inputs from the new pose
        simState.position = new Vector3d(position);
        simState.rotation = rotation;
        simState.velocity = Vector3.zero;
        simState.hasTarget = false;
        prevPosition = simState.position;
        prevRotation = rotation;
        ApplyPose(simState.position, rotation);
        netState.Value = simState;
    }

    public void OwnerTeleport(Vector3 position, Quaternion rotation)
    {
        if (!IsOwner) return;

        simState.position = new Vector3d(position);
        simState.rotation = rotation;
        simState.velocity = Vector3.zero;
        simState.hasTarget = false;
        hasQueuedTarget = false;
        prevPosition = simState.position;
        prevRotation = rotation;
        ApplyPose(simState.position, rotation);
    }
}
