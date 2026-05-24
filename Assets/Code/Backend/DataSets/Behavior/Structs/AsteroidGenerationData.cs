using Unity.Burst;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[BurstCompile]
public struct AsteroidGenerationData : INetworkSerializable
{
    public FixedString64Bytes asteroidName;
    public CelestialEnvironment type;
    public OrbitParams orbitParams;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref asteroidName);
        int t = (int)type;
        serializer.SerializeValue(ref t);
        if (serializer.IsReader) type = (CelestialEnvironment)t;
        orbitParams.NetworkSerialize(serializer);
    }
}