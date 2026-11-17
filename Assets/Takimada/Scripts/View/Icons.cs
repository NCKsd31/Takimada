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

        public static Texture2D Res(int i)
        {
            if (res == null)
            {
                res = new Texture2D[T.ResCount];
                for (int r = 0; r < T.ResCount; r++) res[r] = ToTexture(Paint(Layers(r)), Size, Size);
            }
            return res[Mathf.Clamp(i, 0, T.ResCount - 1)];
        }

        /// <summary>Beş sembol yan yana, saydam yerler verilen zemin rengiyle dolu (pul yüzeyine basmak için).</summary>
        public static Texture2D Atlas(Color background)
        {
            int w = Size * T.ResCount;
            var px = new Color32[w * Size];
            for (int r = 0; r < T.ResCount; r++)
            {
                var icon = Paint(Layers(r));
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
