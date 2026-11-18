// Board, pieces and highlights: vertex color + sun light + shadows + distance fog.
// Vertex color alpha is glow amount: 1 = normal, 0 = fully glowing (lava, highlights).
// TEXCOORD1 carries per-vertex animation (see Mesh3.Anim): sheep graze and wander, ships ride the waves,
// dolphins leap, whales surface and dive, volcano smoke rises. _Cull = Front draws the piece outlines.
Shader "Takimada/Lit"
{
    Properties
    {
        _Glow ("Glow", Float) = 1.6
        _MainTex ("Texture", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    float _TkWaveAmp;

    // Must match the vertex waves in TakimadaWater.shader so ships sit on the water.
    void TkWave(float2 p, float t, out float h, out float dx, out float dz)
    {
        float a1 = p.x * 0.9 + t * 0.8;
        float a2 = p.y * 1.1 + t * 0.6;
        float a3 = (p.x + p.y) * 1.7 + t * 1.3;
        h  = (sin(a1) * 0.025 + cos(a2) * 0.02 + sin(a3) * 0.01) * _TkWaveAmp;
        dx = (cos(a1) * 0.9 * 0.025 + cos(a3) * 1.7 * 0.01) * _TkWaveAmp;
        dz = (-sin(a2) * 1.1 * 0.02 + cos(a3) * 1.7 * 0.01) * _TkWaveAmp;
    }

    float2 TkRot(float2 v, float a)
    {
        float s = sin(a), c = cos(a);
        return float2(c * v.x - s * v.y, s * v.x + c * v.y);
    }

    // Nose-down rotation by ang (radians) about the horizontal axis through c that is perpendicular to heading f (x,z).
    void TkPitch(inout float3 p, inout float3 n, float3 c, float2 f, float ang)
    {
        float2 s = float2(f.y, -f.x);
        float cs = cos(ang), sn = sin(ang);
        float3 r = p - c;
        float a = dot(r.xz, f), sd = dot(r.xz, s);
        float a2 = a * cs + r.y * sn, y2 = -a * sn + r.y * cs;
        p = c + float3(f.x * a2 + s.x * sd, y2, f.y * a2 + s.y * sd);
        float na = dot(n.xz, f), ns = dot(n.xz, s);
        float na2 = na * cs + n.y * sn, ny2 = -na * sn + n.y * cs;
        n = float3(f.x * na2 + s.x * ns, ny2, f.y * na2 + s.y * ns);
    }

    // Kinds 4 and up pack extra data in an.z: integer part = parameter, fraction = phase (Mesh3.Packed).
    float2 TkHeading(float4 an) { float hd = floor(an.z) * 0.01745329; return float2(sin(hd), cos(hd)); }

    // Smoke puff life 0..1 (also used to tint the puff lighter as it rises).
    float TkSmokeU(float4 an) { return frac(_Time.y * 0.17 + frac(an.z)); }

    // an.xy = pivot (x,z), an.z = phase 0..1, an.w = kind: 1 sheep body, 2 sheep head, 3 ship,
    // 4 dolphin, 5 whale, 6 whale spout, 7 volcano smoke
    void TkAnimate(inout float3 p, inout float3 n, float4 an)
    {
        if (an.w < 0.5) return;
        float t = _Time.y;
        float ph = an.z * 6.2831853;
        float2 pv = an.xy;

        if (an.w < 2.5)
        {
            // sheep: slow wandering around its spot, turning now and then; the head dips to graze
            float tt = t * 0.35 + ph;
            float yawA = sin(tt * 0.8) * 0.7 + sin(tt * 0.31 + ph) * 0.5;
            float2 off = float2(sin(tt * 0.6 + 1.7), sin(tt * 0.47 + ph)) * 0.035;
            float2 rel = TkRot(p.xz - pv, yawA);
            if (an.w > 1.5)
            {
                float g = smoothstep(0.55, 0.95, sin(t * 0.7 + ph * 3.0));
                p.y -= g * 0.045;
            }
            p.y += abs(sin(tt * 5.0)) * 0.004;
            p.xz = pv + off + rel;
            n.xz = TkRot(n.xz, yawA);
        }
        else if (an.w < 3.5)
        {
            // ship: follow the wave height under its centre and tilt with the wave slope
            float3 wpv = mul(unity_ObjectToWorld, float4(pv.x, 0, pv.y, 1)).xyz;
            float h, dx, dz;
            TkWave(wpv.xz, t, h, dx, dz);
            float3 rel = p - float3(pv.x, 0, pv.y);
            float rz = atan(dx) * 2.5 + sin(t * 1.1 + ph) * 0.03;
            float rx = -atan(dz) * 2.5 + sin(t * 0.8 + ph * 1.7) * 0.02;
            rel.xy = TkRot(rel.xy, rz); n.xy = TkRot(n.xy, rz);
            rel.yz = TkRot(rel.yz, rx); n.yz = TkRot(n.yz, rx);
            p = float3(pv.x, 0, pv.y) + rel;
            p.y += h + sin(t * 1.3 + ph) * 0.008;
        }
        else if (an.w < 4.5)
        {
            // dolphin: built at the top of its leap; swings along an arc around an axis under the water
            // (BoardView.DolphinAxisY / DolphinArc), then waits hidden below the surface for the next leap
            float2 f = TkHeading(an);
            float ph01 = frac(an.z);
            float u = frac(t / 6.5 + ph01);
            float th = u < 0.2 ? lerp(-1.35, 1.35, u / 0.2) : 3.14159;
            TkPitch(p, n, float3(pv.x, -0.34, pv.y), f, th);
            p.xz += f * (sin(t * 0.045 + ph01 * 6.2831853) * 0.6);
        }
        else if (an.w < 6.5)
        {
            // whale: surfaces, breathes (spout), then dives showing its tail and swims on hidden
            float2 f = TkHeading(an);
            float ph01 = frac(an.z);
            float u = frac(t / 18.0 + ph01);
            if (an.w > 5.5)
            {
                // spout grows out of the blowhole (BoardView.SpoutY) while at the surface
                float sp = smoothstep(0.14, 0.18, u) * (1.0 - smoothstep(0.27, 0.35, u));
                float3 b = float3(pv.x, 0.07, pv.y);
                float3 r = p - b;
                r.y *= 0.5 + 0.5 * smoothstep(0.14, 0.24, u);
                p = b + r * sp;
            }
            float rise = 1.0 - smoothstep(0.0, 0.08, u);
            float sink = smoothstep(0.56, 0.7, u);
            float pitch = smoothstep(0.5, 0.62, u) * 0.75 - rise * 0.15;
            TkPitch(p, n, float3(pv.x - f.x * 0.12, -0.02, pv.y - f.y * 0.12), f, pitch);
            p.y += sin(t * 1.2 + ph) * 0.008 * (1.0 - sink) - rise * 0.5 - sink * 1.1;
            p.xz += f * (u * 0.6);
        }
        else
        {
            // smoke puff: rises from the crater (base height in an.z), swells, drifts with the wind and shrinks away
            float u = TkSmokeU(an);
            float3 c = float3(pv.x, floor(an.z) * 0.001, pv.y);
            float k = (0.35 + 1.25 * u) * smoothstep(0.0, 0.1, u) * (1.0 - smoothstep(0.72, 1.0, u));
            float3 off = float3(0.24 * u * u + sin(t * 0.7 + ph) * 0.03 * u, 0.95 * u, 0.08 * u + cos(t * 0.6 + ph * 0.8) * 0.03 * u);
            p = c + (p - c) * k + off;
        }
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma target 3.0
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            float _Glow;
            sampler2D _MainTex;
            float4 _TkSky, _TkGround, _TkFogColor;
            float _TkFogStart, _TkFogEnd;
            float _TkBright;   // ayarlardaki parlaklık - 1 (Settings.ApplyBrightness)

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 an : TEXCOORD1;
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
                float3 p = v.vertex.xyz, nn = v.normal;
                TkAnimate(p, nn, v.an);
                v.vertex.xyz = p;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(nn);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.col = v.color;
                // smoke lightens as it rises
                if (v.an.w > 6.5) o.col.rgb = lerp(o.col.rgb, float3(0.62, 0.6, 0.58), saturate(TkSmokeU(v.an) * 1.3));
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

            struct appdata_anim
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 an : TEXCOORD1;
            };

            struct v2f { V2F_SHADOW_CASTER; };

            v2f vert (appdata_anim v)
            {
                v2f o;
                float3 p = v.vertex.xyz, nn = v.normal;
                TkAnimate(p, nn, v.an);
                v.vertex.xyz = p;
                v.normal = nn;
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
