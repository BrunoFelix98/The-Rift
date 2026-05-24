using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[BurstCompile]
public struct PlanetGenerationData : INetworkSerializable
{
    public FixedString64Bytes planetName;
    public CelestialEnvironment type;
    public OrbitParams orbitParams;
    public int moonCount;
    public int moonListStartIndex;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref planetName);
        int t = (int)type;
        serializer.SerializeValue(ref t);
        if (serializer.IsReader) type = (CelestialEnvironment)t;
        orbitParams.NetworkSerialize(serializer);
        serializer.SerializeValue(ref moonCount);
        serializer.SerializeValue(ref moonListStartIndex);
    }
}