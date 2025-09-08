using Unity.Burst;
using Unity.Collections;
using Unity.Netcode;

[BurstCompile]
public struct GateGenerationData : INetworkSerializable
{
    public FixedString64Bytes gateName;
    public OrbitParams orbitParams;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref gateName);
        orbitParams.NetworkSerialize(serializer);
    }
}