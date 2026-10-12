// Animated star surface: boiling granulation, drifting sunspots and limb darkening.
// Unlit (it is the light source); HDR output so bloom picks it up when post-processing is on.
Shader "TheRift/Celestial/Star"
{
    Properties
    {
        _Seed ("Seed", Float) = 0
        [HDR] _CoreColor ("Core Color", Color) = (1, 0.95, 0.8, 1)
        [HDR] _EdgeColor ("Limb Color", Color) = (1, 0.6, 0.25, 1)
        _Intensity ("Intensity", Range(0, 10)) = 2.5
        _Speed ("Boil Speed", Range(0, 2)) = 0.4
        _SpotAmount ("Sunspot Amount", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardStar"
            // ForwardOnly: the renderer is in Deferred mode, where plain UniversalForward opaques
            // never reach the depth buffer and everything drawn later paints over them
            Tags { "LightMode" = "UniversalForwardOnly" }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Shaders/Celestial/CelestialNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Seed;
                float4 _CoreColor;
                float4 _EdgeColor;
                float _Intensity;
                float _Speed;
                float _SpotAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 p = normalize(input.positionOS);
                float t = _Time.y * _Speed;
                float3 sp = p + float3(_Seed * 29.0, _Seed * 41.0, _Seed * 17.0);

                // Convection cells: cellular noise distorted by slow flow
                float3 flow = float3(CelFbm(sp * 3.0 + t * 0.05, 3), CelFbm(sp * 3.0 + 9.0 - t * 0.04, 3), 0.0) - 0.5;
                float cells = CelVoronoi(sp * 14.0 + flow * 1.5 + float3(0, t * 0.08, 0));
                float granulation = 1.0 - smoothstep(0.1, 0.75, cells);
                float boil = CelFbm(sp * 6.0 + float3(t * 0.06, -t * 0.03, t * 0.04), 4);

                float spots = smoothstep(0.66, 0.74, CelFbm(sp * 2.2 + float3(t * 0.01, 0, 0), 4)) * _SpotAmount;

                float3 N = normalize(input.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - input.positionWS);
                float mu = saturate(dot(N, V));
                float limb = pow(mu, 0.5);

                float3 baseColor = lerp(_EdgeColor.rgb, _CoreColor.rgb, limb);
                float brightness = (0.7 + 0.35 * granulation + 0.25 * boil) * (1.0 - spots * 0.75);
                // Limb darkening: the disc edge is dimmer and redder
                brightness *= lerp(0.45, 1.0, limb);

                return half4(baseColor * brightness * _Intensity, 1.0);
            }
            ENDHLSL
        }

        // Depth for the prepass / depth texture; deferred and forward renderers both use it
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Seed;
                float4 _CoreColor;
                float4 _EdgeColor;
                float _Intensity;
                float _Speed;
                float _SpotAmount;
            CBUFFER_END

            float4 vertDepth(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half4 fragDepth() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
