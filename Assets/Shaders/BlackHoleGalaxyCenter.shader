Shader "Custom/BlackHoleGalaxyCenter"
{
    Properties
    {
        _MainColor ("Accretion Disk Color", Color) = (1, 0.5, 0.2, 1)
        _CoreColor ("Black Hole Core", Color) = (0, 0, 0, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 20)) = 5.0
        _DiskSize ("Disk Size", Range(0.1, 10)) = 2.0
        _LensingAmount ("Lensing Amount", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        ZWrite Off
        Blend One One
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            float4 _MainColor;
            float4 _CoreColor;
            float _GlowIntensity;
            float _DiskSize;
            float _LensingAmount;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 v = normalize(i.viewDir);
                
                float dotNV = dot(n, v);
                float absDotNV = abs(dotNV);

                // Accretion Disk Fresnel (Glow around the edges)
                float diskGlow = 1.0 - absDotNV;
                diskGlow = pow(diskGlow, 4.0) * _GlowIntensity;

                // Gargantua "Hat" Effect (Simulated lensing)
                // We simulate the disk being bent by gravity by adding glow when looking near the edges
                float lensing = smoothstep(0.1, 0.4, absDotNV);
                lensing = 1.0 - lensing;
                lensing = pow(lensing, 2.0) * _LensingAmount;

                // Event Horizon (Black Core)
                float core = smoothstep(0.5, 0.6, absDotNV);
                
                fixed4 col = _MainColor * (diskGlow + lensing);
                col = lerp(col, _CoreColor, core);

                // Add a bright ring at the edge of the event horizon
                float ring = smoothstep(0.48, 0.5, absDotNV) * (1.0 - smoothstep(0.5, 0.52, absDotNV));
                col += _MainColor * ring * 10.0;

                return col;
            }
            ENDCG
        }
    }
}
