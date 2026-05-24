Shader "Custom/VolumetricNebula"
{
    Properties
    {
        [HDR] _Color1 ("Primary Color", Color) = (0.2, 0.5, 1.0, 0.5)
        [HDR] _Color2 ("Secondary Color", Color) = (0.5, 0.1, 0.8, 0.3)
        _MainTex ("Base Noise", 2D) = "white" {}
        _Speed ("Scroll Speed", Vector) = (0.1, 0.1, 0, 0)
        _Distortion ("Distortion", Range(0, 1)) = 0.5
        _Density ("Density", Range(0, 5)) = 1.0
        _FresnelPower ("Fresnel Power", Range(0.1, 5.0)) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline"}
        LOD 100

        Pass
        {
            Blend One One // Additive blending for gaseous look
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color1;
            float4 _Color2;
            float4 _Speed;
            float _Distortion;
            float _Density;
            float _FresnelPower;

            Varyings vert (Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                return output;
            }

            // Simple pseudo-random noise
            float noise(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }

            float4 frag (Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y;

                // Scrolling noise layers
                float2 scroll1 = uv + _Speed.xy * time;
                float2 scroll2 = uv - _Speed.xy * time * 0.7;

                float n1 = tex2D(_MainTex, scroll1).r;
                float n2 = tex2D(_MainTex, scroll2).g;
                
                // Fresnel for volumetric edges
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);

                // Mix colors based on noise
                float combinedNoise = saturate(n1 + n2 * _Distortion);
                float4 finalColor = lerp(_Color1, _Color2, n2) * combinedNoise * _Density;

                return finalColor * fresnel;
            }
            ENDHLSL
        }
    }
}
