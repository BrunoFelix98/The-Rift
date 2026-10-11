using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared helpers for the procedural celestial visuals: materials (loaded from
/// Assets/Shaders/Celestial/Resources/Celestial), deterministic per-body randomness and generated meshes.
/// </summary>
public static class CelestialVisuals
{
    public static readonly int StarPosId = Shader.PropertyToID("_CelestialStarPos");
    public static readonly int StarColorId = Shader.PropertyToID("_CelestialStarColor");

    private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private static Mesh ringMesh;

    public const float RingInnerRadius = 0.65f; // planet mesh radius is 0.5
    public const float RingOuterRadius = 1.3f;

    public static Material GetMaterial(string name)
    {
        if (!materials.TryGetValue(name, out var mat) || mat == null)
        {
            mat = Resources.Load<Material>("Celestial/" + name);
            if (mat == null) Debug.LogWarning($"[CelestialVisuals] Missing material Resources/Celestial/{name}");
            materials[name] = mat;
        }
        return mat;
    }

    /// <summary>xorshift32 seeded from the body's id, identical on every client and platform.</summary>
    public struct Rng
    {
        private uint state;

        public Rng(ulong id, int extra)
        {
            state = (uint)(id ^ (id >> 32)) * 2654435761u ^ (uint)extra * 40503u ^ 0x9E3779B9u;
            if (state == 0) state = 1;
            for (int i = 0; i < 4; i++) Next();
        }

        public float Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFF) / 16777216f;
        }

        public float Range(float min, float max) => min + (max - min) * Next();
        public bool Chance(float probability) => Next() < probability;
    }

    /// <summary>Small brightness/tint jitter so bodies of the same type don't look identical.</summary>
    public static Color Vary(Color c, ref Rng rng, float amount)
    {
        float v = 1f + rng.Range(-amount, amount);
        return new Color(
            Mathf.Clamp01(c.r * v * (1f + rng.Range(-amount, amount) * 0.5f)),
            Mathf.Clamp01(c.g * v * (1f + rng.Range(-amount, amount) * 0.5f)),
            Mathf.Clamp01(c.b * v * (1f + rng.Range(-amount, amount) * 0.5f)),
            1f);
    }

    public static Mesh RingMesh
    {
        get
        {
            if (ringMesh != null) return ringMesh;

            const int segments = 160;
            var vertices = new Vector3[(segments + 1) * 2];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var tris = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float u = i / (float)segments;
                vertices[i * 2] = dir * RingInnerRadius;
                vertices[i * 2 + 1] = dir * RingOuterRadius;
                normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
                uvs[i * 2] = new Vector2(0f, u);
                uvs[i * 2 + 1] = new Vector2(1f, u);
            }

            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                int t = i * 6;
                tris[t] = v;
                tris[t + 1] = v + 1;
                tris[t + 2] = v + 3;
                tris[t + 3] = v;
                tris[t + 4] = v + 3;
                tris[t + 5] = v + 2;
            }

            ringMesh = new Mesh { name = "CelestialRing" };
            ringMesh.vertices = vertices;
            ringMesh.normals = normals;
            ringMesh.uv = uvs;
            ringMesh.triangles = tris;
            ringMesh.RecalculateBounds();
            return ringMesh;
        }
    }

    /// <summary>Finds or creates a child renderer used for an extra visual layer.</summary>
    public static MeshRenderer EnsureChild(Transform parent, string name, Mesh mesh, Material material)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            child = go.transform;
            child.SetParent(parent, false);
            go.AddComponent<MeshFilter>();
            var created = go.AddComponent<MeshRenderer>();
            created.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            created.receiveShadows = false;
        }

        child.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }
}
