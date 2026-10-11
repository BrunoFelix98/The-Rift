// Procedural deep-space skybox: three layers of background stars with colour temperatures,
// a galactic band with dust lanes, and faint nebulae. Stars are sized from the pixel footprint so
// they stay crisp points at any resolution. The real neighbouring systems are drawn on top by
// DynamicSkyboxManager with the SystemStars shader.
Shader "TheRift/Space/Skybox"
{
    Properties
    {
        _Seed ("Seed", Float) = 0.37
        _StarDensity ("Star Density", Range(0, 1)) = 0.6
        _StarBrightness ("Star Brightness", Range(0, 4)) = 1.2
        [HDR] _BandColor ("Galactic Band Color", Color) = (0.16, 0.14, 0.12, 1)
        _BandStrength ("Galactic Band Strength", Range(0, 2)) = 0.7
        [HDR] _NebulaColorA ("Nebula Color A", Color) = (0.18, 0.05, 0.22, 1)
        [HDR] _NebulaColorB ("Nebula Color B", Color) = (0.03, 0.12, 0.22, 1)
        _NebulaStrength ("Nebula Strength", Range(0, 2)) = 0.6
        _BaseColor ("Base Color", Color) = (0.002, 0.0025, 0.005, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Shaders/Celestial/CelestialNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Seed;
                float _StarDensity;
                float _StarBrightness;
                float4 _BandColor;
                float _BandStrength;
                float4 _NebulaColorA;
                float4 _NebulaColorB;
                float _NebulaStrength;
                float4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float3 StarTint(float t)
            {
                // Blue-white through yellow to orange, weighted toward white
                float3 blue = float3(0.70, 0.80, 1.00);
                float3 white = float3(1.00, 0.97, 0.92);
                float3 orange = float3(1.00, 0.72, 0.45);
                return t < 0.5 ? lerp(blue, white, t * 2.0) : lerp(white, orange, (t - 0.5) * 2.0);
            }

            // One candidate star per grid cell; the star sits inside the cell so it is never cut off
            float3 StarLayer(float3 d, float scale, float density, float brightness, float pixel, float seed)
            {
                float3 p = d * scale;
                float3 cell = floor(p);
                float3 h = CelHash33(cell + seed);
                float exists = step(1.0 - density, CelHash13(cell * 1.7 + seed * 3.1));

                float3 starDir = normalize((cell + 0.2 + h * 0.6) / scale);
                float angle = length(cross(d, starDir));
                // At least about a pixel wide so stars don't shimmer
                float radius = pixel * (0.7 + h.x * 0.9);
                float shape = exp(-(angle * angle) / (radius * radius));

                // Few bright stars, many faint ones
                float magnitude = 0.15 + pow(h.y, 8.0) * 3.0;
                return StarTint(h.z) * shape * magnitude * brightness * exists;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float pixel = max(length(fwidth(d)), 1e-5);
                float3 seedOffset = float3(_Seed * 41.0, _Seed * 17.0, _Seed * 29.0);

                float3 color = _BaseColor.rgb;

                // Galactic band around a tilted great circle, broken up by dark dust lanes
                float3 bandNormal = normalize(float3(0.25, 1.0, 0.35));
                float bandLat = dot(d, bandNormal);
                float band = exp(-bandLat * bandLat * 16.0);
                float bandCore = exp(-bandLat * bandLat * 70.0);
                float bandNoise = CelFbm(d * 6.0 + seedOffset, 5);
                float dust = smoothstep(0.48, 0.72, CelFbm(CelWarp(d * 9.0 + seedOffset * 2.0, 0.8), 4));
                float bandLight = band * (0.35 + 0.9 * bandNoise) + bandCore * 0.6;
                color += _BandColor.rgb * bandLight * (1.0 - dust * 0.75 * band) * _BandStrength;

                // Nebulae: warped clouds, two hues, brighter near the band
                float neb = CelFbm(CelWarp(d * 2.2 + seedOffset, 1.3), 5);
                float nebMask = smoothstep(0.52, 0.85, neb);
                float hue = CelFbm(d * 1.4 + seedOffset + 7.0, 3);
                float3 nebColor = lerp(_NebulaColorA.rgb, _NebulaColorB.rgb, hue);
                color += nebColor * nebMask * nebMask * _NebulaStrength * (0.6 + band * 0.8);

                // Stars: sparse bright, medium, and a dense faint layer that follows the band
                float density = _StarDensity;
                color += StarLayer(d, 70.0, 0.20 * density, 1.0, pixel, _Seed * 13.0) * _StarBrightness;
                color += StarLayer(d, 160.0, 0.28 * density, 0.55, pixel, _Seed * 31.0 + 5.0) * _StarBrightness;
                color += StarLayer(d, 340.0, (0.18 + band * 0.5) * density, 0.35, pixel, _Seed * 59.0 + 11.0) * _StarBrightness;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
