// Additive atmosphere shell drawn on a sphere slightly larger than the planet.
// Glow peaks just outside the planet's limb, fades to nothing at the shell edge,
// is brighter on the day side and gets a forward-scattering halo when backlit.
Shader "TheRift/Celestial/Atmosphere"
{
    Properties
    {
        [HDR] _AtmoColor ("Atmosphere Color", Color) = (0.3, 0.55, 1, 1)
        _AtmoStrength ("Strength", Range(0, 4)) = 1.2
        _AtmoPower ("Falloff Power", Range(1, 8)) = 3
        _NightLevel ("Night Side Level", Range(0, 1)) = 0.01
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardAtmosphere"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Shaders/Celestial/CelestialNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _AtmoColor;
                float _AtmoStrength;
                float _AtmoPower;
                float _NightLevel;
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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 L = CelestialLightDir(input.positionWS);

                float rim = 1.0 - saturate(dot(N, V));
                float glow = pow(rim, _AtmoPower);
                // Fade out at the very edge of the shell so it reads as a soft halo, not a hard disc
                glow *= 1.0 - smoothstep(0.82, 1.0, rim);
                // Faint haze over the planet disc itself
                glow += 0.06 * (1.0 - rim);

                // The scattering glow wraps a little past the terminator, then the night side goes dark
                float day = smoothstep(-0.25, 0.35, dot(N, L));
                float lit = lerp(_NightLevel, 1.0, day);
                // Backlit planets get a bright ring when the star is behind them
                float forward = pow(saturate(dot(-V, L)), 6.0) * rim * 1.5;

                float3 color = _AtmoColor.rgb * CelestialStarColor() * (glow * lit + forward) * _AtmoStrength;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
