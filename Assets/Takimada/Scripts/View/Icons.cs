using System;
using System.Collections.Generic;
using UnityEngine;

namespace Takimada
{
    /// <summary>
    /// Kaynak sembolleri (odun, tuğla, koyun, buğday, taş). Açılışta kodla çizilir, dosya gerekmez.
    /// Şekiller birim karede (0-1, y yukarı) tanımlanır; kenarlar 4x4 örneklemeyle yumuşatılır.
    /// </summary>
    public static class Icons
    {
        const int Size = 64, SS = 4;
        static Texture2D[] res;

        // şekil: (x, y, büyütme) -> içeride mi
        delegate bool Shape(float x, float y, float grow);
        struct Layer { public Shape shape; public float grow; public Color col; }

        static readonly Color Ink = Pal.Hex("#2B241D");

        // ---------------- indirilen simgeler (game-icons.net, CC BY 3.0) ----------------
        // Resources/TakimadaIcons/*.bytes: siyah siluet, saydam zemin, 512x512 PNG. Unity içe aktarma ayarlarına
        // bağlı kalmamak için .bytes olarak durur ve çalışma anında çözülür. Yoksa kodla çizilen simgeler kullanılır.
        static readonly string[] GlyphFiles = { "odun", "tugla", "koyun", "bugday", "tas" };
        const int GlyphSize = 256;
        static byte[][] glyphAlpha;     // GlyphSize x GlyphSize, satır satır (y yukarı)
        static Texture2D[] glyphTex;
        static bool glyphTried;

        static bool LoadGlyphs()
        {
            if (glyphTried) return glyphAlpha != null;
            glyphTried = true;
            var all = new byte[T.ResCount][];
            for (int r = 0; r < T.ResCount; r++)
            {
                all[r] = LoadAlpha(GlyphFiles[r]);
                if (all[r] == null) return false;
            }
            glyphAlpha = all;
            return true;
        }

        /// <summary>Resources/TakimadaIcons/name.bytes PNG'sini GlyphSize karelik örtüklük haritasına çevirir. Yoksa null.</summary>
        static byte[] LoadAlpha(string name)
        {
            var ta = Resources.Load<TextAsset>("TakimadaIcons/" + name);
            if (ta == null) return null;
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(ta.bytes)) { UnityEngine.Object.Destroy(src); return null; }
            var px = src.GetPixels32();
            int w = src.width, h = src.height;
            UnityEngine.Object.Destroy(src);
            // kutu filtresiyle GlyphSize'a küçült; siyah ve opak = 1
            var a = new byte[GlyphSize * GlyphSize];
            for (int y = 0; y < GlyphSize; y++)
                for (int x = 0; x < GlyphSize; x++)
                {
                    int x0 = x * w / GlyphSize, x1 = Mathf.Max(x0 + 1, (x + 1) * w / GlyphSize);
                    int y0 = y * h / GlyphSize, y1 = Mathf.Max(y0 + 1, (y + 1) * h / GlyphSize);
                    float sum = 0; int n = 0;
                    for (int yy = y0; yy < y1; yy++)
                        for (int xx = x0; xx < x1; xx++)
                        {
                            var c = px[yy * w + xx];
                            float lum = (c.r + c.g + c.b) / (3f * 255f);
                            sum += c.a / 255f * (1f - lum);
                            n++;
                        }
                    a[y * GlyphSize + x] = (byte)Mathf.RoundToInt(Mathf.Clamp01(sum / n) * 255f);
                }
            return a;
        }

        static readonly Dictionary<string, Texture2D> symbols = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Adıyla yüklenen beyaz siluet (GUI.color ile boyanır): "muhafiz", "cekic", "yol".
        /// Dosya yoksa null; çağıran yazıyla idare etmeli.
        /// </summary>
        public static Texture2D Symbol(string name)
        {
            if (symbols.TryGetValue(name, out var t)) return t;
            var a = LoadAlpha(name);
            if (a != null)
            {
                var px = new Color32[GlyphSize * GlyphSize];
                for (int k = 0; k < px.Length; k++) px[k] = new Color32(255, 255, 255, a[k]);
                t = ToTexture(px, GlyphSize, GlyphSize);
            }
            symbols[name] = t;
            return t;
        }

        /// <summary>Kaynağın tek renkli silueti (koyu mürekkep, saydam zemin). Renkli kart/pul üstüne çizmek için.</summary>
        public static Texture2D Glyph(int i)
        {
            i = Mathf.Clamp(i, 0, T.ResCount - 1);
            if (!LoadGlyphs()) return Res(i);
            if (glyphTex == null) glyphTex = new Texture2D[T.ResCount];
            if (glyphTex[i] == null)
            {
                var px = new Color32[GlyphSize * GlyphSize];
                Color32 ink = Ink;
                for (int k = 0; k < px.Length; k++) { ink.a = glyphAlpha[i][k]; px[k] = ink; }
                glyphTex[i] = ToTexture(px, GlyphSize, GlyphSize);
            }
            return glyphTex[i];
        }

        /// <summary>Simge dokusu: indirilen siluet varsa o, yoksa kodla çizilen renkli simge.</summary>
        public static Texture2D Res(int i)
        {
            if (LoadGlyphs()) return Glyph(i);
            if (res == null)
            {
                res = new Texture2D[T.ResCount];
                for (int r = 0; r < T.ResCount; r++) res[r] = ToTexture(Paint(Layers(r)), Size, Size);
            }
            return res[Mathf.Clamp(i, 0, T.ResCount - 1)];
        }

        static Texture2D gear;

        /// <summary>Ayarlar çarkı: 8 dişli beyaz siluet, ortası delik; GUI.color ile boyanır.</summary>
        public static Texture2D Gear()
        {
            if (gear != null) return gear;
            const int N = 128, Teeth = 8, AA = 4;
            const float Hole = 0.15f, Body = 0.34f, Tip = 0.47f;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < AA; sy++)
                        for (int sx = 0; sx < AA; sx++)
                        {
                            float u = (x + (sx + 0.5f) / AA) / N - 0.5f, v = (y + (sy + 0.5f) / AA) / N - 0.5f;
                            float d = Mathf.Sqrt(u * u + v * v);
                            if (d < Hole || d > Tip) continue;
                            if (d <= Body) { hit++; continue; }
                            // diş: açısal olarak dışa doğru biraz daralan kesit
                            float a = Mathf.Atan2(v, u) / (Mathf.PI * 2f) * Teeth;
                            float f = Mathf.Abs(Mathf.Repeat(a, 1f) - 0.5f);
                            float half = Mathf.Lerp(0.24f, 0.17f, (d - Body) / (Tip - Body));
                            if (f < half) hit++;
                        }
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(hit * 255 / (AA * AA)));
                }
            gear = ToTexture(px, N, N);
            return gear;
        }

        static Texture2D card;

        /// <summary>Yuvarlak köşeli beyaz kart şekli; GUI.color ile boyanır.</summary>
        public static Texture2D Card()
        {
            if (card != null) return card;
            const int W = 128, H = 176;
            const float R = 18f;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, R, W - R), cy = Mathf.Clamp(y + 0.5f, R, H - R);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    px[y * W + x] = new Color(1, 1, 1, Mathf.Clamp01(R - d + 0.5f));
                }
            card = ToTexture(px, W, H);
            return card;
        }

        /// <summary>Beş sembol yan yana, saydam yerler verilen zemin rengiyle dolu (pul yüzeyine basmak için).</summary>
        public static Texture2D Atlas(Color background)
        {
            int w = Size * T.ResCount;
            var px = new Color32[w * Size];
            bool glyphs = LoadGlyphs();
            for (int r = 0; r < T.ResCount; r++)
            {
                var icon = glyphs ? GlyphCell(r) : Paint(Layers(r));
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        var c = icon[y * Size + x];
                        float a = c.a / 255f;
                        px[y * w + r * Size + x] = new Color(
                            background.r * (1 - a) + c.r / 255f * a,
                            background.g * (1 - a) + c.g / 255f * a,
                            background.b * (1 - a) + c.b / 255f * a, 1f);
                    }
            }
            return ToTexture(px, w, Size);
        }

        static Texture2D ToTexture(Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>Siluetin Size x Size hücreye küçültülmüş, kenarda biraz boşluk bırakılmış hali (atlas için).</summary>
        static Color32[] GlyphCell(int r)
        {
            var px = new Color32[Size * Size];
            Color32 ink = Ink;
            const float Pad = 0.1f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = ((x + 0.5f) / Size - Pad) / (1 - 2 * Pad), v = ((y + 0.5f) / Size - Pad) / (1 - 2 * Pad);
                    byte a = 0;
                    if (u >= 0 && u < 1 && v >= 0 && v < 1)
                    {
                        // PNG satırları aşağıdan yukarı; atlas da öyle
                        int gx = (int)(u * GlyphSize), gy = (int)(v * GlyphSize);
                        int sum = 0, n = 0, step = Mathf.Max(1, Mathf.CeilToInt(GlyphSize * (1 - 2 * Pad) / Size));
                        for (int dy = 0; dy < step; dy++)
                            for (int dx = 0; dx < step; dx++)
                            {
                                int sx = Mathf.Min(GlyphSize - 1, gx + dx), sy = Mathf.Min(GlyphSize - 1, gy + dy);
                                sum += glyphAlpha[r][sy * GlyphSize + sx]; n++;
                            }
                        a = (byte)(sum / n);
                    }
                    ink.a = a;
                    px[y * Size + x] = ink;
                }
            return px;
        }

        static Shape Circle(float cx, float cy, float r) => (x, y, g) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= (r + g) * (r + g);

        static Shape Box(float x0, float y0, float x1, float y1) => (x, y, g) => x >= x0 - g && x <= x1 + g && y >= y0 - g && y <= y1 + g;

        static Shape Ellipse(float cx, float cy, float rx, float ry, float deg)
        {
            float a = deg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            return (x, y, g) =>
            {
                float dx = x - cx, dy = y - cy;
                float u = dx * ca + dy * sa, v = -dx * sa + dy * ca;
                float ex = rx + g, ey = ry + g;
                return u * u / (ex * ex) + v * v / (ey * ey) <= 1f;
            };
        }

        static Shape Segment(float ax, float ay, float bx, float by, float w) => (x, y, g) =>
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
            float px = ax + dx * t - x, py = ay + dy * t - y;
            return px * px + py * py <= (w + g) * (w + g);
        };

        const float Outline = 0.035f;

        static void Add(List<Layer> l, Shape s, Color c, bool outline = true)
        {
            if (outline) l.Add(new Layer { shape = s, grow = Outline, col = Ink });
            l.Add(new Layer { shape = s, grow = 0, col = c });
        }

        static List<Layer> Layers(int r)
        {
            var l = new List<Layer>();
            switch (r)
            {
                case 0: // odun: üç kütük, kesik yüzleri görünür
                {
                    var wood = Pal.Hex("#C49A6C"); var ring = Pal.Hex("#E6CDA4"); var core = Pal.Hex("#9C7448");
                    foreach (var (cx, cy) in new[] { (0.3f, 0.3f), (0.7f, 0.3f), (0.5f, 0.66f) })
                    {
                        Add(l, Circle(cx, cy, 0.19f), wood);
                        Add(l, Circle(cx, cy, 0.12f), ring, false);
                        Add(l, Circle(cx, cy, 0.045f), core, false);
                    }
                    break;
                }
                case 1: // tuğla: üç sıra şaşırtmalı duvar
                {
                    var brick = Pal.Hex("#DE8A70");
                    l.Add(new Layer { shape = Box(0.08f, 0.14f, 0.92f, 0.86f), grow = 0, col = Ink });
                    float[][] rows = { new[] { 0.12f, 0.48f, 0.52f, 0.88f }, new[] { 0.12f, 0.28f, 0.32f, 0.68f, 0.72f, 0.88f }, new[] { 0.12f, 0.48f, 0.52f, 0.88f } };
                    for (int row = 0; row < 3; row++)
                    {
                        float y0 = 0.18f + row * 0.22f, y1 = y0 + 0.18f;
                        for (int k = 0; k + 1 < rows[row].Length; k += 2)
                            l.Add(new Layer { shape = Box(rows[row][k], y0, rows[row][k + 1], y1), grow = 0, col = brick });
                    }
                    break;
                }
                case 2: // koyun: bulut gibi yün, koyu baş ve bacaklar
                {
                    var wool = Pal.Hex("#F4F1E8"); var dark = Pal.Hex("#4A4038");
                    foreach (var x in new[] { 0.34f, 0.6f })
                        Add(l, Box(x, 0.12f, x + 0.05f, 0.36f), dark);
                    var puffs = new[] { Circle(0.33f, 0.5f, 0.16f), Circle(0.5f, 0.6f, 0.18f), Circle(0.66f, 0.5f, 0.16f), Circle(0.5f, 0.42f, 0.16f), Circle(0.4f, 0.62f, 0.14f), Circle(0.6f, 0.62f, 0.14f) };
                    foreach (var p in puffs) l.Add(new Layer { shape = p, grow = Outline, col = Ink });
                    foreach (var p in puffs) l.Add(new Layer { shape = p, grow = 0, col = wool });
                    Add(l, Ellipse(0.82f, 0.55f, 0.11f, 0.085f, -15f), dark);
                    Add(l, Ellipse(0.78f, 0.66f, 0.05f, 0.025f, 35f), dark, false);
                    l.Add(new Layer { shape = Circle(0.85f, 0.57f, 0.018f), grow = 0, col = wool });
                    break;
                }
                case 3: // buğday: sap ve iki yana açılan taneler
                {
                    var gold = Pal.Hex("#EDD48A"); var stalk = Pal.Hex("#C9A64E");
                    Add(l, Segment(0.5f, 0.08f, 0.5f, 0.78f), stalk);
                    for (int k = 0; k < 4; k++)
                    {
                        float y = 0.34f + k * 0.12f;
                        Add(l, Ellipse(0.41f, y, 0.065f, 0.11f, 35f), gold);
                        Add(l, Ellipse(0.59f, y, 0.065f, 0.11f, -35f), gold);
                    }
                    Add(l, Ellipse(0.5f, 0.86f, 0.06f, 0.1f, 0f), gold);
                    break;
                }
                default: // taş: büyük ve küçük kaya, üstte ışık
                {
                    var stone = Pal.Hex("#B5BBC1"); var light = Pal.Hex("#DCE0E3");
                    Add(l, Ellipse(0.42f, 0.42f, 0.3f, 0.24f, 8f), stone);
                    Add(l, Ellipse(0.72f, 0.3f, 0.17f, 0.14f, -10f), stone);
                    l.Add(new Layer { shape = Ellipse(0.34f, 0.52f, 0.12f, 0.06f, 20f), grow = 0, col = light });
                    l.Add(new Layer { shape = Ellipse(0.7f, 0.36f, 0.06f, 0.03f, 10f), grow = 0, col = light });
                    break;
                }
            }
            return l;
        }

        static Shape Segment(float ax, float ay, float bx, float by) => Segment(ax, ay, bx, by, 0.03f);

        static Color32[] Paint(List<Layer> layers)
        {
            var px = new Color32[Size * Size];
            for (int py = 0; py < Size; py++)
                for (int pxi = 0; pxi < Size; pxi++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float x = (pxi + (sx + 0.5f) / SS) / Size, y = (py + (sy + 0.5f) / SS) / Size;
                            for (int i = layers.Count - 1; i >= 0; i--)
                            {
                                if (!layers[i].shape(x, y, layers[i].grow)) continue;
                                var c = layers[i].col;
                                r += c.r; g += c.g; b += c.b; a += 1;
                                break;
                            }
                        }
                    if (a > 0) { r /= a; g /= a; b /= a; }
                    px[py * Size + pxi] = new Color(r, g, b, a / (SS * SS));
                }
            return px;
        }
    }
}
