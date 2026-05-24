using System;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public struct Vector3d : INetworkSerializable
{
    public double x;
    public double y;
    public double z;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref x);
        serializer.SerializeValue(ref y);
        serializer.SerializeValue(ref z);
    }

    public Vector3d(double x, double y, double z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public Vector3d(Vector3 v)
    {
        this.x = v.x;
        this.y = v.y;
        this.z = v.z;
    }

    public static Vector3d zero => new Vector3d(0, 0, 0);

    public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
    public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
    public static Vector3d operator *(Vector3d a, double b) => new Vector3d(a.x * b, a.y * b, a.z * b);

    public Vector3 ToVector3() => new Vector3((float)x, (float)y, (float)z);
    
    public double magnitude => Math.Sqrt(x * x + y * y + z * z);
    
    public static double Distance(Vector3d a, Vector3d b)
    {
        return (a - b).magnitude;
    }

    public override string ToString() => $"({x}, {y}, {z})";
}
