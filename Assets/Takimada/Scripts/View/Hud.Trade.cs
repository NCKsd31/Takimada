using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Takimada
{
    /// <summary>
    /// Ticaret teklifinin görseli: üstte teklif sahibi; aşağı okun ucunda verdikleri, altta yukarı okla
    /// gelen alacakları. Gelen teklifler ekranın ortasında açılır; reddedilince kapanır, kabul edilince kalır.
    /// </summary>
    public partial class Hud
    {
        // ekranda açık duran teklifler (sırayla gösterilir) ve açıldıkları an (kısa vurgu için)
        readonly List<int> popOffers = new List<int>();
        readonly Dictionary<int, float> popAt = new Dictionary<int, float>();

        void PopOffer(int id)
        {
            if (!popOffers.Contains(id)) popOffers.Add(id);
            popAt[id] = Time.realtimeSinceStartup;
        }

        void HideOffer(int id)
        {
            popOffers.Remove(id);
            popAt.Remove(id);
        }

        /// <summary>Yeni durum geldiğinde: yeni gelen teklifi ekrana aç, biten teklifleri kapat.</summary>
        void OfferPopups(GameState prev, GameState s)
        {
            popOffers.RemoveAll(id => !s.offers.Exists(o => o.id == id));
            if (prev == null) return;
            int me = app.Me;
            bool notify = false;
            foreach (var o in s.offers)
            {
                var old = prev.offers.Find(x => x.id == o.id);
                if (app.isLocal)
                {
                    // tek ekranda herkes aynı yerde: her yeni teklif açılır
                    if (old == null) { PopOffer(o.id); notify = true; }
                    continue;
                }
                if (me < 0) continue;
                if (old == null && o.from != me && Rules.OfferParty(s, o, me)) { PopOffer(o.id); notify = true; }
                // herkese açık teklifimi biri kabul etti: onaylamam için öne getir
                if (o.from == me && o.accepted.Exists(q => old == null || !old.accepted.Contains(q))) { PopOffer(o.id); notify = true; }
            }
            if (notify) app.sfx?.Notify();
        }

        /// <summary>Teklif kartındaki düğmeler (yan panel ve açılır pencere aynı düğmeleri kullanır).</summary>
        List<(string label, bool on, bool primary, Action act)> OfferButtons(GameState s, int me, TradeOffer o)
        {
            var buttons = new List<(string label, bool on, bool primary, Action act)>();
            var parties = Rules.OfferParties(s, o);
            int id = o.id, from = o.from;
            if (app.isLocal)
            {
                foreach (var p in parties)
                {
                    if (o.rejected.Contains(p)) continue;
                    int pp = p;
                    buttons.Add((Esc(s.players[p].name) + " ile takas", Util.Has(s.players[p].res, o.get), true, () => LocalTrade(id, from, pp)));
                }
                buttons.Add(("İptal", true, false, () => { HideOffer(id); app.Send(new GameAction { type = Act.CancelOffer, target = id }, from); }));
            }
            else if (o.from == me)
            {
                foreach (var p in o.accepted)
                {
                    int pp = p;
                    buttons.Add((Esc(Name(s, p)) + " ile takas", true, true, () => app.Send(new GameAction { type = Act.ConfirmOffer, target = id, a = pp })));
                }
                buttons.Add(("İptal", true, false, () => { HideOffer(id); app.Send(new GameAction { type = Act.CancelOffer, target = id }); }));
            }
            else if (me >= 0 && parties.Contains(me) && !o.rejected.Contains(me))
            {
                bool has = Util.Has(s.players[me].res, o.get);
                bool acc = o.accepted.Contains(me);
                buttons.Add((acc ? "Kabul ettin" : has ? "Kabul et" : "Kartın yok", has && !acc, true, () => app.Send(new GameAction { type = Act.AcceptOffer, target = id })));
                buttons.Add(("Reddet", true, false, () => { HideOffer(id); app.Send(new GameAction { type = Act.RejectOffer, target = id }); }));
                buttons.Add(("Karşı teklif", true, false, () => OpenCounter(s, me, o)));
            }
            return buttons;
        }

        /// <summary>Kim kabul etti, kim reddetti (tek ekranda boş).</summary>
        string OfferStatus(GameState s, int me, TradeOffer o)
        {
            if (app.isLocal) return "";
            var parties = Rules.OfferParties(s, o);
            if (o.from != me && o.accepted.Contains(me))
                return "<color=#9BD3A5>Kabul ettin.</color> " + Esc(Name(s, o.from)) + " onaylayınca takas yapılır.";
            return string.Join(" · ", parties.Select(p => Esc(p == me ? "Sen" : Name(s, p)) + ": " +
                (o.accepted.Contains(p) ? "<color=#7CC47F>kabul</color>" : o.rejected.Contains(p) ? "<color=#E0675A>ret</color>" : "bekliyor")));
        }

        /// <summary>Oyuncu adı ve rengi; çevrim içi oyunda kendin "Sen" olarak görünürsün.</summary>
        (string name, Color col) PlayerEnd(GameState s, int me, int p) =>
            (!app.isLocal && p == me ? "Sen" : Name(s, p), Pal.PlayerColor(s.players[p].color));

        // ---------------- çizim parçaları ----------------

        const float CardGap = 6;

        /// <summary>Kartların yan yana genişliği (aynı kaynaktan çoksa simgeler üst üste biner).</summary>
        static float CardsWidth(int[] cards, float ic)
        {
            float w = 0;
            int groups = 0;
            for (int i = 0; i < T.ResCount; i++)
                if (cards[i] > 0) { w += IconRunWidth(cards[i], ic); groups++; }
            return groups == 0 ? 0 : w + CardGap * (groups - 1);
        }

        /// <summary>Kartları merkeze ortalar; sığmazsa küçültür. Altına zemin rengi koyar ki ok çizgisi kesilsin.</summary>
        void CardsCentered(Vector2 c, int[] cards, float ic, float maxW, Color bg)
        {
            float w = CardsWidth(cards, ic);
            if (w > maxW && w > 0) { ic = Mathf.Max(10, ic * maxW / w); w = CardsWidth(cards, ic); }
            if (w <= 0)
            {
                // karşılıksız (hediye): okun ortasında küçük boş kart
                var e = new Rect(c.x - ic * 0.4f, c.y - ic * 0.55f, ic * 0.8f, ic * 1.1f);
                Sty.Fill(new Rect(e.x - 3, e.y - 3, e.width + 6, e.height + 6), bg);
                var old = GUI.color;
                GUI.color = Sty.Line;
                GUI.DrawTexture(e, Icons.Card());
                GUI.color = bg;
                GUI.DrawTexture(new Rect(e.x + 2, e.y + 2, e.width - 4, e.height - 4), Icons.Card());
                GUI.color = old;
                return;
            }
            float x = c.x - w / 2;
            Sty.Fill(new Rect(x - 4, c.y - ic / 2 - 3, w + 8, ic + 6), bg);
            for (int i = 0; i < T.ResCount; i++)
            {
                if (cards[i] <= 0) continue;
                float step = IconRunStep(cards[i], ic);
                for (int k = 0; k < cards[i]; k++) ResIcon(new Rect(x + k * step, c.y - ic / 2, ic, ic), i);
                x += IconRunWidth(cards[i], ic) + CardGap;
            }
        }

        /// <summary>a'dan b'ye yatay ya da dikey ok; ucu b'de.</summary>
        static void Arrow(Vector2 a, Vector2 b, Color col, float thick = 3, float head = 14)
        {
            bool vertical = Mathf.Abs(b.y - a.y) > Mathf.Abs(b.x - a.x);
            int dir = vertical ? (b.y > a.y ? 1 : 3) : (b.x > a.x ? 0 : 2);
            float len = vertical ? Mathf.Abs(b.y - a.y) : Mathf.Abs(b.x - a.x);
            if (len < 1) return;
            head = Mathf.Min(head, len * 0.6f);
            var d = (b - a) / len;
            var end = b - d * head * 0.7f; // gövde ucun içinde biter
            var line = vertical
                ? Rect.MinMaxRect(a.x - thick / 2, Mathf.Min(a.y, end.y), a.x + thick / 2, Mathf.Max(a.y, end.y))
                : Rect.MinMaxRect(Mathf.Min(a.x, end.x), a.y - thick / 2, Mathf.Max(a.x, end.x), a.y + thick / 2);
            Sty.Fill(line, col);
            Rect hr;
            switch (dir)
            {
                case 0: hr = new Rect(b.x - head, b.y - head / 2, head, head); break;
                case 1: hr = new Rect(b.x - head / 2, b.y - head, head, head); break;
                case 2: hr = new Rect(b.x, b.y - head / 2, head, head); break;
                default: hr = new Rect(b.x - head / 2, b.y, head, head); break;
            }
            var old = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(hr, Icons.ArrowHead(dir));
            GUI.color = old;
        }

        readonly Dictionary<GUIStyle, GUIStyle> midCenter = new Dictionary<GUIStyle, GUIStyle>();

        GUIStyle MidCenter(GUIStyle s)
        {
            if (!midCenter.TryGetValue(s, out var m))
            {
                m = new GUIStyle(s) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Clip };
                midCenter[s] = m;
            }
            return m;
        }

        /// <summary>Uzun adı "…" ile kısaltır.</summary>
        static string FitText(string text, GUIStyle st, float width)
        {
            if (st.CalcSize(new GUIContent(text)).x <= width) return text;
            for (int n = text.Length - 1; n > 1; n--)
            {
                string t = text.Substring(0, n) + "…";
                if (st.CalcSize(new GUIContent(t)).x <= width) return t;
            }
            return text.Substring(0, 1) + "…";
        }

        /// <summary>Oyuncu adı kutusu: koyu zemin, oyuncu renginde çerçeve ve ad.</summary>
        void NameChip(Rect r, string name, Color col, GUIStyle st)
        {
            Sty.Fill(r, Pal.Ink);
            Sty.Frame(r, col, 2);
            var style = MidCenter(st);
            Label(r, "<b>" + Esc(FitText(name, style, r.width - 10)) + "</b>", Sty.Colored(style, col));
        }

        /// <summary>
        /// Takas şeması, tek sütun: üstte oyuncu adı, ondan aşağı inen okun ucunda verdiği kartlar;
        /// altta alacağı kartlar ve onlara aşağıdan yukarı gelen ok.
        /// mine: teklifi yapan kendi teklifine bakıyor; sıra ters döner, önce istedikleri (aşağı okun ucunda),
        /// altta verdikleri (yukarı okla) görünür.
        /// </summary>
        void DrawExchange(Rect r, string aName, Color aCol, int[] aGives, int[] bGives, bool mine, float ic, Color bg, GUIStyle nameStyle)
        {
            if (mine) (aGives, bGives) = (bGives, aGives);
            const float Pad = 4; // kart sırasıyla ok ucu arası
            float chipH = Mathf.Clamp(r.height * 0.2f, 20, 30);
            float chipW = Mathf.Min(r.width * 0.76f, 240);
            var top = new Rect(r.x + (r.width - chipW) / 2, r.y, chipW, chipH);
            float rest = r.height - chipH;
            ic = Mathf.Min(ic, rest * 0.25f);
            float gap = Mathf.Max(8, rest * 0.1f);
            float arrowLen = Mathf.Max(8, (rest - 2 * ic - gap - 4 * Pad) / 2);
            float cx = r.x + r.width / 2;
            float giveY = top.yMax + arrowLen + Pad + ic / 2;
            float getY = giveY + ic + gap;
            float head = Mathf.Min(16, arrowLen * 0.7f);
            float maxW = r.width * 0.9f;

            Arrow(new Vector2(cx, top.yMax + 1), new Vector2(cx, giveY - ic / 2 - Pad), aCol, 4, head);
            Arrow(new Vector2(cx, r.yMax), new Vector2(cx, getY + ic / 2 + Pad), Sty.Text, 4, head);
            CardsCentered(new Vector2(cx, giveY), aGives, ic, maxW, bg);
            CardsCentered(new Vector2(cx, getY), bGives, ic, maxW, bg);
            NameChip(top, aName, aCol, nameStyle);
        }

        // ---------------- yan panel: tek teklif ----------------

        /// <summary>Yan paneldeki teklif kartı. Kartın alt kenarının y değerini döndürür.</summary>
        float DrawOffer(GameState s, int me, TradeOffer o, Rect at)
        {
            float x = at.x, w = at.width;
            var buttons = OfferButtons(s, me, o);
            string status = OfferStatus(s, me, o);

            float bx0 = x + 10, maxX = x + w - 6, fx = bx0;
            int lines = 1;
            var widths = buttons.Select(b => Mathf.Max(60, Sty.btnSmall.CalcSize(new GUIContent(b.label)).x + 14)).ToList();
            foreach (var bw in widths) { if (fx + bw > maxX && fx > bx0) { lines++; fx = bx0; } fx += bw + 4; }
            float statusH = status.Length > 0 ? Sty.small.CalcHeight(new GUIContent(status), w - 16) : 0;
            float headH = o.counterOf > 0 ? 18 : 0;
            const float ExH = 124;
            float rowH = 6 + headH + ExH + 6 + statusH + (buttons.Count > 0 ? lines * 26 : 0) + 4;

            var row = new Rect(x, at.y, w, rowH);
            Sty.Fill(row, Sty.Panel2);
            float y = row.y + 6;
            if (headH > 0) { Label(new Rect(row.x + 10, y - 3, w - 16, 18), "Karşı teklif", Sty.Colored(Sty.small, Sty.Brass)); y += headH; }
            var (aName, aCol) = PlayerEnd(s, me, o.from);
            DrawExchange(new Rect(row.x + 6, y, w - 12, ExH), aName, aCol, o.give, o.get, !app.isLocal && o.from == me, 22, Sty.Panel2, Sty.small);
            float by = y + ExH + 6;
            if (statusH > 0) { Label(new Rect(row.x + 10, by, w - 16, statusH), status, Sty.dim); by += statusH + 2; }
            fx = bx0;
            for (int k = 0; k < buttons.Count; k++)
            {
                if (fx + widths[k] > maxX && fx > bx0) { by += 26; fx = bx0; }
                if (Btn(new Rect(fx, by, widths[k], 22), buttons[k].label, buttons[k].on, Sty.btnSmall)) buttons[k].act();
                fx += widths[k] + 4;
            }
            return row.yMax;
        }

        // ---------------- ekranın ortasında açılan teklif ----------------

        void DrawOfferPopups(GameState s, int me)
        {
            if (s.phase != Phase.Main) return;
            var list = popOffers.Select(id => s.offers.Find(o => o.id == id))
                .Where(o => o != null && (app.isLocal || !o.rejected.Contains(me)))
                .ToList();
            if (list.Count == 0) return;
            float cx = 290 + (sw - 290 - 324) / 2;
            DrawOfferPopup(s, me, list[0], cx, 96, list.Count - 1);
        }

        void DrawOfferPopup(GameState s, int me, TradeOffer o, float cx, float top, int more)
        {
            const float W = 420, ExH = 200, Pad = 18;
            var buttons = OfferButtons(s, me, o);
            string status = OfferStatus(s, me, o);
            float statusH = status.Length > 0 ? Sty.small.CalcHeight(new GUIContent(status), W - Pad * 2) : 0;
            float h = 14 + 28 + 10 + ExH + 10 + (statusH > 0 ? statusH + 8 : 0) + (buttons.Count > 0 ? 36 : 0) + 14;
            var r = new Rect(cx - W / 2, top, W, h);

            Panel(r);
            var fromCol = Pal.PlayerColor(s.players[o.from].color);
            Sty.Fill(new Rect(r.x, r.y, r.width, 4), fromCol);
            // yeni açıldıysa kısa bir süre çerçeve parlar
            float age = popAt.TryGetValue(o.id, out var t0) ? Time.realtimeSinceStartup - t0 : 99f;
            if (age < 1.6f)
            {
                float k = 0.5f + 0.5f * Mathf.Cos(age * Mathf.PI * 2.5f);
                Sty.Frame(r, new Color(Sty.Brass.r, Sty.Brass.g, Sty.Brass.b, k * (1f - age / 1.6f)), 3);
            }

            string title = o.counterOf > 0 ? "Karşı teklif" : "Ticaret teklifi";
            if (more > 0) title += "  <size=13><color=#9FB0B5>+" + more + " teklif daha</color></size>";
            Label(new Rect(r.x + Pad, r.y + 14, W - Pad * 2 - 80, 28), title, Sty.h2);
            int id = o.id;
            if (Btn(new Rect(r.xMax - Pad - 70, r.y + 14, 70, 24), "Gizle", true, Sty.btnSmall)) HideOffer(id);

            float y = r.y + 14 + 28 + 10;
            var (aName, aCol) = PlayerEnd(s, me, o.from);
            DrawExchange(new Rect(r.x + Pad, y, W - Pad * 2, ExH), aName, aCol, o.give, o.get, !app.isLocal && o.from == me, 34, Sty.Panel, Sty.label);
            y += ExH + 10;

            if (statusH > 0)
            {
                Label(new Rect(r.x + Pad, y, W - Pad * 2, statusH), status, Sty.Colored(SmallCenter(), Sty.Dim));
                y += statusH + 8;
            }

            if (buttons.Count > 0)
            {
                const float Gap = 8;
                var widths = buttons.Select(b => Mathf.Max(96, Sty.btn.CalcSize(new GUIContent(b.label)).x + 20)).ToList();
                float total = widths.Sum() + Gap * (widths.Count - 1);
                float sc = total > W - Pad * 2 ? (W - Pad * 2 - Gap * (widths.Count - 1)) / widths.Sum() : 1f;
                float bx = cx - Mathf.Min(total, W - Pad * 2) / 2;
                for (int k = 0; k < buttons.Count; k++)
                {
                    float bw = widths[k] * sc;
                    var b = buttons[k];
                    var style = b.primary && b.on ? Sty.btnAccent : Sty.btn;
                    if (Btn(new Rect(bx, y, bw, 32), b.label, b.on, style)) b.act();
                    bx += bw + Gap;
                }
            }
        }
    }
}
