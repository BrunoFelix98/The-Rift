using Unity.Burst;
using Unity.Collections;
using Unity.Netcode;

[BurstCompile]
public struct StarGenerationData : INetworkSerializable
{
    public FixedString64Bytes starName;
    public OrbitParams orbitParams;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref starName);
        orbitParams.NetworkSerialize(serializer);
    }
}