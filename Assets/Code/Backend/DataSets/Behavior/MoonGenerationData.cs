using Unity.Burst;
using Unity.Collections;
using Unity.Netcode;

[BurstCompile]
public struct MoonGenerationData : INetworkSerializable
{
    public FixedString64Bytes moonName;
    public CelestialEnvironment type;
    public OrbitParams orbitParams;
    public int stationCount;
    public int stationListStartIndex;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref moonName);
        int t = (int)type;
        serializer.SerializeValue(ref t);
        if (serializer.IsReader) type = (CelestialEnvironment)t;
        orbitParams.NetworkSerialize(serializer);
        serializer.SerializeValue(ref stationCount);
        serializer.SerializeValue(ref stationListStartIndex);
    }
}