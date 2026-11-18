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
            float _TkWaveAmp;   // BoardView ayarlar; gemiler de aynı değerle sallanır
            float _TkRipple;    // küçük dalgacıkların gücü
            float4 _TkSky, _TkGround, _TkFogColor;
            float _TkFogStart, _TkFogEnd;
            float _TkBright;   // ayarlardaki parlaklık - 1 (Settings.ApplyBrightness)

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
                float amp = _Amp * _TkWaveAmp;
                float h = (sin(a1) * 0.025 + cos(a2) * 0.02 + sin(a3) * 0.01) * amp;
                float dx = (cos(a1) * 0.9 * 0.025 + cos(a3) * 1.7 * 0.01) * amp;
                float dz = (-sin(a2) * 1.1 * 0.02 + cos(a3) * 1.7 * 0.01) * amp;
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
                float camD = distance(i.wpos, _WorldSpaceCameraPos);

                // küçük dalgacıklar: farklı yönlerde ilerleyen kısa dalgalar yalnızca normali bozar (geometri değişmez)
                {
                    float2 p = i.wpos.xz;
                    float t = _Time.y;
                    float2 g = 0;
                    float2 d1 = float2(0.96, 0.28), d2 = float2(-0.37, 0.93), d3 = float2(0.66, -0.75), d4 = float2(-0.98, -0.2);
                    // bir pikselin kapladığı dünya genişliği; dalga bir piksele sığmayacak kadar sıklaşınca söner
                    // (yoksa uzakta ve yatık açıda parlamalar kırpışıp pürüzlü görünüyor)
                    float px = max(length(fwidth(p)), 1e-4);
                    #define TK_AA(freq) saturate(1.0 - (freq) * px * 0.55)
                    g += d1 * cos(dot(p, d1) * 6.0 + t * 1.9) * 0.040 * TK_AA(6.0);
                    g += d2 * cos(dot(p, d2) * 7.5 + t * 2.3) * 0.035 * TK_AA(7.5);
                    g += d3 * cos(dot(p, d3) * 11.0 + t * 2.9) * 0.028 * TK_AA(11.0);
                    g += d4 * cos(dot(p, d4) * 15.0 + t * 3.4) * 0.020 * TK_AA(15.0);
                    // uzakta titremesin diye sönümlenir
                    float fade = saturate(1.0 - camD / max(1.0, _TkFogEnd));
                    g *= _TkRipple * fade;
                    n = normalize(n + float3(-g.x, 0, -g.y));
                }
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

                // kıyıya vuran dalgalar: açıktan kıyıya ilerleyen kırık köpük çizgileri + kıyıda gidip gelen ince köpük.
                // Köşe rengi alfası kıyıya uzaklıktır (BoardView.ShoreRange = 3 birim).
                {
                    float sd = i.col.a * 3.0;
                    float t = _Time.y;
                    float2 p = i.wpos.xz;
                    float wob = sin(p.x * 2.7 + t * 0.4) * 0.5 + sin(p.y * 3.3 - t * 0.3) * 0.5;
                    // sabit fazlı nokta zamanla kıyıya yaklaşır (~0,5 birim/sn, bantlar arası ~1,8 birim)
                    float k = 0.55;
                    float wave = frac(t * 0.28 + sd * k + wob * 0.05);
                    float aa = max(fwidth(sd * k) * 1.5, 0.02);
                    float crest = smoothstep(0.0, aa + 0.03, wave) * (1.0 - smoothstep(0.05, 0.05 + aa + 0.12, wave));
                    // açıkta görünmez, kıyıya yaklaştıkça belirir, kumda söner
                    crest *= (1.0 - smoothstep(0.7, 1.5, sd)) * smoothstep(0.03, 0.2, sd);
                    // çizgiler kesintili olsun, düz halka gibi durmasın
                    float br = saturate(0.5 + 0.6 * sin(p.x * 4.1 + p.y * 1.7 + t * 0.2) * sin(p.y * 3.7 - p.x * 1.3 - t * 0.15));
                    crest *= br;
                    // kıyı çizgisinde gidip gelen köpük
                    float swashAt = 0.2 + 0.07 * sin(t * 0.9 + wob * 2.0);
                    float swash = 1.0 - smoothstep(0.0, 0.05 + fwidth(sd), abs(sd - swashAt));
                    swash *= 0.6 + 0.4 * br;
                    float foam = saturate(crest * 0.42 + swash * 0.3);
                    float3 foamC = float3(0.94, 0.98, 0.97) * (amb + _LightColor0.rgb * (0.55 + 0.45 * sh));
                    c = lerp(c, foamC, foam);
                }

                float d = distance(i.wpos, _WorldSpaceCameraPos);
                float f = saturate((d - _TkFogStart) / max(0.001, _TkFogEnd - _TkFogStart));
                c = lerp(c, _TkFogColor.rgb, f * f);
                c *= 1.0 + _TkBright;
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
