// Procedural planet / moon surface. One shader for every environment, selected by _Env so a
// MaterialPropertyBlock can switch looks per pooled instance:
// 0 ROCKY, 1 ICE, 2 GAS, 3 LAVA, 4 TEMPERATE (matches CelestialEnvironment).
// Each environment produces albedo, a height field (turned into relief lighting with screen-space
// derivatives), emission, specular mask, clouds and night lights. Noise octaves fade out once they
// get smaller than a pixel, so distant planets stay calm and close ones gain detail.
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
        _CityLights ("City Lights", Range(0, 1)) = 0
        _BumpStrength ("Relief Strength", Range(0, 0.05)) = 0.012
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            // ForwardOnly: the renderer is in Deferred mode, where plain UniversalForward opaques
            // never reach the depth buffer and everything drawn later paints over them
            Tags { "LightMode" = "UniversalForwardOnly" }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

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
                float _CityLights;
                float _BumpStrength;
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

            struct Surface
            {
                float3 albedo;
                float height;       // roughly [-1, 1], scaled into relief by _BumpStrength
                float3 emission;
                float spec;         // specular mask
                float specPower;
                float clouds;       // cloud cover [0, 1]
                float cloudShadow;  // cloud cover offset toward the star, darkens the ground
                float3 nightLights; // only visible on the night side
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

            // Fractal noise whose octaves fade to their mean once smaller than the pixel footprint
            float FbmLod(float3 p, int octaves, float footprint)
            {
                float sum = 0.0;
                float amp = 0.5;
                float norm = 0.0;
                float freq = 1.0;
                for (int o = 0; o < octaves; o++)
                {
                    float fade = saturate(1.5 - footprint * freq * 3.0);
                    sum += lerp(0.5, CelNoise(p), fade) * amp;
                    norm += amp;
                    p = p * 2.03 + float3(17.1, 9.7, 3.3);
                    freq *= 2.03;
                    amp *= 0.5;
                }
                return sum / max(norm, 1e-4);
            }

            float RidgedLod(float3 p, int octaves, float footprint)
            {
                float sum = 0.0;
                float amp = 0.5;
                float norm = 0.0;
                float freq = 1.0;
                for (int o = 0; o < octaves; o++)
                {
                    float fade = saturate(1.5 - footprint * freq * 3.0);
                    float n = 1.0 - abs(CelNoise(p) * 2.0 - 1.0);
                    sum += lerp(0.5, n * n, fade) * amp;
                    norm += amp;
                    p = p * 2.07 + float3(5.3, 11.9, 7.1);
                    freq *= 2.07;
                    amp *= 0.5;
                }
                return sum / max(norm, 1e-4);
            }

            // Multi-scale craters, each scale fading out when too small to see
            float Craters(float3 sp, float footprint, float amount)
            {
                float h = 0.0;
                h += CelCraterHeight(sp * 2.5) * 1.0;
                h += CelCraterHeight(sp * 6.0 + 11.0) * 0.6 * saturate(1.5 - footprint * 6.0 * 3.0);
                h += CelCraterHeight(sp * 15.0 + 23.0) * 0.35 * saturate(1.5 - footprint * 15.0 * 3.0);
                h += CelCraterHeight(sp * 38.0 + 41.0) * 0.2 * saturate(1.5 - footprint * 38.0 * 3.0);
                return h * amount;
            }

            // Rotates p around the planet's axis; used for gas-band differential rotation
            float3 RotateY(float3 p, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);
            }

            Surface Rocky(float3 p, float3 sp, float fw)
            {
                Surface s = (Surface)0;
                s.specPower = 16.0;

                float base = FbmLod(sp * 1.2, 6, fw * 1.2);
                float highlands = smoothstep(0.45, 0.62, base);
                float mountains = RidgedLod(CelWarp(sp * 2.0, 0.6), 5, fw * 2.0) * highlands;
                float craterAmount = max(_CraterAmount, 0.45);
                float craters = Craters(sp, fw, craterAmount);
                float grain = FbmLod(sp * 24.0, 4, fw * 24.0);

                s.height = (base - 0.5) * 0.8 + mountains * 0.9 + craters * 0.7 + (grain - 0.5) * 0.15;

                // Basaltic plains, lighter highlands, mineral streaks, bright fresh ejecta at crater rims
                float minerals = FbmLod(sp * 3.5 + 7.0, 4, fw * 3.5);
                float3 col = lerp(_ColorB.rgb, _ColorA.rgb, smoothstep(0.35, 0.65, base));
                col = lerp(col, _ColorC.rgb, highlands * 0.55 + mountains * 0.3);
                col *= lerp(0.85, 1.15, minerals);
                col *= 1.0 + saturate(craters) * 0.35 - saturate(-craters) * 0.25;
                col *= lerp(0.92, 1.08, grain);
                s.albedo = col;
                s.spec = 0.03;
                return s;
            }

            Surface Ice(float3 p, float3 sp, float fw)
            {
                Surface s = (Surface)0;
                s.specPower = 40.0;

                float sheets = FbmLod(sp * 1.8, 5, fw * 1.8);
                float2 v1 = CelVoronoi2(CelWarp(sp * 3.0, 0.4));
                float2 v2 = CelVoronoi2(sp * 9.0 + 5.0);
                float crackWide = smoothstep(0.10, 0.0, v1.y - v1.x);
                float crackFine = smoothstep(0.06, 0.0, v2.y - v2.x) * saturate(1.5 - fw * 9.0 * 3.0);
                float ridges = smoothstep(0.22, 0.10, v1.y - v1.x) - crackWide;
                float frost = FbmLod(sp * 20.0, 4, fw * 20.0);
                float craters = _CraterAmount > 0.0 ? Craters(sp, fw, _CraterAmount) : 0.0;

                s.height = (sheets - 0.5) * 0.5 - crackWide * 0.8 - crackFine * 0.3 + ridges * 0.35 + craters * 0.6;

                float3 col = lerp(_ColorC.rgb, _ColorA.rgb, sheets);
                // Older, dirtier ice in patches
                col = lerp(col, _ColorC.rgb * 0.75, smoothstep(0.55, 0.75, FbmLod(sp * 2.6 + 3.0, 4, fw * 2.6)) * 0.5);
                // Cracks show deep blue subsurface ice
                col = lerp(col, _ColorB.rgb, saturate(crackWide * 0.9 + crackFine * 0.6));
                col *= lerp(0.94, 1.06, frost);
                col *= 1.0 + saturate(craters) * 0.2 - saturate(-craters) * 0.15;
                s.albedo = col;
                s.spec = 0.35 * (1.0 - crackWide);
                return s;
            }

            Surface Gas(float3 p, float3 sp, float fw, float t)
            {
                Surface s = (Surface)0;
                s.specPower = 8.0;

                // Differential rotation: equator and mid-latitudes drift at different speeds
                float lat = p.y;
                float3 rp = RotateY(p, t * (0.004 + 0.003 * sin(lat * 6.0 + _Seed * 9.0)));
                float3 rsp = rp * 2.0 + float3(_Seed * 37.0, 0.0, _Seed * 71.0);

                float3 warped = CelWarp(rsp * float3(1.0, 3.5, 1.0), 0.7);
                float turb = FbmLod(warped * float3(1.5, 4.0, 1.5), 5, fw * 4.0);
                float bandFreq = 6.0 + frac(_Seed * 7.3) * 6.0;
                float band = lat * bandFreq + turb * 1.6 + _Seed * 13.0;

                float b1 = sin(band * 3.14159) * 0.5 + 0.5;
                float b2 = sin(band * 7.37 + turb * 4.0) * 0.5 + 0.5;
                float b3 = sin(band * 17.1 + turb * 7.0) * 0.5 + 0.5;
                float3 col = lerp(_ColorA.rgb, _ColorB.rgb, b1);
                col = lerp(col, _ColorC.rgb, b2 * 0.45);
                col *= lerp(0.88, 1.08, b3);

                // Shear eddies where bands meet
                float shear = abs(cos(band * 3.14159));
                float eddies = FbmLod(CelWarp(rsp * float3(4.0, 10.0, 4.0), 1.2), 4, fw * 10.0);
                col = lerp(col, lerp(_ColorC.rgb, _ColorB.rgb, eddies), smoothstep(0.75, 1.0, shear) * 0.35);

                // A few storms with spiral structure, one of them large
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    float3 h = CelHash33(float3(_Seed * 91.0 + i * 13.0, 3.1 + i, 7.7));
                    float3 center = normalize(float3(h.x * 2.0 - 1.0, (h.y - 0.5) * 1.1, h.z * 2.0 - 1.0));
                    center = RotateY(center, t * 0.004);
                    float size = i == 0 ? 0.30 : 0.10 + h.x * 0.08;
                    float3 dv = (p - center) * float3(1.0, 2.2, 1.0);
                    float dist = length(dv);
                    float spiral = FbmLod(dv * (8.0 / size) + sp, 3, fw * 8.0 / size);
                    float storm = smoothstep(size, size * 0.15, dist + (spiral - 0.5) * size * 0.5);
                    float core = smoothstep(size * 0.4, 0.0, dist);
                    float3 stormCol = i == 0 ? _ColorD.rgb : lerp(_ColorC.rgb, float3(1, 1, 1), 0.4);
                    col = lerp(col, stormCol * lerp(0.85, 1.15, spiral), storm * 0.9);
                    col = lerp(col, stormCol * 1.2, core * 0.5);
                }

                // Hazy, darker poles
                float pole = smoothstep(0.65, 0.95, abs(lat));
                col = lerp(col, _ColorB.rgb * 0.7, pole * 0.6);

                s.albedo = col;
                s.height = (turb - 0.5) * 0.15;
                s.spec = 0.02;
                return s;
            }

            Surface Lava(float3 p, float3 sp, float fw, float t)
            {
                Surface s = (Surface)0;
                s.specPower = 24.0;

                float2 plates = CelVoronoi2(CelWarp(sp * 2.2, 0.5));
                float seam = plates.y - plates.x;
                float crack = smoothstep(0.12, 0.0, seam);
                float2 fine = CelVoronoi2(sp * 8.0 + 3.0);
                float fineCrack = smoothstep(0.05, 0.0, fine.y - fine.x) * saturate(1.5 - fw * 8.0 * 3.0);

                float flow = RidgedLod(CelWarp(sp * 1.6 + float3(0.0, t * 0.01, 0.0), 1.0), 5, fw * 1.6);
                float rivers = smoothstep(0.78, 0.92, flow);
                float crust = FbmLod(sp * 3.0, 5, fw * 3.0);
                float lowlands = smoothstep(0.42, 0.30, FbmLod(sp * 0.9 + 9.0, 4, fw * 0.9));

                float molten = saturate(max(crack, max(rivers, lowlands * 0.9)) + fineCrack * 0.5);
                float pulse = 0.8 + 0.2 * sin(t * 0.9 + flow * 25.0 + plates.x * 10.0);

                s.height = (crust - 0.5) * 0.6 + plates.x * 0.4 - molten * 0.7;

                float3 rock = lerp(_ColorA.rgb, _ColorB.rgb, crust);
                // Crust cools from glowing red near the seams to black rock
                float heat = smoothstep(0.35, 0.0, seam) * 0.6 + rivers * 0.4;
                rock = lerp(rock, _ColorC.rgb * 0.25, heat * 0.5);
                s.albedo = lerp(rock, _ColorC.rgb * 0.3, molten);
                float3 hot = lerp(_ColorC.rgb, float3(1.0, 0.9, 0.5), molten * molten);
                s.emission = hot * (molten * pulse + heat * 0.15) * _EmissionStrength;
                s.spec = 0.08 * (1.0 - molten);
                return s;
            }

            Surface Temperate(float3 p, float3 sp, float fw, float t, float3 lightOS)
            {
                Surface s = (Surface)0;
                s.specPower = 80.0;

                float3 warped = CelWarp(sp * 1.1, 0.8);
                float continent = FbmLod(warped, 6, fw * 1.1);
                float mountains = RidgedLod(sp * 3.0 + 4.0, 5, fw * 3.0);
                float elevation = continent + (mountains - 0.5) * 0.12 * smoothstep(_SeaLevel, _SeaLevel + 0.1, continent);
                float land = smoothstep(_SeaLevel - 0.004, _SeaLevel + 0.004, elevation);
                float height01 = saturate((elevation - _SeaLevel) * 4.0);

                // Ocean: deep to shallow with turquoise coasts
                float depth = saturate((_SeaLevel - elevation) * 7.0);
                float3 ocean = lerp(_ColorD.rgb * 1.6, _ColorA.rgb, smoothstep(0.0, 0.6, depth));
                ocean = lerp(float3(0.10, 0.55, 0.55), ocean, smoothstep(0.0, 0.08, depth));

                // Biomes from temperature (latitude, altitude) and moisture
                float lat = abs(p.y);
                float temperature = 1.0 - lat * 1.25 - height01 * 0.6 + (FbmLod(sp * 2.0 + 13.0, 4, fw * 2.0) - 0.5) * 0.35;
                float moisture = FbmLod(sp * 1.7 + 29.0, 5, fw * 1.7);
                float3 desert = float3(0.76, 0.62, 0.38);
                float3 savanna = float3(0.55, 0.50, 0.24);
                float3 grass = _ColorB.rgb * 1.3;
                float3 forest = _ColorB.rgb * 0.6;
                float3 tundra = float3(0.42, 0.40, 0.33);
                float3 rock = _ColorC.rgb;
                float3 snow = float3(0.93, 0.94, 0.97);

                float3 warmBiome = lerp(desert, lerp(savanna, forest, smoothstep(0.5, 0.75, moisture)), smoothstep(0.35, 0.55, moisture));
                float3 coolBiome = lerp(tundra, grass, smoothstep(0.3, 0.6, moisture));
                float3 ground = lerp(coolBiome, warmBiome, smoothstep(0.35, 0.65, temperature));
                ground = lerp(ground, rock, smoothstep(0.55, 0.8, height01));
                ground = lerp(ground, snow, smoothstep(0.15, 0.0, temperature) + smoothstep(0.85, 0.95, height01));
                ground *= lerp(0.85, 1.12, FbmLod(sp * 18.0, 4, fw * 18.0));

                float3 col = lerp(ocean, ground, land);
                float cap = smoothstep(0.82, 0.9, lat + (FbmLod(sp * 4.0, 4, fw * 4.0) - 0.5) * 0.15);
                col = lerp(col, snow, cap);

                s.albedo = col;
                s.height = land * (height01 * 0.8 + (mountains - 0.5) * 0.6 * height01) + (1.0 - land) * -0.1;
                s.spec = (1.0 - land) * (1.0 - cap);

                // Swirling clouds plus a thin high layer; shadow sampled toward the star
                float3 cp = CelWarp(sp * 1.8 + float3(t * 0.004, 0.0, t * 0.002), 1.1);
                float cloudLow = FbmLod(cp * 1.6, 6, fw * 3.0);
                float cloudHigh = FbmLod(sp * float3(6.0, 2.0, 6.0) + float3(t * 0.01, 0.0, 0.0), 4, fw * 6.0);
                s.clouds = saturate(smoothstep(0.50, 0.70, cloudLow) + smoothstep(0.62, 0.8, cloudHigh) * 0.4) * _CloudAmount;
                float3 shadowP = CelWarp((sp + lightOS * 0.04) * 1.8 + float3(t * 0.004, 0.0, t * 0.002), 1.1);
                s.cloudShadow = smoothstep(0.50, 0.70, FbmLod(shadowP * 1.6, 4, fw * 3.0)) * _CloudAmount;

                // City lights cluster on habitable land near coasts
                if (_CityLights > 0.0)
                {
                    float habitable = land * smoothstep(0.2, 0.5, temperature) * (1.0 - smoothstep(0.4, 0.7, height01)) * (1.0 - cap);
                    float coast = smoothstep(0.25, 0.0, height01);
                    float clusters = smoothstep(0.55, 0.75, FbmLod(sp * 5.0 + 51.0, 4, fw * 5.0));
                    float dots = smoothstep(0.7, 0.95, CelNoise(sp * 60.0)) * saturate(1.5 - fw * 60.0 * 3.0) + 0.25;
                    s.nightLights = float3(1.0, 0.75, 0.4) * habitable * (clusters + coast * 0.4) * dots * _CityLights * 2.0;
                }
                return s;
            }

            float3 PerturbNormal(float3 N, float3 positionWS, float heightWS)
            {
                // Surface gradient bump mapping from screen-space derivatives (Mikkelsen 2010)
                float3 dpdx = ddx(positionWS);
                float3 dpdy = ddy(positionWS);
                float dhdx = ddx(heightWS);
                float dhdy = ddy(heightWS);
                float3 r1 = cross(dpdy, N);
                float3 r2 = cross(N, dpdx);
                float det = dot(dpdx, r1);
                float3 grad = sign(det) * (dhdx * r1 + dhdy * r2);
                float3 perturbed = abs(det) * N - grad;
                float len = length(perturbed);
                return len > 1e-12 ? perturbed / len : N;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 p = normalize(input.positionOS);
                // Seed offset keeps every body's pattern unique while inputs stay small
                float3 sp = p * 2.0 + float3(_Seed * 37.0, _Seed * 53.0, _Seed * 71.0);
                float fw = max(length(fwidth(sp)), 1e-5);
                float t = _Time.y;

                float3 N = normalize(input.normalWS);
                float3 L = CelestialLightDir(input.positionWS);
                float3 V = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 lightColor = CelestialStarColor();
                float3 lightOS = normalize(TransformWorldToObjectDir(L));

                int env = (int)round(_Env);
                Surface s;
                if (env == 2) s = Gas(p, sp, fw, t);
                else if (env == 4) s = Temperate(p, sp, fw, t, lightOS);
                else if (env == 1) s = Ice(p, sp, fw);
                else if (env == 3) s = Lava(p, sp, fw, t);
                else s = Rocky(p, sp, fw);

                // Relief: height in world units relative to the body's radius
                float radiusWS = 0.5 * length(float3(UNITY_MATRIX_M[0][0], UNITY_MATRIX_M[1][0], UNITY_MATRIX_M[2][0]));
                float3 Nb = PerturbNormal(N, input.positionWS, s.height * radiusWS * _BumpStrength);

                // Clouds sit above the relief, so they use the smooth normal
                float3 Ns = lerp(Nb, N, s.clouds);

                float ndlSmooth = dot(N, L);
                float ndl = dot(Ns, L);
                // Soft terminator instead of a hard Lambert cut
                float diffuse = smoothstep(-0.1, 0.5, ndl) * saturate(ndl * 0.5 + 0.6);
                float daylight = smoothstep(-0.15, 0.2, ndlSmooth);

                float shadow = 1.0 - s.cloudShadow * 0.55 * (1.0 - s.clouds);
                float3 color = s.albedo * (diffuse * shadow * lightColor + _Ambient);

                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(Ns, H)), s.specPower) * s.spec * step(0.0, ndlSmooth);
                color += spec * lightColor * 0.6;

                // Clouds, lit with the smooth normal, slightly brighter at the terminator edge
                float3 cloudLit = (smoothstep(-0.1, 0.45, ndlSmooth) * lightColor + _Ambient) * 0.95;
                color = lerp(color, cloudLit, s.clouds);

                color += s.emission;
                color += s.nightLights * (1.0 - daylight) * (1.0 - s.clouds * 0.8);

                // Atmospheric scattering at the limb, brighter on the day side
                float rim = pow(1.0 - saturate(dot(N, V)), 3.0);
                float dayRim = saturate(ndlSmooth * 0.8 + 0.35);
                color += _RimColor.rgb * rim * _RimStrength * dayRim;

                return half4(color, 1.0);
            }
            ENDHLSL
        }

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
                float _CityLights;
                float _BumpStrength;
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
