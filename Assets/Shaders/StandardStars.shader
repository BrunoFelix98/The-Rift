Shader "Custom/StandardStars"
{
    Properties
    {
        [HDR] _Color ("Star Color", Color) = (1,1,1,1)
        [HDR] _GlowColor ("Aura Color", Color) = (1,0.5,0.2,1)
        _MainTex ("Surface Noise", 2D) = "white" {}
        _Emission ("Core Emission", Range(0, 100)) = 10.0
        _GlowPower ("Aura Strength", Range(0.1, 10)) = 2.0
        _NoiseSpeed ("Boiling Speed", Range(0, 2)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD1;
                float3 viewDir : TEXCOORD3;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _GlowColor;
            float _Emission;
            float _GlowPower;
            float _NoiseSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // Normal and ViewDir for Fresnel
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float time = _Time.y * _NoiseSpeed;
                
                // Animated surface noise (boiling gas effect)
                float2 uv1 = i.uv + float2(time * 0.1, time * 0.05);
                float2 uv2 = i.uv - float2(time * 0.07, time * 0.12);
                float n1 = tex2D(_MainTex, uv1).r;
                float n2 = tex2D(_MainTex, uv2).r;
                float combinedNoise = (n1 + n2) * 0.5;

                // Core Color
                float4 coreColor = _Color * _Emission * combinedNoise;

                // Fresnel Aura/Glow
                float3 normal = normalize(i.normal);
                float3 viewDir = normalize(i.viewDir);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _GlowPower);
                float4 aura = _GlowColor * fresnel * _Emission * 2.0;

                return coreColor + aura;
            }
            ENDCG
        }
    }
}
