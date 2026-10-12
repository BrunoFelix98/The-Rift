using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public class SystemPersistenceData : INetworkSerializable
{
    public ulong id;
    public string name;
    public Vector3d absolutePosition; 
    public float orbitRadius;
    public float orbitSpeed;
    public float phaseOffset;
    public float tiltAngle;
    public Vector3 tiltAxis;
    public ulong parentId; 
    public List<CelestialPersistenceData> celestials = new List<CelestialPersistenceData>();

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref id);
        serializer.SerializeValue(ref name);
        serializer.SerializeValue(ref absolutePosition);
        serializer.SerializeValue(ref orbitRadius);
        serializer.SerializeValue(ref orbitSpeed);
        serializer.SerializeValue(ref phaseOffset);
        serializer.SerializeValue(ref tiltAngle);
        serializer.SerializeValue(ref tiltAxis);
        serializer.SerializeValue(ref parentId);

        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            celestials = new List<CelestialPersistenceData>(count);
            for (int i = 0; i < count; i++)
            {
                CelestialPersistenceData c = new CelestialPersistenceData();
                serializer.SerializeValue(ref c);
                celestials.Add(c);
            }
        }
        else
        {
            int count = celestials?.Count ?? 0;
            serializer.SerializeValue(ref count);
            for (int i = 0; i < count; i++)
            {
                CelestialPersistenceData c = celestials[i];
                serializer.SerializeValue(ref c);
            }
        }
    }

    public Vector3d GetAbsolutePosition(double time, Vector3d parentPos)
    {
        if (orbitRadius <= 0) return absolutePosition;

        double angle = (time * orbitSpeed) + phaseOffset;
        Vector3 localPos = new Vector3((float)Math.Cos(angle) * orbitRadius, 0, (float)Math.Sin(angle) * orbitRadius);
        
        if (tiltAngle != 0)
        {
            localPos = Quaternion.AngleAxis(tiltAngle, tiltAxis) * localPos;
        }

        return parentPos + new Vector3d(localPos.x, localPos.y, localPos.z);
    }
}

[Serializable]
public class GalaxyPersistenceData : INetworkSerializable
{
    public ulong id;
    public string name;
    public Vector3d absolutePosition; 
    public float orbitRadius; 
    public float orbitSpeed;
    public float tiltAngle;
    public Vector3 tiltAxis;
    public List<SystemPersistenceData> systems = new List<SystemPersistenceData>();

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref id);
        serializer.SerializeValue(ref name);
        serializer.SerializeValue(ref absolutePosition);
        serializer.SerializeValue(ref orbitRadius);
        serializer.SerializeValue(ref orbitSpeed);
        serializer.SerializeValue(ref tiltAngle);
        serializer.SerializeValue(ref tiltAxis);

        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            systems = new List<SystemPersistenceData>(count);
            for (int i = 0; i < count; i++)
            {
                SystemPersistenceData s = new SystemPersistenceData();
                serializer.SerializeValue(ref s);
                systems.Add(s);
            }
        }
        else
        {
            int count = systems?.Count ?? 0;
            serializer.SerializeValue(ref count);
            for (int i = 0; i < count; i++)
            {
                SystemPersistenceData s = systems[i];
                serializer.SerializeValue(ref s);
            }
        }
    }

    public Vector3d GetAbsolutePosition(double time)
    {
        if (orbitRadius <= 0) return absolutePosition;

        double angle = (time * orbitSpeed);
        Vector3 localPos = new Vector3((float)Math.Cos(angle) * orbitRadius, 0, (float)Math.Sin(angle) * orbitRadius);
        
        if (tiltAngle != 0)
        {
            localPos = Quaternion.AngleAxis(tiltAngle, tiltAxis) * localPos;
        }

        return absolutePosition + new Vector3d(localPos.x, localPos.y, localPos.z);
    }
}

[Serializable]
public struct SystemMetadata : INetworkSerializable
{
    public ulong id;
    public Vector3d absolutePosition;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref id);
        serializer.SerializeValue(ref absolutePosition);
    }
}

[Serializable]
public struct SystemMetadataListWrapper : INetworkSerializable
{
    public List<SystemMetadata> systems;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            systems = new List<SystemMetadata>(count);
            for (int i = 0; i < count; i++)
            {
                SystemMetadata s = new SystemMetadata();
                serializer.SerializeValue(ref s);
                systems.Add(s);
            }
        }
        else
        {
            int count = systems?.Count ?? 0;
            serializer.SerializeValue(ref count);
            for (int i = 0; i < count; i++)
            {
                SystemMetadata s = systems[i];
                serializer.SerializeValue(ref s);
            }
        }
    }
}

[Serializable]
public struct SystemListWrapper : INetworkSerializable
{
    public List<SystemPersistenceData> systems;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            systems = new List<SystemPersistenceData>(count);
            for (int i = 0; i < count; i++)
            {
                SystemPersistenceData s = new SystemPersistenceData();
                serializer.SerializeValue(ref s);
                systems.Add(s);
            }
        }
        else
        {
            int count = systems?.Count ?? 0;
            serializer.SerializeValue(ref count);
            for (int i = 0; i < count; i++)
            {
                SystemPersistenceData s = systems[i];
                serializer.SerializeValue(ref s);
            }
        }
    }
}

[Serializable]
public struct CelestialBatchWrapper : INetworkSerializable
{
    public List<CelestialPersistenceData> celestials;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            celestials = new List<CelestialPersistenceData>(count);
            for (int i = 0; i < count; i++)
            {
                CelestialPersistenceData c = new CelestialPersistenceData(default);
                serializer.SerializeValue(ref c);
                celestials.Add(c);
            }
        }
        else
        {
            int count = celestials?.Count ?? 0;
            serializer.SerializeValue(ref count);
            for (int i = 0; i < count; i++)
            {
                CelestialPersistenceData c = celestials[i];
                serializer.SerializeValue(ref c);
            }
        }
    }
}

[Serializable]
public class UniverseMapDataV2
{
    public List<GalaxyPersistenceData> galaxies = new List<GalaxyPersistenceData>();
}
