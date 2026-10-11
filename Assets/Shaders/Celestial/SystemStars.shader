// Neighbouring star systems, drawn by DynamicSkyboxManager centred on the camera.
// Each star's four vertices carry its position relative to the current system (positionOS), its
// orbital drift per second (uv1) and its quad corner (uv0). The stars are treated as infinitely far
// away: only their direction from the current system matters (no parallax from moving around inside
// it), and orbital drift is extrapolated so the sky turns smoothly. The vertex shader builds a
// camera-facing quad pushed to the far plane: everything in the scene covers it.
// Vertex colour = star tint, alpha = brightness.
Shader "TheRift/Space/SystemStars"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 10)) = 2.5
        _Core ("Core Sharpness", Range(4, 60)) = 22
        _Halo ("Halo Strength", Range(0, 1)) = 0.25
        _SkyRadius ("Sky Radius", Float) = 90000
        _StarSize ("Star Size", Float) = 500
        _Elapsed ("Seconds Since Positions Were Built", Float) = 0
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
                float _SkyRadius;
                float _StarSize;
                float _Elapsed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 corner : TEXCOORD0;
                float3 drift : TEXCOORD1;
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

                float3 relative = input.positionOS.xyz + input.drift * _Elapsed;
                float len = length(relative);
                float3 dir = len > 1e-3 ? relative / len : float3(0, 0, 1);

                // Object space is centred on the camera (DrawMesh at the camera position)
                float3 centerVS = TransformWorldToView(TransformObjectToWorld(dir * _SkyRadius));
                float size = _StarSize * lerp(0.7, 1.4, saturate(input.color.a / 1.6));
                float4 positionCS = TransformWViewToHClip(centerVS + float3(input.corner * size, 0.0));
                #if UNITY_REVERSED_Z
                    positionCS.z = positionCS.w * 1e-6;
                #else
                    positionCS.z = positionCS.w * (1.0 - 1e-6);
                #endif

                output.positionCS = positionCS;
                output.uv = input.corner;
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
