// Neighbouring star systems drawn by DynamicSkyboxManager as camera-centred quads. The quads are
// pushed to the far plane in the vertex shader, so anything in the scene (planets, stations, ships)
// always covers them, and they are drawn after the skybox so it can't paint over them.
// Vertex colour = star tint, alpha = brightness.
Shader "TheRift/Space/SystemStars"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 10)) = 2.5
        _Core ("Core Sharpness", Range(4, 60)) = 22
        _Halo ("Halo Strength", Range(0, 1)) = 0.25
    }

    SubShader
    {
        // First transparent slot: after the skybox, before atmospheres and rings
        Tags { "Queue" = "Transparent-499" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SystemStars"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Core;
                float _Halo;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                #if UNITY_REVERSED_Z
                    positionCS.z = positionCS.w * 1e-6;
                #else
                    positionCS.z = positionCS.w * (1.0 - 1e-6);
                #endif
                output.positionCS = positionCS;
                output.uv = input.uv * 2.0 - 1.0;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float r = length(input.uv);
                float core = exp(-r * r * _Core);
                float halo = exp(-r * 5.0) * _Halo;
                float fade = saturate((1.0 - r) * 3.0);
                float3 color = input.color.rgb * (core + halo) * fade * input.color.a * _Intensity;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
