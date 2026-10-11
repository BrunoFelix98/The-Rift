using UnityEngine;

/// <summary>
/// Procedural look for planets and moons: environment-driven surface, atmosphere shell and rings.
/// Everything derives from the body's NetworkId/ResourceSeed, so all clients see the same planet.
/// Pooled instances are fully reconfigured on every Apply.
/// </summary>
public class CelestialBodyVisuals : MonoBehaviour
{
    private static readonly int EnvId = Shader.PropertyToID("_Env");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int ColorAId = Shader.PropertyToID("_ColorA");
    private static readonly int ColorBId = Shader.PropertyToID("_ColorB");
    private static readonly int ColorCId = Shader.PropertyToID("_ColorC");
    private static readonly int ColorDId = Shader.PropertyToID("_ColorD");
    private static readonly int SeaLevelId = Shader.PropertyToID("_SeaLevel");
    private static readonly int CloudAmountId = Shader.PropertyToID("_CloudAmount");
    private static readonly int CraterAmountId = Shader.PropertyToID("_CraterAmount");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
    private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
    private static readonly int AtmoColorId = Shader.PropertyToID("_AtmoColor");
    private static readonly int AtmoStrengthId = Shader.PropertyToID("_AtmoStrength");
    private static readonly int RingColorAId = Shader.PropertyToID("_RingColorA");
    private static readonly int RingColorBId = Shader.PropertyToID("_RingColorB");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

    private const float AtmosphereScale = 1.07f;

    private static readonly Color[][] GasPalettes =
    {
        // band A, band B, highlight, storm
        new[] { new Color(0.86f, 0.74f, 0.58f), new Color(0.58f, 0.40f, 0.28f), new Color(0.96f, 0.92f, 0.84f), new Color(0.78f, 0.34f, 0.20f) }, // Jovian
        new[] { new Color(0.36f, 0.56f, 0.90f), new Color(0.18f, 0.32f, 0.72f), new Color(0.62f, 0.82f, 1.00f), new Color(0.10f, 0.18f, 0.48f) }, // Ice giant
        new[] { new Color(0.90f, 0.82f, 0.60f), new Color(0.74f, 0.64f, 0.44f), new Color(0.98f, 0.95f, 0.82f), new Color(0.86f, 0.76f, 0.50f) }, // Pale gold
        new[] { new Color(0.55f, 0.70f, 0.36f), new Color(0.34f, 0.50f, 0.26f), new Color(0.82f, 0.86f, 0.56f), new Color(0.90f, 0.84f, 0.40f) }, // Toxic
        new[] { new Color(0.72f, 0.46f, 0.62f), new Color(0.44f, 0.26f, 0.46f), new Color(0.92f, 0.78f, 0.86f), new Color(0.98f, 0.62f, 0.42f) }, // Violet
    };

    private MeshRenderer surface;
    private MeshRenderer atmosphere;
    private MeshRenderer rings;
    private MaterialPropertyBlock surfaceBlock;
    private MaterialPropertyBlock atmosphereBlock;
    private MaterialPropertyBlock ringBlock;
    private Quaternion ringRotation = Quaternion.identity;

    public void Apply(OrbitParams p, bool isMoon)
    {
        surfaceBlock ??= new MaterialPropertyBlock();
        atmosphereBlock ??= new MaterialPropertyBlock();
        ringBlock ??= new MaterialPropertyBlock();

        surface = GetComponent<MeshRenderer>();
        if (surface == null) return;

        var rng = new CelestialVisuals.Rng(p.NetworkId, p.ResourceSeed);
        CelestialEnvironment env = p.EnvironmentType;
        // Moons don't get oceans or gas bands; giant-moon types become icy, temperate become barren
        if (isMoon && env == CelestialEnvironment.GAS) env = CelestialEnvironment.ICE;
        if (isMoon && env == CelestialEnvironment.TEMPERATE) env = CelestialEnvironment.ROCKY;

        Mesh sphere = GetComponent<MeshFilter>() != null ? GetComponent<MeshFilter>().sharedMesh : null;

        surface.sharedMaterial = CelestialVisuals.GetMaterial("CelestialPlanet");
        surfaceBlock.Clear();
        surfaceBlock.SetFloat(EnvId, (float)env);
        surfaceBlock.SetFloat(SeedId, rng.Next());
        surfaceBlock.SetFloat(CraterAmountId, isMoon ? 1f : 0f);
        surfaceBlock.SetFloat(SeaLevelId, 0.52f);
        surfaceBlock.SetFloat(CloudAmountId, 0f);
        surfaceBlock.SetFloat(EmissionStrengthId, 0f);

        Color atmoColor = Color.black;
        float atmoStrength = 0f;
        float ringChance = 0f;

        switch (env)
        {
            case CelestialEnvironment.TEMPERATE:
            {
                bool arid = rng.Chance(0.25f);
                surfaceBlock.SetColor(ColorAId, CelestialVisuals.Vary(new Color(0.01f, 0.05f, 0.17f), ref rng, 0.15f));
                surfaceBlock.SetColor(ColorBId, CelestialVisuals.Vary(arid ? new Color(0.46f, 0.38f, 0.20f) : new Color(0.12f, 0.30f, 0.08f), ref rng, 0.2f));
                surfaceBlock.SetColor(ColorCId, CelestialVisuals.Vary(new Color(0.44f, 0.36f, 0.24f), ref rng, 0.2f));
                surfaceBlock.SetColor(ColorDId, CelestialVisuals.Vary(new Color(0.05f, 0.30f, 0.46f), ref rng, 0.15f));
                surfaceBlock.SetFloat(SeaLevelId, arid ? rng.Range(0.40f, 0.46f) : rng.Range(0.48f, 0.56f));
                surfaceBlock.SetFloat(CloudAmountId, rng.Range(0.5f, 0.95f));
                atmoColor = new Color(0.30f, 0.55f, 1.0f);
                atmoStrength = 1.3f;
                ringChance = 0.1f;
                break;
            }
            case CelestialEnvironment.GAS:
            {
                var palette = GasPalettes[Mathf.Min((int)(rng.Next() * GasPalettes.Length), GasPalettes.Length - 1)];
                surfaceBlock.SetColor(ColorAId, CelestialVisuals.Vary(palette[0], ref rng, 0.08f));
                surfaceBlock.SetColor(ColorBId, CelestialVisuals.Vary(palette[1], ref rng, 0.08f));
                surfaceBlock.SetColor(ColorCId, CelestialVisuals.Vary(palette[2], ref rng, 0.05f));
                surfaceBlock.SetColor(ColorDId, CelestialVisuals.Vary(palette[3], ref rng, 0.08f));
                atmoColor = Color.Lerp(palette[0], Color.white, 0.35f);
                atmoStrength = 0.9f;
                ringChance = 0.65f;
                break;
            }
            case CelestialEnvironment.ICE:
            {
                surfaceBlock.SetColor(ColorAId, CelestialVisuals.Vary(new Color(0.86f, 0.92f, 1.0f), ref rng, 0.06f));
                surfaceBlock.SetColor(ColorBId, CelestialVisuals.Vary(new Color(0.24f, 0.44f, 0.70f), ref rng, 0.15f));
                surfaceBlock.SetColor(ColorCId, CelestialVisuals.Vary(new Color(0.68f, 0.82f, 0.94f), ref rng, 0.08f));
                if (!isMoon && rng.Chance(0.6f))
                {
                    atmoColor = new Color(0.60f, 0.85f, 1.0f);
                    atmoStrength = 0.5f;
                }
                ringChance = 0.3f;
                break;
            }
            case CelestialEnvironment.LAVA:
            {
                surfaceBlock.SetColor(ColorAId, CelestialVisuals.Vary(new Color(0.07f, 0.05f, 0.045f), ref rng, 0.2f));
                surfaceBlock.SetColor(ColorBId, CelestialVisuals.Vary(new Color(0.22f, 0.15f, 0.11f), ref rng, 0.2f));
                surfaceBlock.SetColor(ColorCId, new Color(1.0f, rng.Range(0.30f, 0.50f), 0.06f));
                surfaceBlock.SetFloat(EmissionStrengthId, rng.Range(2.5f, 4f));
                if (!isMoon && rng.Chance(0.8f))
                {
                    atmoColor = new Color(1.0f, 0.40f, 0.15f);
                    atmoStrength = 0.7f;
                }
                ringChance = 0.05f;
                break;
            }
            default: // ROCKY
            {
                Color baseColor = isMoon
                    ? Color.HSVToRGB(rng.Range(0.05f, 0.12f), rng.Range(0.02f, 0.10f), rng.Range(0.45f, 0.62f))
                    : Color.HSVToRGB(rng.Range(0.03f, 0.11f), rng.Range(0.18f, 0.40f), rng.Range(0.42f, 0.60f));
                surfaceBlock.SetColor(ColorAId, baseColor);
                surfaceBlock.SetColor(ColorBId, baseColor * 0.45f);
                surfaceBlock.SetColor(ColorCId, Color.Lerp(baseColor * 1.35f, Color.white * 0.75f, 0.25f));
                if (!isMoon && rng.Chance(0.25f))
                {
                    atmoColor = new Color(0.75f, 0.62f, 0.50f);
                    atmoStrength = 0.35f;
                }
                ringChance = 0.12f;
                break;
            }
        }

        // The surface shader adds its own limb tint; the shell adds the halo beyond the silhouette
        surfaceBlock.SetColor(RimColorId, atmoColor);
        surfaceBlock.SetFloat(RimStrengthId, atmoStrength * 0.7f);
        surface.SetPropertyBlock(surfaceBlock);

        // Atmosphere shell
        if (atmoStrength > 0f && sphere != null)
        {
            atmosphere = CelestialVisuals.EnsureChild(transform, "Atmosphere", sphere, CelestialVisuals.GetMaterial("CelestialAtmosphere"));
            atmosphere.transform.localPosition = Vector3.zero;
            atmosphere.transform.localRotation = Quaternion.identity;
            atmosphere.transform.localScale = Vector3.one * AtmosphereScale;
            atmosphereBlock.Clear();
            atmosphereBlock.SetColor(AtmoColorId, atmoColor);
            atmosphereBlock.SetFloat(AtmoStrengthId, atmoStrength);
            atmosphere.SetPropertyBlock(atmosphereBlock);
            atmosphere.enabled = true;
        }
        else if (atmosphere != null)
        {
            atmosphere.enabled = false;
        }

        // Rings, planets only
        if (!isMoon && rng.Chance(ringChance))
        {
            rings = CelestialVisuals.EnsureChild(transform, "Rings", CelestialVisuals.RingMesh, CelestialVisuals.GetMaterial("CelestialRing"));
            rings.transform.localPosition = Vector3.zero;
            rings.transform.localScale = Vector3.one;

            bool icy = env == CelestialEnvironment.ICE || rng.Chance(0.5f);
            Color ringA = icy ? new Color(0.90f, 0.88f, 0.82f) : new Color(0.72f, 0.62f, 0.50f);
            Color ringB = icy ? new Color(0.58f, 0.54f, 0.48f) : new Color(0.40f, 0.33f, 0.26f);
            ringBlock.Clear();
            ringBlock.SetFloat(SeedId, rng.Next());
            ringBlock.SetColor(RingColorAId, CelestialVisuals.Vary(ringA, ref rng, 0.08f));
            ringBlock.SetColor(RingColorBId, CelestialVisuals.Vary(ringB, ref rng, 0.08f));
            ringBlock.SetFloat(OpacityId, rng.Range(0.55f, 0.9f));
            rings.SetPropertyBlock(ringBlock);

            // Fixed in world space: the planet object spins with its orbit angle, the ring plane must not
            ringRotation = Quaternion.Euler(rng.Range(-25f, 25f), rng.Range(0f, 360f), rng.Range(-25f, 25f));
            rings.transform.rotation = ringRotation;
            rings.enabled = true;
        }
        else if (rings != null)
        {
            rings.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (rings != null && rings.enabled) rings.transform.rotation = ringRotation;
    }
}
