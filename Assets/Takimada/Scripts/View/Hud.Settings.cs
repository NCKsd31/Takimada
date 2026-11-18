using System.Collections.Generic;
using UnityEngine;

namespace Takimada
{
    public partial class Hud
    {
        // =====================================================================
        // Ayarlar (çark düğmesi): ses, ekran modu, çözünürlük, parlaklık, dikey senkron

        bool settingsOpen;
        List<Vector2Int> resList;

        void OpenSettings()
        {
            settingsOpen = true;
            resList = Settings.Resolutions();
        }

        void CloseSettings()
        {
            settingsOpen = false;
            app.settings.Save();
        }

        /// <summary>Çark simgeli küçük düğme; tıklanınca ayarları açar/kapatır.</summary>
        void GearButton(Rect r)
        {
            if (Button(Block(r), GUIContent.none, Sty.btnSmall))
            {
                if (settingsOpen) CloseSettings(); else OpenSettings();
            }
            float p = Mathf.Round(r.height * 0.16f);
            var old = GUI.color;
            GUI.color = settingsOpen ? Sty.Brass : Sty.Text;
            GUI.DrawTexture(new Rect(r.x + p, r.y + p, r.width - 2 * p, r.height - 2 * p), Icons.Gear(), ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        void DrawSettings()
        {
            var st = app.settings;
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { CloseSettings(); e.Use(); return; }

            var r = Modal(560, Application.isEditor ? 486 : 462, "Ayarlar");
            float x0 = r.x + 24, cx = x0 + 150, cw = r.width - 48 - 150 - 58, vx = cx + cw + 10;
            float y = r.y + 56;
            const float RowH = 36;
            bool changed = false;

            float SliderRow(string label, float v, float min, float max)
            {
                Label(new Rect(x0, y, 150, 28), label, MidLeft(Sty.label));
                float nv = GUI.HorizontalSlider(new Rect(cx, y + 9, cw, 16), v, min, max);
                Label(new Rect(vx, y, 50, 28), Mathf.RoundToInt(nv * 100) + "%", MidLeft(Sty.label));
                if (!Mathf.Approximately(nv, v)) changed = true;
                y += RowH;
                return nv;
            }

            // seçeneklerden biri vurgulu düğme dizisi; tıklanan seçeneğin sırasını döndürür (yoksa -1)
            int Choice(string label, string[] options, int selected)
            {
                Label(new Rect(x0, y, 150, 28), label, MidLeft(Sty.label));
                float total = cw + 58, gap = 6, bw = (total - gap * (options.Length - 1)) / options.Length;
                int clicked = -1;
                for (int i = 0; i < options.Length; i++)
                    if (Btn(new Rect(cx + i * (bw + gap), y, bw, 28), options[i], true, i == selected ? Sty.btnOn : Sty.btnSmall) && i != selected)
                        clicked = i;
                y += RowH;
                return clicked;
            }

            Label(new Rect(x0, y, 200, 24), "Ses", Sty.h2);
            y += 28;
            st.master = SliderRow("Ana ses", st.master, 0f, 1f);
            st.music = SliderRow("Müzik", st.music, 0f, 1f);
            int mute = Choice("Sesler", new[] { "Açık", "Kapalı" }, st.muted ? 1 : 0);
            if (mute >= 0) { st.muted = mute == 1; changed = true; }

            y += 6;
            Sty.Fill(new Rect(x0, y, r.width - 48, 1), Sty.Line);
            y += 10;
            Label(new Rect(x0, y, 200, 24), "Görüntü", Sty.h2);
            y += 28;

            int mode = Choice("Ekran modu", new[] { "Tam ekran", "Kenarlıksız", "Pencereli" }, (int)st.mode);
            if (mode >= 0)
            {
                var cur = st.Current();
                st.mode = (WindowMode)mode;
                SetDisplay(cur);
            }

            // çözünürlük: ‹ küçült · › büyüt (liste büyükten küçüğe)
            Label(new Rect(x0, y, 150, 28), "Çözünürlük", MidLeft(Sty.label));
            if (resList == null || resList.Count == 0) resList = Settings.Resolutions();
            var now = st.Current();
            int idx = resList.IndexOf(now);
            if (idx < 0)
            {
                idx = 0;
                for (int i = 0; i < resList.Count; i++) if (resList[i].x * resList[i].y >= now.x * now.y) idx = i;
            }
            float total2 = cw + 58;
            if (Btn(new Rect(cx, y, 34, 28), "‹", idx < resList.Count - 1, Sty.btnSmall)) SetDisplay(resList[idx + 1]);
            if (Btn(new Rect(cx + total2 - 34, y, 34, 28), "›", idx > 0, Sty.btnSmall)) SetDisplay(resList[idx - 1]);
            bool native = now == Settings.Native();
            Label(new Rect(cx + 34, y, total2 - 68, 28), now.x + " × " + now.y + (native ? "  <size=11>(ekranın kendi)</size>" : ""), Sty.center);
            y += RowH;

            float bright = SliderRow("Parlaklık", st.brightness, Settings.MinBright, Settings.MaxBright);
            st.brightness = Mathf.Abs(bright - 1f) < 0.025f ? 1f : bright; // %100'e yapışsın

            int vs = Choice("Dikey senkron", new[] { "Açık", "Kapalı" }, st.vsync ? 0 : 1);
            if (vs >= 0) { st.vsync = vs == 0; st.ApplyVsync(); }

            if (Application.isEditor)
            {
                Label(new Rect(x0, y, r.width - 48, 20), "Editörde ekran modu ve çözünürlük uygulanmaz; build'de çalışır.", Sty.dim);
                y += 24;
            }

            if (changed) app.ApplySettings();

            float by = r.yMax - 58;
            if (Btn(new Rect(x0, by, 150, 38), "Varsayılanlar", true, Sty.btnSmall)) { st.ResetDefaults(); st.ApplyVsync(); app.ApplySettings(); }
            if (Btn(new Rect(r.xMax - 24 - 140, by, 140, 38), "Kapat", true, Sty.btnAccent)) CloseSettings();
        }

        void SetDisplay(Vector2Int res)
        {
            var st = app.settings;
            st.hasDisplay = true;
            st.width = res.x; st.height = res.y;
            st.ApplyDisplay();
            st.Save();
        }
    }
}
