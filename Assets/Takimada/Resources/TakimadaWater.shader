// Sea: shallow/deep vertex colors, waves, sun glint, shadows and fog.
Shader "Takimada/Water"
{
    Properties
    {
        _Amp ("Dalga", Float) = 1.0
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

            float _Amp;
            float4 _TkSky, _TkGround, _TkFogColor;
            float _TkFogStart, _TkFogEnd;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wn : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float4 col : COLOR;
                SHADOW_COORDS(2)
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y;
                float a1 = wp.x * 0.9 + t * 0.8;
                float a2 = wp.z * 1.1 + t * 0.6;
                float a3 = (wp.x + wp.z) * 1.7 + t * 1.3;
                float h = (sin(a1) * 0.025 + cos(a2) * 0.02 + sin(a3) * 0.01) * _Amp;
                float dx = (cos(a1) * 0.9 * 0.025 + cos(a3) * 1.7 * 0.01) * _Amp;
                float dz = (-sin(a2) * 1.1 * 0.02 + cos(a3) * 1.7 * 0.01) * _Amp;
                wp.y += h;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                o.wn = normalize(float3(-dx, 1.0, -dz));
                o.wpos = wp;
                o.col = v.color;
                TRANSFER_SHADOW(o);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.wn);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                float sh = SHADOW_ATTENUATION(i);
                float ndl = saturate(dot(n, l));
                float3 amb = lerp(_TkGround.rgb, _TkSky.rgb, 0.8);
                float3 c = i.col.rgb * (amb + _LightColor0.rgb * ndl * (0.4 + 0.6 * sh));
                float spec = pow(saturate(dot(n, normalize(l + v))), 90.0) * 0.4 * sh;
                c += _LightColor0.rgb * spec;
                float fr = pow(1.0 - saturate(dot(n, v)), 4.0);
                c = lerp(c, _TkSky.rgb * 1.6, fr * 0.35);
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
