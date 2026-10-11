// Camera-facing corona glow for stars, drawn on a quad (vertices in -1..1) parented to the star.
// The quad is expanded in view space around the star's center; the star sphere in front of the
// center hides the inner part through the depth test, leaving the halo and streamers around the disc.
Shader "TheRift/Celestial/Corona"
{
    Properties
    {
        _Seed ("Seed", Float) = 0
        [HDR] _CoronaColor ("Corona Color", Color) = (1, 0.7, 0.35, 1)
        _Size ("Size (star radii)", Range(1.2, 8)) = 4
        _Falloff ("Falloff", Range(0.5, 8)) = 2.2
        _Intensity ("Intensity", Range(0, 10)) = 1.6
        _Speed ("Streamer Speed", Range(0, 2)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "DisableBatching" = "True" }

        Pass
        {
            Name "ForwardCorona"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Shaders/Celestial/CelestialNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Seed;
                float4 _CoronaColor;
                float _Size;
                float _Falloff;
                float _Intensity;
                float _Speed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 local : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float4x4 m = GetObjectToWorldMatrix();
                float3 centerWS = float3(m[0][3], m[1][3], m[2][3]);
                // Star mesh radius is 0.5 in object space; the quad inherits the star's scale
                float starRadius = 0.5 * length(float3(m[0][0], m[1][0], m[2][0]));

                float3 centerVS = TransformWorldToView(centerWS);
                float3 positionVS = centerVS + float3(input.positionOS.xy * _Size * starRadius, 0.0);
                output.positionCS = TransformWViewToHClip(positionVS);
                output.local = input.positionOS.xy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Distance from the star center in star radii
                float rs = length(input.local) * _Size;
                float glow = exp(-max(rs - 1.0, 0.0) * _Falloff);

                // Streamers: angular noise sampled on a circle so there is no atan2 seam
                float2 dir = normalize(input.local + 1e-5);
                float t = _Time.y * _Speed;
                float rays = CelFbm(float3(dir * 3.0, t * 0.15 + _Seed * 10.0), 4);
                float rays2 = CelNoise(float3(dir * 9.0, t * 0.4 + _Seed * 3.0));
                float streamers = lerp(0.6, 1.6, rays) * lerp(0.85, 1.15, rays2);
                glow *= lerp(1.0, streamers, saturate((rs - 1.0) * 1.5));

                // Fade before the quad edge
                glow *= saturate((1.0 - length(input.local)) * 4.0);

                return half4(_CoronaColor.rgb * glow * _Intensity, 1.0);
            }
            ENDHLSL
        }
    }
}
