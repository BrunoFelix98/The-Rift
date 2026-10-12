// Procedural deep-space backdrop: a faint galactic glow with dust lanes and dim nebulae.
// It deliberately has no stars of its own; the only stars are the real neighbouring systems,
// drawn on top by DynamicSkyboxManager with the SystemStars shader.
Shader "TheRift/Space/Skybox"
{
    Properties
    {
        _Seed ("Seed", Float) = 0.37
        [HDR] _BandColor ("Galactic Band Color", Color) = (0.16, 0.14, 0.12, 1)
        _BandStrength ("Galactic Band Strength", Range(0, 2)) = 0.25
        [HDR] _NebulaColorA ("Nebula Color A", Color) = (0.18, 0.05, 0.22, 1)
        [HDR] _NebulaColorB ("Nebula Color B", Color) = (0.03, 0.12, 0.22, 1)
        _NebulaStrength ("Nebula Strength", Range(0, 2)) = 0.25
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

            half4 frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
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

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
