using Unity.Burst;
using Unity.Collections;
using Unity.Netcode;

[BurstCompile]
public struct AsteroidBeltGenerationData : INetworkSerializable
{
    public FixedString64Bytes asteroidBeltName;
    public CelestialEnvironment type;
    public OrbitParams orbitParams;
    public int asteroidCount;
    public int asteroidListStartIndex;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref asteroidBeltName);
        int t = (int)type;
        serializer.SerializeValue(ref t);
        if (serializer.IsReader) type = (CelestialEnvironment)t;
        orbitParams.NetworkSerialize(serializer);
        serializer.SerializeValue(ref asteroidCount);
        serializer.SerializeValue(ref asteroidListStartIndex);
    }
}