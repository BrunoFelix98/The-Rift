using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

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
    public bool RequiresNetworking;

    // Extended Data for Instancing
    public FixedString32Bytes Name;
    public int FactionId;
    public FixedString32Bytes TemplateName;
    public int ResourceSeed;
    public CelestialEnvironment EnvironmentType; // Maps to CelestialEnvironment enum

    // Realism: Verticality and non-horizontal orbits
    public float TiltAngle; // Angle in degrees
    public Vector3 TiltAxis; // Axis to tilt around

    public OrbitParams(ulong networkId, ulong parentId, Vector3 centerPos, float radius, float angularSpeed, float phaseOffset, CelestialType type, bool requiresNetworking, string name = "", int factionId = -1, string templateName = "", int resSeed = 0, CelestialEnvironment envType = 0, float tiltAngle = 0, Vector3? tiltAxis = null)
    {
        NetworkId = networkId;
        ParentId = parentId;
        CenterPos = centerPos;
        Radius = radius;
        AngularSpeed = angularSpeed;
        PhaseOffset = phaseOffset;
        Type = type;
        RequiresNetworking = requiresNetworking;
        Name = name;
        FactionId = factionId;
        TemplateName = templateName;
        ResourceSeed = resSeed;
        EnvironmentType = envType;
        TiltAngle = tiltAngle;
        TiltAxis = tiltAxis ?? Vector3.right;
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
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref FactionId);
        serializer.SerializeValue(ref TemplateName);
        serializer.SerializeValue(ref ResourceSeed);
        serializer.SerializeValue(ref EnvironmentType);
        
        serializer.SerializeValue(ref TiltAngle);
        serializer.SerializeValue(ref TiltAxis);
    }
}
