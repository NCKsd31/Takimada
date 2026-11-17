// Board, pieces and highlights: vertex color + sun light + shadows + distance fog.
// Vertex color alpha is glow amount: 1 = normal, 0 = fully glowing (lava, highlights).
Shader "Takimada/Lit"
{
    Properties
    {
        _Glow ("Glow", Float) = 1.6
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            float _Glow;
            sampler2D _MainTex;
            float4 _TkSky, _TkGround, _TkFogColor;
            float _TkFogStart, _TkFogEnd;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wn : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float4 col : COLOR;
                SHADOW_COORDS(2)
                float2 uv : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.col = v.color;
                o.uv = v.uv;
                TRANSFER_SHADOW(o);
                return o;
            }

            float4 frag (v2f i, float facing : VFACE) : SV_Target
            {
                // two-sided: flip the normal on back faces
                float3 n = normalize(i.wn) * (facing > 0 ? 1.0 : -1.0);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float ndl = saturate(dot(n, l));
                float sh = SHADOW_ATTENUATION(i);
                float3 amb = lerp(_TkGround.rgb, _TkSky.rgb, n.y * 0.5 + 0.5);
                float3 albedo = i.col.rgb * tex2D(_MainTex, i.uv).rgb;
                float3 c = albedo * (amb + _LightColor0.rgb * ndl * sh);
                c += albedo * (1.0 - i.col.a) * _Glow;
                float d = distance(i.wpos, _WorldSpaceCameraPos);
                float f = saturate((d - _TkFogStart) / max(0.001, _TkFogEnd - _TkFogStart));
                c = lerp(c, _TkFogColor.rgb, f * f);
                return float4(c, 1);
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f { V2F_SHADOW_CASTER; };

            v2f vert (appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    FallBack Off
}
