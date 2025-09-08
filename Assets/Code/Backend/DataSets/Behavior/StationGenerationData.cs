using Unity.Burst;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[BurstCompile]
public struct StationGenerationData : INetworkSerializable
{
    public FixedString64Bytes stationName;
    public OrbitParams orbitParams;
    public int allegianceID;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref stationName);
        orbitParams.NetworkSerialize(serializer);
        serializer.SerializeValue(ref allegianceID);
    }
}