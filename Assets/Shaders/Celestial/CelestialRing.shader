// Planetary ring on a flat annulus mesh (uv.x = 0 inner edge .. 1 outer edge).
// Procedural bands and gaps, lit from the star on both faces, with the planet's shadow cast on it.
// The parent planet's sphere is derived from this object's transform (same origin, mesh radius 0.5).
Shader "TheRift/Celestial/Ring"
{
    Properties
    {
        _Seed ("Seed", Float) = 0
        _RingColorA ("Color A", Color) = (0.85, 0.8, 0.7, 1)
        _RingColorB ("Color B", Color) = (0.55, 0.48, 0.4, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _Ambient ("Ambient", Range(0, 0.5)) = 0.06
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardRing"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Shaders/Celestial/CelestialNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Seed;
                float4 _RingColorA;
                float4 _RingColorB;
                float _Opacity;
                float _Ambient;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float r = input.uv.x;
                float s = _Seed * 23.0;

                // Layered 1D bands along the radius
                float fine = CelNoise(float3(r * 140.0, s, 0.0));
                float mid = CelNoise(float3(r * 38.0, s + 5.0, 1.0));
                float coarse = CelNoise(float3(r * 9.0, s + 11.0, 2.0));
                float density = saturate(coarse * 0.55 + mid * 0.35 + fine * 0.3);

                // One major division at a seeded radius
                float gapPos = 0.45 + frac(_Seed * 5.17) * 0.3;
                density *= smoothstep(0.015, 0.035, abs(r - gapPos));

                // Soft inner and outer edges
                density *= smoothstep(0.0, 0.08, r) * smoothstep(1.0, 0.85, r);

                float3 albedo = lerp(_RingColorB.rgb, _RingColorA.rgb, saturate(mid * 0.7 + fine * 0.5));

                float3 N = normalize(input.normalWS);
                float3 L = CelestialLightDir(input.positionWS);
                float lit = abs(dot(N, L)) * 0.75 + 0.25;

                // Planet shadow: does the ray toward the star hit the planet sphere?
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float4x4 m = GetObjectToWorldMatrix();
                float radius = 0.5 * length(float3(m[0][0], m[1][0], m[2][0]));
                float3 oc = input.positionWS - center;
                float b = dot(oc, L);
                float c = dot(oc, oc) - radius * radius;
                float disc = b * b - c;
                float inShadow = (b < 0.0 && c > 0.0) ? smoothstep(0.0, radius * radius * 0.05, disc) : 0.0;
                lit *= 1.0 - inShadow * 0.9;

                float3 color = albedo * (lit * CelestialStarColor() + _Ambient);
                return half4(color, density * _Opacity);
            }
            ENDHLSL
        }
    }
}
