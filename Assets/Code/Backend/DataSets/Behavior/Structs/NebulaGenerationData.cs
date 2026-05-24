using Unity.Burst;
using Unity.Netcode;
using UnityEngine;

[BurstCompile]
public struct NebulaGenerationData : INetworkSerializable
{
    // 0 = no nebula, >0 = index into a NebulaSO lookup table
    public int nebulaId;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref nebulaId);
    }
}