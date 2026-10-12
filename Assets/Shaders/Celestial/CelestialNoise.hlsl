#ifndef CELESTIAL_NOISE_INCLUDED
#define CELESTIAL_NOISE_INCLUDED

// Texture-free noise for celestial bodies. Inputs are object-space unit-sphere positions times a
// frequency plus a small seed offset, so values stay small and precise at any world scale.

// Set globally by StarData: system star position and its light color.
float4 _CelestialStarPos;
float4 _CelestialStarColor;

float3 CelestialLightDir(float3 positionWS)
{
    float3 toStar = _CelestialStarPos.xyz - positionWS;
    float len = length(toStar);
    return len > 1e-3 ? toStar / len : float3(0, 1, 0);
}

float3 CelestialStarColor()
{
    // Unset global (all zero) falls back to neutral white light
    float3 c = _CelestialStarColor.rgb;
    return dot(c, c) > 1e-4 ? c : float3(1, 1, 1);
}

// Hash without sine (Dave Hoskins), stable across GPUs for small inputs
float CelHash13(float3 p3)
{
    p3 = frac(p3 * 0.1031);
    p3 += dot(p3, p3.zyx + 31.32);
    return frac((p3.x + p3.y) * p3.z);
}

float3 CelHash33(float3 p3)
{
    p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return frac((p3.xxy + p3.yxx) * p3.zyx);
}

// Value noise in [0, 1]
float CelNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);

    float n000 = CelHash13(i + float3(0, 0, 0));
    float n100 = CelHash13(i + float3(1, 0, 0));
    float n010 = CelHash13(i + float3(0, 1, 0));
    float n110 = CelHash13(i + float3(1, 1, 0));
    float n001 = CelHash13(i + float3(0, 0, 1));
    float n101 = CelHash13(i + float3(1, 0, 1));
    float n011 = CelHash13(i + float3(0, 1, 1));
    float n111 = CelHash13(i + float3(1, 1, 1));

    float nx00 = lerp(n000, n100, u.x);
    float nx10 = lerp(n010, n110, u.x);
    float nx01 = lerp(n001, n101, u.x);
    float nx11 = lerp(n011, n111, u.x);
    float nxy0 = lerp(nx00, nx10, u.y);
    float nxy1 = lerp(nx01, nx11, u.y);
    return lerp(nxy0, nxy1, u.z);
}

// Fractal noise in roughly [0, 1]
float CelFbm(float3 p, int octaves)
{
    float sum = 0.0;
    float amp = 0.5;
    float norm = 0.0;
    for (int o = 0; o < octaves; o++)
    {
        sum += CelNoise(p) * amp;
        norm += amp;
        p = p * 2.03 + float3(17.1, 9.7, 3.3);
        amp *= 0.5;
    }
    return sum / max(norm, 1e-4);
}

// CelRidged noise in [0, 1]; thin bright ridges where the base noise crosses 0.5
float CelRidged(float3 p, int octaves)
{
    float sum = 0.0;
    float amp = 0.5;
    float norm = 0.0;
    for (int o = 0; o < octaves; o++)
    {
        float n = 1.0 - abs(CelNoise(p) * 2.0 - 1.0);
        sum += n * n * amp;
        norm += amp;
        p = p * 2.07 + float3(5.3, 11.9, 7.1);
        amp *= 0.5;
    }
    return sum / max(norm, 1e-4);
}

// Distance to the nearest random feature point (cellular noise F1), roughly [0, 1]
float CelVoronoi(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float best = 8.0;
    for (int z = -1; z <= 1; z++)
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++)
    {
        float3 cell = float3(x, y, z);
        float3 d = cell + CelHash33(i + cell) - f;
        best = min(best, dot(d, d));
    }
    return sqrt(best);
}

// Cellular noise returning (F1, F2); F2 - F1 is small along cell borders (plates, cracks, cells)
float2 CelVoronoi2(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float f1 = 8.0;
    float f2 = 8.0;
    for (int z = -1; z <= 1; z++)
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++)
    {
        float3 cell = float3(x, y, z);
        float3 d = cell + CelHash33(i + cell) - f;
        float dd = dot(d, d);
        if (dd < f1) { f2 = f1; f1 = dd; }
        else if (dd < f2) { f2 = dd; }
    }
    return float2(sqrt(f1), sqrt(f2));
}

// Domain warp: offsets p by a smooth noise field, gives swirling, flowing shapes
float3 CelWarp(float3 p, float strength)
{
    float3 q = float3(CelFbm(p + float3(0.0, 0.0, 0.0), 3),
                      CelFbm(p + float3(5.2, 1.3, 2.8), 3),
                      CelFbm(p + float3(1.7, 9.2, 4.1), 3));
    return p + (q - 0.5) * strength;
}

// Crater height: bowl below 0, raised rim above 0, flat (0) outside. Radius and presence come from
// the nearest feature point, so craters are whole circles rather than cut at cell borders.
float CelCraterHeight(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float best = 8.0;
    float id = 0.0;
    for (int z = -1; z <= 1; z++)
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++)
    {
        float3 cell = float3(x, y, z);
        float3 d = cell + CelHash33(i + cell) - f;
        float dd = dot(d, d);
        if (dd < best) { best = dd; id = CelHash13(i + cell + 0.37); }
    }

    if (id < 0.4) return 0.0;
    float radius = lerp(0.15, 0.45, frac(id * 7.13));
    float r = sqrt(best) / radius;
    float bowl = r < 1.0 ? (r * r - 1.0) * 0.8 : 0.0;
    float rim = exp(-((r - 1.0) * 4.0) * ((r - 1.0) * 4.0)) * 0.45;
    return bowl + rim;
}

// Crater shading multiplier: dark bowls with bright raised rims
float CelCraters(float3 p)
{
    float d = CelVoronoi(p);
    float bowl = smoothstep(0.05, 0.38, d);
    float rim = smoothstep(0.30, 0.38, d) * smoothstep(0.50, 0.40, d);
    return lerp(0.62, 1.0, bowl) + rim * 0.22;
}

#endif
