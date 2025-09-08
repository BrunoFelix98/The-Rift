using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Netcode;

[BurstCompile]
public struct GalaxyGenerationData : INetworkSerializable
{
    public FixedString64Bytes galaxyName;
    public OrbitParams orbitParams;
    public int dustParticles;
    public int systemCount;
    public int systemListStartIndex;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref galaxyName);
        orbitParams.NetworkSerialize(serializer);
        serializer.SerializeValue(ref dustParticles);
        serializer.SerializeValue(ref systemCount);
        serializer.SerializeValue(ref systemListStartIndex);
    }
}