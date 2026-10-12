// Star corona drawn on a sphere shell around the star (child object scaled to _Size star radii).
// Each pixel looks at the closest approach of its view ray to the star's center: the glow falls off
// with that distance, and the streamers are looked up from the closest point's direction in the
// star's own space, so they stay attached to the star instead of turning with the camera.
Shader "TheRift/Celestial/Corona"
{
    Properties
    {
        _Seed ("Seed", Float) = 0
        [HDR] _CoronaColor ("Corona Color", Color) = (1, 0.7, 0.35, 1)
        _Size ("Shell Size (star radii)", Range(1.2, 8)) = 4
        _Falloff ("Falloff", Range(0.5, 8)) = 2.2
        _Intensity ("Intensity", Range(0, 10)) = 1.6
        _Speed ("Streamer Speed", Range(0, 2)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardCorona"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            // Inside faces: works from outside and from within the shell, and is drawn once
            Cull Front

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
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float shellRadius = 0.5 * length(float3(UNITY_MATRIX_M[0][0], UNITY_MATRIX_M[1][0], UNITY_MATRIX_M[2][0]));
                float starRadius = shellRadius / max(_Size, 1.01);

                float3 origin = _WorldSpaceCameraPos;
                float3 dir = normalize(input.positionWS - origin);
                float along = max(dot(centerWS - origin, dir), 0.0);
                float3 closest = origin + dir * along;
                float3 offset = closest - centerWS;

                // Impact distance in star radii; inside the disc the star itself covers the glow
                float b = length(offset) / starRadius;
                float glow = exp(-max(b - 1.0, 0.0) * _Falloff);

                float3 dirOS = normalize(TransformWorldToObjectDir(offset + 1e-4));
                float t = _Time.y * _Speed;
                float rays = CelFbm(dirOS * 4.0 + float3(_Seed * 10.0, t * 0.05, 0.0), 4);
                float fine = CelNoise(dirOS * 14.0 + float3(0.0, t * 0.2, _Seed * 3.0));
                float streamers = lerp(0.45, 1.8, rays) * lerp(0.8, 1.2, fine);
                glow *= lerp(1.0, streamers, saturate((b - 1.0) * 1.2));

                // Fade well before the shell surface so the sphere edge never shows
                float edge = saturate((_Size - b) / (_Size * 0.4));
                glow *= edge * edge;

                return half4(_CoronaColor.rgb * glow * _Intensity, 1.0);
            }
            ENDHLSL
        }
    }
}
