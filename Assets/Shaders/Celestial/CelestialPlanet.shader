// Procedural planet / moon surface. One shader for every environment, selected by _Env so a
// MaterialPropertyBlock can switch looks per pooled instance:
// 0 ROCKY, 1 ICE, 2 GAS, 3 LAVA, 4 TEMPERATE (matches CelestialEnvironment).
Shader "TheRift/Celestial/Planet"
{
    Properties
    {
        _Env ("Environment", Float) = 0
        _Seed ("Seed", Float) = 0
        _ColorA ("Color A", Color) = (0.5, 0.45, 0.4, 1)
        _ColorB ("Color B", Color) = (0.3, 0.28, 0.25, 1)
        _ColorC ("Color C", Color) = (0.7, 0.65, 0.6, 1)
        _ColorD ("Color D", Color) = (0.1, 0.3, 0.5, 1)
        _SeaLevel ("Sea Level", Range(0, 1)) = 0.52
        _CloudAmount ("Cloud Amount", Range(0, 1)) = 0.7
        _CraterAmount ("Crater Amount", Range(0, 1)) = 0
        [HDR] _RimColor ("Atmosphere Rim Color", Color) = (0.3, 0.55, 1, 1)
        _RimStrength ("Atmosphere Rim Strength", Range(0, 4)) = 0.8
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 2
        _Ambient ("Ambient", Range(0, 0.3)) = 0.035
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Shaders/Celestial/CelestialNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Env;
                float _Seed;
                float4 _ColorA;
                float4 _ColorB;
                float4 _ColorC;
                float4 _ColorD;
                float _SeaLevel;
                float _CloudAmount;
                float _CraterAmount;
                float4 _RimColor;
                float _RimStrength;
                float _EmissionStrength;
                float _Ambient;
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

            // Returns albedo; writes emission and ocean mask (for specular) through out params
            float3 SurfaceColor(float3 p, float3 sp, out float3 emission, out float wetness, out float clouds)
            {
                emission = 0;
                wetness = 0;
                clouds = 0;
                int env = (int)round(_Env);
                float t = _Time.y;

                if (env == 2) // GAS: latitude bands warped by turbulence, plus a storm oval
                {
                    float turb = CelFbm(sp * float3(1.2, 3.5, 1.2) + float3(t * 0.004, 0, 0), 5);
                    float band = p.y * (5.0 + frac(_Seed * 7.3) * 4.0) + turb * 1.8 + _Seed * 13.0;
                    float b1 = sin(band * 3.14159) * 0.5 + 0.5;
                    float b2 = sin(band * 7.37 + turb * 3.0) * 0.5 + 0.5;
                    float3 col = lerp(_ColorA.rgb, _ColorB.rgb, b1);
                    col = lerp(col, _ColorC.rgb, b2 * 0.45);

                    float3 stormCenter = normalize(CelHash33(float3(_Seed * 91.0, 3.1, 7.7)) * 2.0 - 1.0);
                    stormCenter.y = (frac(_Seed * 3.7) - 0.5) * 0.9;
                    stormCenter = normalize(stormCenter);
                    float3 dv = (p - stormCenter) * float3(1.0, 2.4, 1.0);
                    float swirl = CelFbm(dv * 6.0 + sp, 3);
                    float storm = smoothstep(0.32, 0.05, length(dv) + (swirl - 0.5) * 0.12);
                    col = lerp(col, _ColorD.rgb, storm * 0.85);
                    return col * (0.85 + 0.3 * CelFbm(sp * 8.0, 3));
                }

                if (env == 4) // TEMPERATE: oceans, continents, ice caps, clouds
                {
                    float continent = CelFbm(sp * 1.4, 6);
                    float detail = CelFbm(sp * 6.0, 4);
                    float elevation = continent + (detail - 0.5) * 0.08;
                    float land = smoothstep(_SeaLevel - 0.008, _SeaLevel + 0.008, elevation);

                    float depth = saturate((_SeaLevel - elevation) * 8.0);
                    float3 ocean = lerp(_ColorD.rgb, _ColorA.rgb, depth);

                    float height = saturate((elevation - _SeaLevel) * 5.0);
                    float3 ground = lerp(_ColorB.rgb, _ColorC.rgb, smoothstep(0.15, 0.7, height));
                    ground = lerp(ground, float3(0.92, 0.92, 0.95), smoothstep(0.75, 0.95, height));
                    // Drier toward the equator
                    ground = lerp(ground, _ColorC.rgb * 1.1, smoothstep(0.35, 0.0, abs(p.y)) * 0.35 * (1.0 - height));

                    float3 col = lerp(ocean, ground, land);
                    float cap = smoothstep(0.80, 0.88, abs(p.y) + (detail - 0.5) * 0.12);
                    col = lerp(col, float3(0.93, 0.95, 1.0), cap);
                    wetness = (1.0 - land) * (1.0 - cap);

                    float c = CelFbm(sp * 2.6 + float3(t * 0.006, t * 0.002, 0), 5);
                    clouds = smoothstep(0.48, 0.72, c) * _CloudAmount;
                    return col;
                }

                if (env == 1) // ICE: bright sheets with dark blue fracture lines
                {
                    float n = CelFbm(sp * 2.0, 5);
                    float cracks = smoothstep(0.80, 0.93, CelRidged(sp * 3.0, 4));
                    float3 col = lerp(_ColorA.rgb, _ColorC.rgb, n);
                    col = lerp(col, _ColorB.rgb, cracks * 0.8);
                    if (_CraterAmount > 0.0) col *= lerp(1.0, CelCraters(sp * 3.0), _CraterAmount);
                    return col;
                }

                if (env == 3) // LAVA: cracked crust with glowing seams
                {
                    float crust = CelFbm(sp * 2.5, 5);
                    float seams = CelRidged(sp * 2.2, 5);
                    float glow = smoothstep(0.80, 0.95, seams);
                    float pulse = 0.85 + 0.15 * sin(t * 0.8 + seams * 20.0);
                    float3 col = lerp(_ColorA.rgb, _ColorB.rgb, crust);
                    emission = _ColorC.rgb * glow * pulse * _EmissionStrength;
                    if (_CraterAmount > 0.0) col *= lerp(1.0, CelCraters(sp * 3.0), _CraterAmount);
                    return col * (1.0 - glow * 0.7);
                }

                // ROCKY (default): mottled regolith, dark maria, craters
                {
                    float n = CelFbm(sp * 2.2, 6);
                    float maria = smoothstep(0.52, 0.62, CelFbm(sp * 1.1 + 4.0, 4));
                    float3 col = lerp(_ColorA.rgb, _ColorC.rgb, n);
                    col = lerp(col, _ColorB.rgb, maria * 0.7);
                    float craterMix = max(_CraterAmount, 0.35);
                    col *= lerp(1.0, CelCraters(sp * 2.5) * CelCraters(sp * 7.0 + 3.0), craterMix);
                    return col;
                }
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 p = normalize(input.positionOS);
                // Seed offset keeps every body's pattern unique while inputs stay small
                float3 sp = p * 2.0 + float3(_Seed * 37.0, _Seed * 53.0, _Seed * 71.0);

                float3 N = normalize(input.normalWS);
                float3 L = CelestialLightDir(input.positionWS);
                float3 V = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 lightColor = CelestialStarColor();

                float3 emission;
                float wetness;
                float clouds;
                float3 albedo = SurfaceColor(p, sp, emission, wetness, clouds);

                float ndl = dot(N, L);
                // Soft terminator instead of a hard Lambert cut
                float diffuse = smoothstep(-0.12, 0.55, ndl) * saturate(ndl * 0.5 + 0.6);

                float3 color = albedo * (diffuse * lightColor + _Ambient);

                // Sun glint on water
                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N, H)), 60.0) * wetness * step(0.0, ndl);
                color += spec * lightColor * 0.6;

                // Cloud layer on top, lit the same way
                color = lerp(color, (diffuse * lightColor + _Ambient) * 0.95, clouds);

                color += emission;

                // Atmospheric scattering at the limb, brighter on the day side
                float rim = pow(1.0 - saturate(dot(N, V)), 3.0);
                float dayRim = saturate(ndl * 0.8 + 0.35);
                color += _RimColor.rgb * rim * _RimStrength * dayRim;

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // Lets the planet occlude itself and other geometry in depth-based effects
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
                float _Env;
                float _Seed;
                float4 _ColorA;
                float4 _ColorB;
                float4 _ColorC;
                float4 _ColorD;
                float _SeaLevel;
                float _CloudAmount;
                float _CraterAmount;
                float4 _RimColor;
                float _RimStrength;
                float _EmissionStrength;
                float _Ambient;
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
