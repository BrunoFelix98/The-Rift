using System;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public class ResourcePersistenceData : INetworkSerializable
{
    public int id;
    public string name;
    public int quantity;
    public int minQuantity;
    public int maxQuantity;
    public float weight;
    public string rarity;

    public ResourcePersistenceData() { } // Required for INetworkSerializable

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref id);
        serializer.SerializeValue(ref name);
        serializer.SerializeValue(ref quantity);
        serializer.SerializeValue(ref minQuantity);
        serializer.SerializeValue(ref maxQuantity);
        serializer.SerializeValue(ref weight);
        serializer.SerializeValue(ref rarity);
    }

    public void WriteBinary(BinaryWriter writer)
    {
        writer.Write(id);
        writer.Write(name ?? "");
        writer.Write(quantity);
        writer.Write(minQuantity);
        writer.Write(maxQuantity);
        writer.Write(weight);
        writer.Write(rarity ?? "");
    }

    // ~3M strings in UniverseMap.bin but only ~31 distinct values; interning keeps one copy each
    public static ResourcePersistenceData ReadBinary(BinaryReader reader)
    {
        return new ResourcePersistenceData
        {
            id = reader.ReadInt32(),
            name = string.Intern(reader.ReadString()),
            quantity = reader.ReadInt32(),
            minQuantity = reader.ReadInt32(),
            maxQuantity = reader.ReadInt32(),
            weight = reader.ReadSingle(),
            rarity = string.Intern(reader.ReadString())
        };
    }
}

[Serializable]
public class OwnershipPersistenceData : INetworkSerializable
{
    public string factionName;
    public string corporationName;
    public string leaderName;
    public int factionId;
    public string factionColor;

    public OwnershipPersistenceData() { } // Required for INetworkSerializable

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref factionName);
        serializer.SerializeValue(ref corporationName);
        serializer.SerializeValue(ref leaderName);
        serializer.SerializeValue(ref factionId);
        serializer.SerializeValue(ref factionColor);
    }

    public void WriteBinary(BinaryWriter writer)
    {
        writer.Write(factionName ?? "");
        writer.Write(corporationName ?? "");
        writer.Write(leaderName ?? "");
        writer.Write(factionId);
        writer.Write(factionColor ?? "");
    }

    public static OwnershipPersistenceData ReadBinary(BinaryReader reader)
    {
        return new OwnershipPersistenceData
        {
            factionName = string.Intern(reader.ReadString()),
            corporationName = string.Intern(reader.ReadString()),
            leaderName = string.Intern(reader.ReadString()),
            factionId = reader.ReadInt32(),
            factionColor = string.Intern(reader.ReadString())
        };
    }
}

[Serializable]
public class CelestialPersistenceData : INetworkSerializable
{
    public OrbitParams orbit;
    public List<ResourcePersistenceData> resources = new List<ResourcePersistenceData>();
    public OwnershipPersistenceData ownership = new OwnershipPersistenceData();
    public List<string> modules = new List<string>();
    public ulong destinationId;
    public List<ulong> childIds = new List<ulong>();

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref orbit);
        
        // Serialize Lists manually
        if (serializer.IsReader)
        {
            int resCount = 0;
            serializer.SerializeValue(ref resCount);
            resources = new List<ResourcePersistenceData>(resCount);
            for (int i = 0; i < resCount; i++)
            {
                ResourcePersistenceData r = new ResourcePersistenceData();
                serializer.SerializeValue(ref r);
                resources.Add(r);
            }

            int modCount = 0;
            serializer.SerializeValue(ref modCount);
            modules = new List<string>(modCount);
            for (int i = 0; i < modCount; i++)
            {
                string m = "";
                serializer.SerializeValue(ref m);
                modules.Add(m);
            }

            int childCount = 0;
            serializer.SerializeValue(ref childCount);
            childIds = new List<ulong>(childCount);
            for (int i = 0; i < childCount; i++)
            {
                ulong c = 0;
                serializer.SerializeValue(ref c);
                childIds.Add(c);
            }
        }
        else
        {
            int resCount = resources?.Count ?? 0;
            serializer.SerializeValue(ref resCount);
            for (int i = 0; i < resCount; i++)
            {
                ResourcePersistenceData r = resources[i];
                serializer.SerializeValue(ref r);
            }

            int modCount = modules?.Count ?? 0;
            serializer.SerializeValue(ref modCount);
            for (int i = 0; i < modCount; i++)
            {
                string m = modules[i];
                serializer.SerializeValue(ref m);
            }

            int childCount = childIds?.Count ?? 0;
            serializer.SerializeValue(ref childCount);
            for (int i = 0; i < childCount; i++)
            {
                ulong c = childIds[i];
                serializer.SerializeValue(ref c);
            }
        }

        serializer.SerializeValue(ref ownership);
        serializer.SerializeValue(ref destinationId);
    }

    public CelestialPersistenceData(OrbitParams op)
    {
        orbit = op;
    }

    public CelestialPersistenceData() { } // For deserialization

    public void WriteBinary(BinaryWriter writer)
    {
        orbit.WriteBinary(writer);
        
        writer.Write(resources.Count);
        foreach (var r in resources) r.WriteBinary(writer);

        ownership.WriteBinary(writer);

        writer.Write(modules.Count);
        foreach (var m in modules) writer.Write(m ?? "");

        writer.Write(destinationId);

        writer.Write(childIds.Count);
        foreach (var c in childIds) writer.Write(c);
    }

    public static CelestialPersistenceData ReadBinary(BinaryReader reader)
    {
        var data = new CelestialPersistenceData();
        data.orbit = OrbitParamsExtensions.ReadBinary(reader);

        int resCount = reader.ReadInt32();
        data.resources = new List<ResourcePersistenceData>(resCount);
        for (int i = 0; i < resCount; i++) data.resources.Add(ResourcePersistenceData.ReadBinary(reader));

        data.ownership = OwnershipPersistenceData.ReadBinary(reader);

        int modCount = reader.ReadInt32();
        data.modules = new List<string>(modCount);
        for (int i = 0; i < modCount; i++) data.modules.Add(string.Intern(reader.ReadString()));

        data.destinationId = reader.ReadUInt64();

        int childCount = reader.ReadInt32();
        data.childIds = new List<ulong>(childCount);
        for (int i = 0; i < childCount; i++) data.childIds.Add(reader.ReadUInt64());

        return data;
    }
}

public static class OrbitParamsExtensions
{
    public static void WriteBinary(this OrbitParams op, BinaryWriter writer)
    {
        writer.Write(op.NetworkId);
        writer.Write(op.ParentId);
        writer.Write(op.CenterPos.x);
        writer.Write(op.CenterPos.y);
        writer.Write(op.CenterPos.z);
        writer.Write(op.Radius);
        writer.Write(op.AngularSpeed);
        writer.Write(op.PhaseOffset);
        writer.Write((int)op.Type);
        writer.Write(op.RequiresNetworking);
        writer.Write(op.Name.ToString());
        writer.Write(op.FactionId);
        writer.Write(op.TemplateName.ToString());
        writer.Write(op.ResourceSeed);
        writer.Write((int)op.EnvironmentType);
        writer.Write(op.TiltAngle);
        writer.Write(op.TiltAxis.x);
        writer.Write(op.TiltAxis.y);
        writer.Write(op.TiltAxis.z);
    }

    public static OrbitParams ReadBinary(BinaryReader reader)
    {
        var op = new OrbitParams();
        op.NetworkId = reader.ReadUInt64();
        op.ParentId = reader.ReadUInt64();
        op.CenterPos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        op.Radius = reader.ReadSingle();
        op.AngularSpeed = reader.ReadSingle();
        op.PhaseOffset = reader.ReadSingle();
        op.Type = (CelestialType)reader.ReadInt32();
        op.RequiresNetworking = reader.ReadBoolean();
        op.Name = reader.ReadString();
        op.FactionId = reader.ReadInt32();
        op.TemplateName = reader.ReadString();
        op.ResourceSeed = reader.ReadInt32();
        op.EnvironmentType = (CelestialEnvironment)reader.ReadInt32();
        op.TiltAngle = reader.ReadSingle();
        op.TiltAxis = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        return op;
    }
}

[Serializable]
public class UniverseMapData
{
    public List<CelestialPersistenceData> objects = new List<CelestialPersistenceData>();
}
