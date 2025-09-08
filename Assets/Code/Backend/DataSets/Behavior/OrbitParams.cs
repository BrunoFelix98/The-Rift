using System;
using Unity.Netcode;
using UnityEngine;
using static UnityEngine.XR.XRDisplaySubsystem;

[Serializable]
public struct OrbitParams : INetworkSerializable
{
    public ulong NetworkId;
    public ulong ParentId;
    public Vector3 CenterPos;
    public float Radius;
    public float AngularSpeed;
    public float PhaseOffset;
    public CelestialType Type;
    public bool RequiresNetworking; // true for asteroids/stations/gates

    public OrbitParams(ulong networkId, ulong parentId, Vector3 centerPos, float radius, float angularSpeed, float phaseOffset, CelestialType type, bool requiresNetworking)
    {
        NetworkId = networkId;
        ParentId = parentId;
        CenterPos = centerPos;
        Radius = radius;
        AngularSpeed = angularSpeed;
        PhaseOffset = phaseOffset;
        Type = type;
        RequiresNetworking = requiresNetworking;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref NetworkId);
        serializer.SerializeValue(ref ParentId);
        serializer.SerializeValue(ref CenterPos);
        serializer.SerializeValue(ref Radius);
        serializer.SerializeValue(ref AngularSpeed);
        serializer.SerializeValue(ref PhaseOffset);

        int t = (int)Type;
        serializer.SerializeValue(ref t);
        if (serializer.IsReader) Type = (CelestialType)t;

        serializer.SerializeValue(ref RequiresNetworking);
    }
}
