using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Takimada
{
    /// <summary>
    /// Net yazı ve simgeli metin.
    ///
    /// Arayüz 860 piksel yüksekliğe göre tasarlanıp GUI.matrix ile ölçekleniyor. IMGUI yazıyı font boyutunda
    /// pikselleştirip sonra büyüttüğü için büyük ekranda (ör. 1440p'de 1,67x) yazılar bulanık çıkıyordu.
    /// Buradaki yardımcılar yerleşimi aynı (ölçekli) koordinatlarda tutar ama yazıyı birim matrisle,
    /// gerçek piksel boyutunda bir fontla çizer.
    /// </summary>
    public partial class Hud
    {
        readonly Dictionary<GUIStyle, GUIStyle> crispStyles = new Dictionary<GUIStyle, GUIStyle>();
        readonly Dictionary<GUIStyle, GUIStyle> hiddenStyles = new Dictionary<GUIStyle, GUIStyle>();
        float crispScale = -1;

        // Net yazı yolu kapalı: ölçekli GUI.matrix ile birlikte yazıların yerini kaydırıyordu.
        // Kapalıyken her şey düz GUI.Label/GUI.Button ile, eskisi gibi çizilir.
        const bool CrispEnabled = false;

        bool Native => !CrispEnabled || Mathf.Abs(scale - 1f) < 0.01f;

        static RectOffset ScaleOffset(RectOffset o, float k) =>
            new RectOffset(Mathf.RoundToInt(o.left * k), Mathf.RoundToInt(o.right * k), Mathf.RoundToInt(o.top * k), Mathf.RoundToInt(o.bottom * k));

        /// <summary>Aynı stilin gerçek piksel boyutlu, arka plansız kopyası (yalnız yazı için).</summary>
        GUIStyle Crisp(GUIStyle s)
        {
            if (crispScale != scale || crispStyles.Count > 400) { crispStyles.Clear(); crispScale = scale; }
            if (!crispStyles.TryGetValue(s, out var c))
            {
                c = new GUIStyle(s);
                int fs = s.fontSize > 0 ? s.fontSize : 13;
                c.fontSize = Mathf.Max(1, Mathf.RoundToInt(fs * scale));
                c.padding = ScaleOffset(s.padding, scale);
                c.contentOffset = s.contentOffset * scale;
                c.fixedWidth = s.fixedWidth * scale;
                c.fixedHeight = s.fixedHeight * scale;
                c.clipping = TextClipping.Overflow;
                foreach (var st in new[] { c.normal, c.hover, c.active, c.focused, c.onNormal, c.onHover, c.onActive, c.onFocused })
                {
                    st.background = null;
#if UNITY_EDITOR
                    st.scaledBackgrounds = new Texture2D[0]; // yalnızca editörde var
#endif
                }
                crispStyles[s] = c;
            }
            c.richText = s.richText; // bazı stillerin richText'i sonradan açılıyor
            return c;
        }

        static readonly Regex SizeTag = new Regex(@"<size=(\d+)>");

        string ScaleTags(string text) =>
            text.IndexOf("<size=", System.StringComparison.Ordinal) < 0 ? text :
            SizeTag.Replace(text, m => "<size=" + Mathf.RoundToInt(int.Parse(m.Groups[1].Value) * scale) + ">");

        /// <summary>GUI.Label yerine: konum ölçekli koordinatta, yazı ekran çözünürlüğünde.</summary>
        void Label(Rect r, string text, GUIStyle s)
        {
            if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
            if (Native) { GUI.Label(r, text, s); return; }
            if (s.normal.background != null) s.Draw(r, GUIContent.none, false, false, false, false);
            CrispText(r, text, s);
        }

        // Net yazı birim matrisle (ekran pikseliyle) çizilmeli; ama GUI.matrix bir BeginArea/ScrollView
        // içindeyken değiştirilince IMGUI'nin kırpması bozuluyor ve her şey kayıyor. Ayrıca IMGUI'nin koordinat
        // dönüştürme işlevleri ölçekli matrisle kırpma içinde güvenilir değil. Bu yüzden:
        //  - alanların ve kaydırma görünümlerinin konumunu kendimiz takip ediyoruz (BeginArea/BeginScroll),
        //  - kırpma içindeki yazıları ekran koordinatıyla sıraya alıyor, en üst seviyeye dönünce
        //    (Panel, Modal, çizim sonu) kendi kırpma dikdörtgenleriyle çiziyoruz.
        struct Frame { public Vector2 origin; public Rect clip; }

        readonly List<Frame> frames = new List<Frame>();

        struct PendingText
        {
            public Rect screen, clip;
            public string text;
            public GUIStyle style;
            public Color color, content;
            public bool enabled;
        }

        readonly List<PendingText> pendingText = new List<PendingText>();

        /// <summary>Şu anki yerel koordinatların başlangıcı, ölçekli GUI biriminde mutlak.</summary>
        Vector2 Origin => frames.Count == 0 ? Vector2.zero : frames[frames.Count - 1].origin;

        /// <summary>Şu anki kırpma dikdörtgeni, ölçekli GUI biriminde mutlak.</summary>
        Rect ClipAbs => frames.Count == 0 ? new Rect(0, 0, sw, sh) : frames[frames.Count - 1].clip;

        static Rect Intersect(Rect a, Rect b)
        {
            float x0 = Mathf.Max(a.xMin, b.xMin), y0 = Mathf.Max(a.yMin, b.yMin);
            float x1 = Mathf.Min(a.xMax, b.xMax), y1 = Mathf.Min(a.yMax, b.yMax);
            return new Rect(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }

        /// <summary>local: yerel koordinatta kırpma alanı; shift: içeriğin bu alana göre kayması (kaydırma).</summary>
        void PushFrame(Rect local, Vector2 shift)
        {
            var o = Origin;
            var abs = new Rect(o.x + local.x, o.y + local.y, local.width, local.height);
            frames.Add(new Frame { origin = abs.position + shift, clip = Intersect(ClipAbs, abs) });
        }

        void PopFrame() { if (frames.Count > 0) frames.RemoveAt(frames.Count - 1); }

        void BeginArea(Rect r) { GUILayout.BeginArea(r); PushFrame(r, Vector2.zero); }

        void EndArea() { GUILayout.EndArea(); PopFrame(); }

        static Vector2 ScrollShift(Vector2 scroll) => new Vector2(-Mathf.Round(scroll.x), -Mathf.Round(scroll.y));

        /// <summary>GUI.BeginScrollView + konum takibi.</summary>
        Vector2 BeginScroll(Rect view, Vector2 scroll, Rect content)
        {
            scroll = GUI.BeginScrollView(view, scroll, content);
            var clip = view;
            if (content.height > view.height) clip.width -= GUI.skin.verticalScrollbar.fixedWidth;
            PushFrame(clip, ScrollShift(scroll) - content.position);
            return scroll;
        }

        void EndScroll() { GUI.EndScrollView(); PopFrame(); }

        // GUILayout kaydırma görünümünün yeri ancak çizildikten sonra belli oluyor; bir önceki karedekini kullanıyoruz.
        readonly Dictionary<int, Rect> layoutScrollRects = new Dictionary<int, Rect>();

        Vector2 BeginLayoutScroll(int id, Vector2 scroll)
        {
            scroll = GUILayout.BeginScrollView(scroll);
            layoutScrollRects.TryGetValue(id, out var view);
            PushFrame(view, ScrollShift(scroll));
            return scroll;
        }

        void EndLayoutScroll(int id)
        {
            GUILayout.EndScrollView();
            PopFrame();
            if (Event.current.type == EventType.Repaint) layoutScrollRects[id] = GUILayoutUtility.GetLastRect();
        }

        /// <summary>Yalnızca yazı (arka plan yok), ekran çözünürlüğünde.</summary>
        void CrispText(Rect r, string text, GUIStyle s)
        {
            if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
            var o = Origin;
            var sr = new Rect((r.x + o.x) * scale, (r.y + o.y) * scale, r.width * scale, r.height * scale);
            if (frames.Count == 0)
            {
                FlushText();
                var m = GUI.matrix;
                GUI.matrix = Matrix4x4.identity;
                GUI.Label(sr, ScaleTags(text), Crisp(s));
                GUI.matrix = m;
                return;
            }
            var c = ClipAbs;
            if (c.width <= 0 || c.height <= 0) return;
            pendingText.Add(new PendingText
            {
                screen = sr,
                clip = new Rect(c.x * scale, c.y * scale, c.width * scale, c.height * scale),
                text = ScaleTags(text),
                style = Crisp(s),
                color = GUI.color,
                content = GUI.contentColor,
                enabled = GUI.enabled
            });
        }

        /// <summary>Sıradaki yazıları çizer. Yalnızca hiçbir alanın içinde değilken çalışır.</summary>
        void FlushText()
        {
            if (pendingText.Count == 0 || frames.Count > 0) return;
            if (Event.current.type != EventType.Repaint) { pendingText.Clear(); return; }
            var m = GUI.matrix;
            var col = GUI.color;
            var cc = GUI.contentColor;
            bool en = GUI.enabled;
            GUI.matrix = Matrix4x4.identity;
            foreach (var p in pendingText)
            {
                GUI.color = p.color;
                GUI.contentColor = p.content;
                GUI.enabled = p.enabled;
                GUI.BeginClip(p.clip);
                GUI.Label(new Rect(p.screen.x - p.clip.x, p.screen.y - p.clip.y, p.screen.width, p.screen.height), p.text, p.style);
                GUI.EndClip();
            }
            pendingText.Clear();
            GUI.matrix = m;
            GUI.color = col;
            GUI.contentColor = cc;
            GUI.enabled = en;
        }

        void Label(Rect r, string text) => Label(r, text, GUI.skin.label);

        /// <summary>GUI.Button yerine: arka plan ve tıklama IMGUI'de, yazı net çizilir.</summary>
        bool Button(Rect r, string text, GUIStyle s)
        {
            if (Native) return GUI.Button(r, text, s);
            bool clicked = GUI.Button(r, GUIContent.none, s);
            CrispText(r, text, s);
            return clicked;
        }

        bool Button(Rect r, GUIContent content, GUIStyle s) => GUI.Button(r, content, s);

        void LLabel(string text, GUIStyle s, params GUILayoutOption[] o)
        {
            var r = GUILayoutUtility.GetRect(new GUIContent(text), s, o);
            Label(r, text, s);
        }

        void LLabel(string text, params GUILayoutOption[] o) => LLabel(text, GUI.skin.label, o);

        bool LButton(string text, GUIStyle s, params GUILayoutOption[] o)
        {
            var r = GUILayoutUtility.GetRect(new GUIContent(text), s, o);
            return Button(r, text, s);
        }

        /// <summary>Yazısı görünmez yapılmış kopya: alanın kendisini IMGUI çizer, yazıyı biz net çizeriz.</summary>
        GUIStyle HiddenText(GUIStyle s)
        {
            if (!hiddenStyles.TryGetValue(s, out var h))
            {
                h = new GUIStyle(s);
                foreach (var st in new[] { h.normal, h.hover, h.active, h.focused, h.onNormal, h.onHover, h.onActive, h.onFocused })
                    st.textColor = new Color(0, 0, 0, 0);
                hiddenStyles[s] = h;
            }
            return h;
        }

        string Field(Rect r, string v, int max, GUIStyle s)
        {
            if (Native) return GUI.TextField(r, v, max, s);
            var nv = GUI.TextField(r, v, max, HiddenText(s));
            CrispText(r, nv, s);
            return nv;
        }

        string LField(string v, int max, GUIStyle s, params GUILayoutOption[] o)
        {
            var r = GUILayoutUtility.GetRect(new GUIContent(v), s, o);
            return Field(r, v, max, s);
        }

        // =====================================================================
        // Simgeler

        /// <summary>Kaynak rengi zeminli, yuvarlak köşeli küçük pul ve üstünde siluet.</summary>
        void ResIcon(Rect r, int res)
        {
            var old = GUI.color;
            GUI.color = Pal.Ink;
            GUI.DrawTexture(r, Icons.Card(), ScaleMode.StretchToFill, true);
            GUI.color = Pal.Res[res];
            float b = Mathf.Max(1f, r.width * 0.07f);
            GUI.DrawTexture(new Rect(r.x + b, r.y + b, r.width - 2 * b, r.height - 2 * b), Icons.Card(), ScaleMode.StretchToFill, true);
            GUI.color = old;
            float p = r.width * 0.14f;
            GUI.DrawTexture(new Rect(r.x + p, r.y + p, r.width - 2 * p, r.height - 2 * p), Icons.Glyph(res), ScaleMode.ScaleToFit, true);
        }

        /// <summary>"2 [simge] 1 [simge]" şeklinde kart listesi. Bitiş x'ini döndürür.</summary>
        float CardsRow(float x, float y, int[] cards, float size, GUIStyle numStyle = null, string empty = "hiçbir şey")
        {
            numStyle = numStyle ?? Sty.small;
            bool any = false;
            for (int i = 0; i < T.ResCount && cards != null && i < cards.Length; i++)
            {
                if (cards[i] <= 0) continue;
                any = true;
                if (cards[i] > 1)
                {
                    string n = cards[i].ToString();
                    float nw = numStyle.CalcSize(new GUIContent(n)).x;
                    Label(new Rect(x, y, nw + 2, size), n, MidLeft(numStyle));
                    x += nw + 1;
                }
                ResIcon(new Rect(x, y, size, size), i);
                x += size + 5;
            }
            if (!any)
            {
                Label(new Rect(x, y, 120, size), empty, MidLeft(Sty.dim));
                x += Sty.dim.CalcSize(new GUIContent(empty)).x;
            }
            return x;
        }

        readonly Dictionary<GUIStyle, GUIStyle> midLeft = new Dictionary<GUIStyle, GUIStyle>();

        GUIStyle MidLeft(GUIStyle s)
        {
            if (!midLeft.TryGetValue(s, out var m))
            {
                m = new GUIStyle(s) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
                midLeft[s] = m;
            }
            return m;
        }

        // =====================================================================
        // Simgeli akan metin: "Ali aldı: 2 Odun, 1 Koyun" → "Ali aldı: 2 [odun], 1 [koyun]"

        static readonly Regex ResWords = new Regex(@"(?:(\d+) )?(Odun|Tuğla|Koyun|Buğday|Taş)(?![a-zçğıöşü])");

        struct FlowItem { public Rect r; public string text; public int res, count; }

        class FlowLayout { public float width, height; public List<FlowItem> items = new List<FlowItem>(); }

        readonly Dictionary<(string, GUIStyle, int), FlowLayout> flowCache = new Dictionary<(string, GUIStyle, int), FlowLayout>();

        static bool HasRes(string text) => text != null && ResWords.IsMatch(text);

        FlowLayout Flow(string text, GUIStyle st, float width)
        {
            var key = (text, st, Mathf.RoundToInt(width));
            if (flowCache.TryGetValue(key, out var fl)) return fl;
            if (flowCache.Count > 800) flowCache.Clear();

            fl = new FlowLayout { width = width };
            float lineH = st.CalcSize(new GUIContent("Ag")).y;
            float icon = Mathf.Round(lineH * 0.95f);
            float x = 0, y = 0;
            float space = st.CalcSize(new GUIContent("a a")).x - st.CalcSize(new GUIContent("aa")).x;

            void Place(float w, FlowItem it)
            {
                if (x > 0 && x + w > width) { x = 0; y += lineH; }
                it.r = new Rect(x, y, w, lineH);
                fl.items.Add(it);
                x += w;
            }

            void Words(string s)
            {
                int i = 0;
                while (i < s.Length)
                {
                    int j = s.IndexOf(' ', i);
                    string w = j < 0 ? s.Substring(i) : s.Substring(i, j - i);
                    if (w.Length > 0) Place(st.CalcSize(new GUIContent(w)).x, new FlowItem { text = w, res = -1 });
                    if (j < 0) break;
                    x += space;
                    i = j + 1;
                }
            }

            int at = 0;
            foreach (Match m in ResWords.Matches(text))
            {
                Words(text.Substring(at, m.Index - at));
                int res = System.Array.IndexOf(T.Res, m.Groups[2].Value);
                int count = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0;
                // "3 Odun" -> üç odun simgesi yan yana
                Place(IconRunWidth(count, icon), new FlowItem { res = res, count = count });
                at = m.Index + m.Length;
            }
            Words(text.Substring(at));
            fl.height = y + lineH;
            return fl;
        }

        /// <summary>k simgelik dizide simgeler arası adım: az ise aralıklı, çoksa üst üste biner.</summary>
        static float IconRunStep(int k, float icon) => k <= 4 ? icon + 2 : icon * 0.6f;

        static float IconRunWidth(int count, float icon)
        {
            int k = Mathf.Max(1, count);
            return icon + IconRunStep(k, icon) * (k - 1);
        }

        float FlowHeight(string text, GUIStyle st, float width) =>
            HasRes(text) ? Flow(text, st, width).height : st.CalcHeight(new GUIContent(text), width);

        /// <summary>Metni çizer; kaynak adlarının yerine simge koyar. Yüksekliği döndürür.</summary>
        float FlowLabel(Rect r, string text, GUIStyle st)
        {
            if (!HasRes(text))
            {
                float h = st.CalcHeight(new GUIContent(text), r.width);
                Label(new Rect(r.x, r.y, r.width, h), text, st);
                return h;
            }
            var fl = Flow(text, st, r.width);
            var plain = MidLeft(st);
            foreach (var it in fl.items)
            {
                var ir = new Rect(r.x + it.r.x, r.y + it.r.y, it.r.width, it.r.height);
                if (it.res < 0) { Label(new Rect(ir.x, ir.y, ir.width + 4, ir.height), it.text, plain); continue; }
                float icon = Mathf.Round(ir.height * 0.95f);
                int k = Mathf.Max(1, it.count);
                float step = IconRunStep(k, icon);
                for (int j = 0; j < k; j++)
                    ResIcon(new Rect(ir.x + j * step, ir.y + (ir.height - icon) / 2, icon, icon), it.res);
            }
            return fl.height;
        }
    }
}
