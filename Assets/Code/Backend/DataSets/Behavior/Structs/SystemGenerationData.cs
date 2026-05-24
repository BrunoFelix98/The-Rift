using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[BurstCompile]
public struct SystemGenerationData : INetworkSerializable
{
    public FixedString64Bytes systemName;
    public OrbitParams orbitParams;
    public int allegianceID;
    public int starCount;
    public int starsListStartIndex;
    public int planetCount;
    public int planetsListStartIndex;
    public int asteroidBeltCount;
    public int asteroidBeltsListStartIndex;
    public int gateCount;
    public int gatesListStartIndex;
    public int stationCount;
    public int stationsListStartIndex;
    public bool requiresNebula;
    public int nebulaID;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref systemName);
        orbitParams.NetworkSerialize(serializer);
        serializer.SerializeValue(ref allegianceID);
        serializer.SerializeValue(ref starCount);
        serializer.SerializeValue(ref starsListStartIndex);
        serializer.SerializeValue(ref planetCount);
        serializer.SerializeValue(ref planetsListStartIndex);
        serializer.SerializeValue(ref asteroidBeltCount);
        serializer.SerializeValue(ref asteroidBeltsListStartIndex);
        serializer.SerializeValue(ref gateCount);
        serializer.SerializeValue(ref gatesListStartIndex);
        serializer.SerializeValue(ref stationCount);
        serializer.SerializeValue(ref stationsListStartIndex);
        serializer.SerializeValue(ref requiresNebula);
        serializer.SerializeValue(ref nebulaID);

    }
}