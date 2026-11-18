using UnityEngine;

namespace Takimada
{
    /// <summary>IMGUI stilleri: lacivert harita masası üzerine pirinç (brass) vurgular.</summary>
    public static class Sty
    {
        public static readonly Color Panel = new Color(0.055f, 0.10f, 0.13f, 0.94f);
        public static readonly Color Panel2 = new Color(0.09f, 0.15f, 0.19f, 1f);
        public static readonly Color Line = Pal.Hex("#2B4350");
        public static readonly Color Text = Pal.Hex("#EDE6D6");
        public static readonly Color Dim = Pal.Hex("#9FB0B5");
        public static readonly Color Brass = Pal.Hex("#DDB878");
        public static readonly Color BrassDark = Pal.Hex("#8E6A26");
        public static readonly Color Danger = Pal.Hex("#E39187");
        public static readonly Color Good = Pal.Hex("#9BD3A5");

        public static Texture2D White;
        public static GUIStyle panel, title, h1, h2, label, small, dim, center, centerBig, btn, btnOn, btnAccent, btnSmall, field, tab, tabOn, wrap;
        static bool ready;

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        static GUIStyle Button(Color bg, Color hover, Color text, int size)
        {
            var s = new GUIStyle(GUI.skin.button)
            {
                fontSize = size,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 4, 4),
                margin = new RectOffset(3, 3, 3, 3),
                border = new RectOffset(0, 0, 0, 0),
                wordWrap = true
            };
            s.normal.background = Tex(bg); s.normal.textColor = text;
            s.hover.background = Tex(hover); s.hover.textColor = text;
            s.active.background = Tex(Color.Lerp(hover, Color.black, 0.25f)); s.active.textColor = text;
            s.focused.background = s.normal.background; s.focused.textColor = text;
            s.onNormal.background = Tex(hover); s.onNormal.textColor = text;
            return s;
        }

        public static void Init()
        {
            if (ready) return;
            ready = true;
            White = Tex(Color.white);

            panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 12, 12), border = new RectOffset(0, 0, 0, 0) };
            panel.normal.background = Tex(Panel);

            label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = false, richText = true };
            label.normal.textColor = Text;
            wrap = new GUIStyle(label) { wordWrap = true };
            small = new GUIStyle(label) { fontSize = 12, wordWrap = true };
            small.normal.textColor = Text;
            dim = new GUIStyle(small);
            dim.normal.textColor = Dim;
            h2 = new GUIStyle(label) { fontSize = 15, fontStyle = FontStyle.Bold };
            h2.normal.textColor = Brass;
            h1 = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
            title = new GUIStyle(label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            title.normal.textColor = Brass;
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            centerBig = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };

            btn = Button(Pal.Hex("#1F3742"), Pal.Hex("#2B4A57"), Text, 14);
            btnOn = Button(Pal.Hex("#5A4A22"), Pal.Hex("#6E5A2A"), Pal.Hex("#FFE3A3"), 14);
            btnAccent = Button(Brass, Pal.Hex("#E8C894"), Pal.Hex("#1B1712"), 15);
            btnSmall = Button(Pal.Hex("#1F3742"), Pal.Hex("#2B4A57"), Text, 13);
            btnSmall.padding = new RectOffset(4, 4, 2, 2);
            tab = Button(Pal.Hex("#13232B"), Pal.Hex("#1F3742"), Dim, 13);
            tabOn = Button(Pal.Hex("#2B4A57"), Pal.Hex("#2B4A57"), Brass, 13);

            field = new GUIStyle(GUI.skin.textField) { fontSize = 15, padding = new RectOffset(8, 8, 6, 6) };
            field.normal.background = Tex(Pal.Hex("#0A151B")); field.normal.textColor = Text;
            field.focused.background = Tex(Pal.Hex("#0F2029")); field.focused.textColor = Text;
            field.hover.background = field.normal.background; field.hover.textColor = Text;
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = old;
        }

        public static void Frame(Rect r, Color c, float w = 2)
        {
            Fill(new Rect(r.x, r.y, r.width, w), c);
            Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Fill(new Rect(r.x, r.y, w, r.height), c);
            Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        static readonly System.Collections.Generic.Dictionary<(GUIStyle, Color), GUIStyle> colored =
            new System.Collections.Generic.Dictionary<(GUIStyle, Color), GUIStyle>();

        /// <summary>Aynı stilin başka yazı rengiyle kopyası. Önbellekli: döndürülen stil değiştirilmemeli.</summary>
        public static GUIStyle Colored(GUIStyle s, Color c)
        {
            if (colored.TryGetValue((s, c), out var n)) return n;
            n = new GUIStyle(s);
            n.normal.textColor = c;
            colored[(s, c)] = n;
            return n;
        }
    }
}
