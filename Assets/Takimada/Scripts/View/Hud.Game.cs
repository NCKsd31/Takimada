using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Takimada
{
    public partial class Hud
    {
        int[] discardSel = new int[5];
        int[] yopSel = new int[5];
        bool yopOpen, monoOpen, tradeOpen, rulesOpen, confirmQuit;
        int[] tGive = new int[5], tGet = new int[5];
        int tTarget = -1, tCounter = -1;
        Vector2 logScroll;
        int lastLogCount;
        string lastLogLine;
        string chat = "";

        // kayıt satırı yükseklikleri: her karede yüzlerce satırı yeniden ölçmemek için
        readonly Dictionary<string, float> logHeights = new Dictionary<string, float>();
        float logHeightsW = -1;
        GUIStyle logHeightsStyle;

        float LogLineHeight(string line, float w)
        {
            if (w != logHeightsW || logHeightsStyle != Sty.small || logHeights.Count > 1000)
            {
                logHeights.Clear(); logHeightsW = w; logHeightsStyle = Sty.small;
            }
            if (!logHeights.TryGetValue(line, out float h)) { h = FlowHeight(line, Sty.small, w) + 2; logHeights[line] = h; }
            return h;
        }

        // sohbet satırlarının hazır metinleri ve yükseklikleri
        string[] chatTexts = new string[0];
        float[] chatHeights = new float[0];
        float chatTotal, chatCacheW = -1;
        int chatCacheVersion = -1;
        GUIStyle chatCacheStyle;

        void ResetDialogs()
        {
            yopOpen = monoOpen = tradeOpen = rulesOpen = confirmQuit = false;
            discardSel = new int[5]; yopSel = new int[5]; tGive = new int[5]; tGet = new int[5];
            tTarget = -1; tCounter = -1;
            popOffers.Clear(); popAt.Clear();
        }

        bool BlockingModal()
        {
            var s = app.state;
            if (settingsOpen) return true;
            if (app.view != View.Game || s == null) return false;
            int me = app.Me;
            if (s.phase == Phase.GameOver) return true;
            if (s.phase == Phase.Discard && me >= 0 && s.discardPending[me] > 0) return true;
            if (s.phase == Phase.Steal && me == s.current) return true;
            return yopOpen || monoOpen || tradeOpen || rulesOpen;
        }

        string Name(GameState s, int p) => p >= 0 && p < s.players.Count ? s.players[p].name : "?";

        // =====================================================================

        void DrawGame()
        {
            var s = app.state;
            if (s == null) return;
            int me = app.Me;
            DrawChat(DrawPlayers(s, me) + 8);
            DrawBanner(s, me);
            DrawBottom(s, me);
            DrawSide(s, me);
            // gelen teklif ekranın ortasında; bir pencere açıkken onun altında kalıp tıklamayı çalmasın diye çizilmez
            if (!BlockingModal()) DrawOfferPopups(s, me);

            if (s.phase == Phase.GameOver) { DrawGameOver(s); return; }
            if (s.phase == Phase.Discard && me >= 0 && s.discardPending[me] > 0) DrawDiscard(s, me);
            else if (s.phase == Phase.Steal && me == s.current) DrawSteal(s, me);
            else if (yopOpen) DrawYop(s, me);
            else if (monoOpen) DrawMono(s, me);
            else if (tradeOpen) DrawTrade(s, me);
            else if (rulesOpen) DrawRulesModal();
        }

        // ---------------- oyuncu listesi ----------------

        float DrawPlayers(GameState s, int me)
        {
            int n = s.players.Count;
            float rowH = 84;
            var r = new Rect(12, 12, 266, 40 + n * (rowH + 6));
            Panel(r);
            Label(new Rect(r.x + 14, r.y + 8, 200, 24), "Kaptanlar · hedef " + s.cfg.victoryPoints, Sty.h2);
            int actor = Rules.Actor(s);
            for (int p = 0; p < n; p++)
            {
                var pl = s.players[p];
                var row = new Rect(r.x + 8, r.y + 36 + p * (rowH + 6), r.width - 16, rowH);
                Sty.Fill(row, Sty.Panel2);
                if (p == actor && s.phase != Phase.GameOver) Sty.Frame(row, Sty.Brass, 2);
                Sty.Fill(new Rect(row.x, row.y, 6, row.height), Pal.PlayerColor(pl.color));

                int vp = p == me || s.phase == Phase.GameOver ? Rules.TotalVP(s, p) : Rules.PublicVP(s, p);
                string tag = (p == me && !app.isLocal ? "  (sen)" : "") + (!pl.connected ? "  · koptu" : "");
                Label(new Rect(row.x + 14, row.y + 5, row.width - 96, 24), "<b>" + Esc(pl.name) + "</b>" + tag, Sty.label);
                Label(new Rect(row.xMax - 84, row.y + 1, 76, 30), "<size=24><b>" + vp + "</b></size> <size=11>puan</size>", PlayerVpStyle());

                // simge şeridi: kaynak kartları · gelişim kartları · muhafız · en uzun yol
                int hand = Rules.HandSize(s, p);
                float x = row.x + 14, y = row.y + 34;
                bool risk = hand > s.cfg.discardLimit && s.cfg.robberEnabled;
                var cardRect = new Rect(x, y, 32, 44);
                DrawCardBack(cardRect, CardBackRes, risk ? Sty.Danger : Sty.Brass);
                Label(cardRect, hand.ToString(), Sty.Colored(Sty.centerBig, risk ? Sty.Danger : Sty.Text));
                x += 40;

                var devRect = new Rect(x, y, 32, 44);
                DrawCardBack(devRect, CardBackDev, Sty.Brass);
                DrawSymbol(new Rect(devRect.x + 5, devRect.y + 9, 22, 22), "cekic", Sty.Text);
                CountBadge(new Vector2(devRect.xMax - 2, devRect.yMax - 3), pl.dev.Count);
                x += 46;

                x = StatTile(x, y, "muhafiz", pl.knights, s.armyHolder == p, "M");
                StatTile(x + 4, y, "yol", pl.roadLength, s.longestHolder == p, "Y");
            }
            return r.yMax;
        }

        static readonly Color CardBackRes = Pal.Hex("#2D5C70"), CardBackDev = Pal.Hex("#5E3558");

        // DevCard sırasıyla: Muhafız, Zafer Puanı, Yol Yapımı, Bereket, Tekel
        static readonly string[] DevSymbol = { "muhafiz", "zafer", "yolyapimi", "bereket", "tekel" };

        /// <summary>
        /// Elindeki gelişim kartları: her tür için simgeli küçük kart ve adet rozeti. Yer varsa "Elde N kart"
        /// yazısının yanına, yoksa altına dizilir. Bu tur alınıp henüz oynanamayanlar soluk görünür.
        /// </summary>
        void DrawDevHand(GameState s, PlayerState pl, float infoX, float top, float xMax)
        {
            const float W = 34, H = 46, Gap = 8;
            var types = new List<int>();
            for (int t = 0; t < 5; t++) if (pl.dev.Exists(d => (int)d.type == t)) types.Add(t);
            if (types.Count == 0) return;
            float need = types.Count * (W + Gap);
            bool beside = infoX + 104 + need <= xMax;
            float x = beside ? infoX + 104 : infoX;
            float y = beside ? top + 8 : top + 30;
            var mouse = Event.current.mousePosition;
            int hover = -1, hoverTotal = 0, hoverNew = 0;
            Rect hoverRect = default;
            foreach (int t in types)
            {
                if (x + W > xMax) break;
                int total = 0, ready = 0;
                foreach (var d in pl.dev)
                    if ((int)d.type == t) { total++; if (s.cfg.playDevSameTurn || d.boughtTurn < s.turn) ready++; }
                var cr = new Rect(x, y, W, H);
                DrawCardBack(cr, CardBackDev, Sty.Brass);
                var ic = new Rect(cr.x + 5, cr.y + (H - (W - 10)) / 2, W - 10, W - 10);
                if (!DrawSymbol(ic, DevSymbol[t], Sty.Text)) Label(cr, T.Dev[t].Substring(0, 1), Sty.Colored(Sty.centerBig, Sty.Text));
                bool notYet = ready == 0 && t != (int)DevCard.Zafer;
                if (notYet)
                {
                    var old = GUI.color;
                    GUI.color = new Color(0, 0, 0, 0.5f);
                    GUI.DrawTexture(cr, Icons.Card());
                    GUI.color = old;
                }
                if (total > 1) CountBadge(new Vector2(cr.xMax - 2, cr.yMax - 3), total);
                if (cr.Contains(mouse)) { hover = t; hoverRect = cr; hoverTotal = total; hoverNew = total - ready; }
                x += W + Gap;
            }

            // üstünde durulan kartın adı
            if (hover >= 0)
            {
                string tag = T.Dev[hover] + (hoverTotal > 1 ? " ×" + hoverTotal : "");
                if (hover != (int)DevCard.Zafer && hoverNew > 0) tag += " · " + hoverNew + " tanesi bu tur alındı";
                float tw = Sty.label.CalcSize(new GUIContent(tag)).x + 20;
                var tr = new Rect(hoverRect.center.x - tw / 2, hoverRect.y - 30, tw, 24);
                Sty.Fill(tr, new Color(0.04f, 0.1f, 0.13f, 0.92f));
                Sty.Frame(tr, Sty.Brass, 1);
                Label(tr, tag, Sty.center);
            }
        }

        GUIStyle playerVpStyle;

        GUIStyle PlayerVpStyle()
        {
            if (playerVpStyle == null)
            {
                playerVpStyle = new GUIStyle(Sty.label) { alignment = TextAnchor.MiddleRight, richText = true };
                playerVpStyle.normal.textColor = Sty.Brass;
            }
            return playerVpStyle;
        }

        /// <summary>Arkası dönük kart: koyu kenar, renkli sırt, içte ince çerçeve.</summary>
        static void DrawCardBack(Rect r, Color body, Color frame)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.35f);
            GUI.DrawTexture(new Rect(r.x + 1.5f, r.y + 2.5f, r.width, r.height), Icons.Card());
            GUI.color = Pal.Ink;
            GUI.DrawTexture(r, Icons.Card());
            GUI.color = body;
            GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), Icons.Card());
            GUI.color = new Color(frame.r, frame.g, frame.b, 0.75f);
            GUI.DrawTexture(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), Icons.Card());
            GUI.color = body;
            GUI.DrawTexture(new Rect(r.x + 5.5f, r.y + 5.5f, r.width - 11, r.height - 11), Icons.Card());
            GUI.color = old;
        }

        /// <summary>Beyaz siluet simgesini verilen renkle çizer. Simge yoksa false.</summary>
        static bool DrawSymbol(Rect r, string name, Color col)
        {
            var tex = Icons.Symbol(name);
            if (tex == null) return false;
            var old = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
            GUI.color = old;
            return true;
        }

        /// <summary>Kartın köşesindeki yuvarlak adet rozeti (merkez verilen noktada).</summary>
        void CountBadge(Vector2 c, int n)
        {
            var b = new Rect(c.x - 10, c.y - 10, 20, 20);
            var old = GUI.color;
            GUI.color = Pal.Ink;
            GUI.DrawTexture(b, Icons.Card());
            GUI.color = Sty.Brass;
            GUI.DrawTexture(new Rect(b.x + 1.5f, b.y + 1.5f, b.width - 3, b.height - 3), Icons.Card());
            GUI.color = old;
            Label(b, n.ToString(), Sty.Colored(Sty.center, Pal.Ink));
        }

        /// <summary>Büyük simge + sayı. Ödülün sahibiyse pirinç zeminle vurgulanır. Bitiş x'ini döndürür.</summary>
        float StatTile(float x, float y, string symbol, int value, bool holder, string fallback)
        {
            var tile = new Rect(x, y, 66, 44);
            if (holder)
            {
                Sty.Fill(tile, new Color(Sty.Brass.r, Sty.Brass.g, Sty.Brass.b, 0.22f));
                Sty.Frame(tile, Sty.Brass, 2);
            }
            var col = holder ? Sty.Brass : Sty.Text;
            var ic = new Rect(tile.x + 3, tile.y + 4, 36, 36);
            if (!DrawSymbol(ic, symbol, col)) Label(ic, fallback, Sty.Colored(Sty.centerBig, col));
            Label(new Rect(ic.xMax, tile.y, tile.xMax - ic.xMax, tile.height), value.ToString(), Sty.Colored(Sty.centerBig, col));
            return tile.xMax;
        }

        static string Esc(string s) => (s ?? "").Replace("<", "‹").Replace(">", "›");

        // ---------------- üst bilgi ----------------

        void DrawBanner(GameState s, int me)
        {
            float w = Mathf.Min(620, sw - 290 - 324 - 20);
            var r = new Rect(290 + (sw - 290 - 324 - w) / 2, 12, w, 70);
            Panel(r);
            int actor = Rules.Actor(s);
            bool mine = me == actor;
            string who = Esc(Name(s, actor));
            string line;
            switch (s.phase)
            {
                case Phase.SetupSettlement: line = mine ? "Başlangıç köyünü yerleştir (parlayan noktalar)." : who + " başlangıç köyünü yerleştiriyor."; break;
                case Phase.SetupRoad: line = mine ? "Köyüne bitişik bir yol yerleştir." : who + " yolunu yerleştiriyor."; break;
                case Phase.Roll: line = mine ? "Zar at. İstersen önce bir gelişim kartı oyna." : who + " zar atacak."; break;
                case Phase.Discard:
                    line = me >= 0 && s.discardPending[me] > 0 ? s.discardPending[me] + " kart atmalısın." :
                        "Kart atılması bekleniyor: " + string.Join(", ", Enumerable.Range(0, s.players.Count).Where(p => s.discardPending[p] > 0).Select(p => Esc(Name(s, p))));
                    break;
                case Phase.MoveRobber: line = mine ? "Korsanı taşı: haritada bir karoya tıkla." : who + " korsanı taşıyor."; break;
                case Phase.Steal: line = mine ? "Kimden kart çalacağını seç." : who + " kart çalacak."; break;
                case Phase.Main: line = mine ? "Ticaret yap, inşa et, sonra turu bitir." : who + " oynuyor."; break;
                case Phase.RoadBuilding: line = mine ? s.freeRoads + " ücretsiz yol kaldı. Yerleştir veya bitir." : who + " ücretsiz yollarını yerleştiriyor."; break;
                case Phase.SpecialBuild: line = mine ? "Ek inşa sırası sende: inşa et ya da pas geç." : who + " ek inşa yapıyor."; break;
                default: line = "Oyun bitti."; break;
            }
            Sty.Fill(new Rect(r.x, r.y, 6, r.height), Pal.PlayerColor(actor >= 0 ? s.players[actor].color : 0));
            float left = TimerLeft(s);
            float textW = r.width - (left >= 0 ? 200 : 140);
            Label(new Rect(r.x + 18, r.y + 8, textW, 22), (mine && !app.isLocal ? "SIRA SENDE · " : "") + "Tur " + Math.Max(1, s.round) + " · " + T.Phase[(int)s.phase], Sty.Colored(Sty.small, Sty.Dim));
            Label(new Rect(r.x + 18, r.y + 28, textW, 36), line, Sty.Colored(Sty.wrap, mine ? Sty.Text : Sty.Dim));
            if (left >= 0) DrawTimer(s, r, left, mine || (s.phase == Phase.Discard && me >= 0 && s.discardPending[me] > 0));

            if (s.d1 > 0)
            {
                DrawDie(new Rect(r.xMax - 112, r.y + 12, 46, 46), s.d1, s.rolled);
                DrawDie(new Rect(r.xMax - 60, r.y + 12, 46, 46), s.d2, s.rolled);
            }
        }

        void DrawDie(Rect r, int v, bool fresh)
        {
            Sty.Fill(r, Pal.Ink);
            Sty.Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), fresh ? Pal.Token : Color.Lerp(Pal.Token, Sty.Panel2, 0.5f));
            var pip = new Color(0.1f, 0.08f, 0.06f);
            float c = r.width / 2, q = r.width / 4, d = 7;
            void P(float x, float y) => Sty.Fill(new Rect(r.x + x - d / 2, r.y + y - d / 2, d, d), pip);
            if (v == 1 || v == 3 || v == 5) P(c, c);
            if (v >= 2) { P(q, q); P(r.width - q, r.width - q); }
            if (v >= 4) { P(r.width - q, q); P(q, r.width - q); }
            if (v == 6) { P(q, c); P(r.width - q, c); }
        }

        // ---------------- alt panel: el ve hamleler ----------------

        Rect BottomRect() => new Rect(290, sh - 158, sw - 290 - 324, 146);

        void DrawBottom(GameState s, int me)
        {
            var r = BottomRect();
            Panel(r);
            if (me < 0) { Label(new Rect(r.x + 16, r.y + 16, r.width - 32, 30), "İzleyici", Sty.h2); return; }
            var pl = s.players[me];
            var hand = HandArea(r);
            DrawHand(pl.res, hand);
            float infoX = hand.xMax + 12;
            string who = app.isLocal ? "<b>" + Esc(pl.name) + "</b>\n" : "";
            Label(new Rect(infoX, r.y + 10, 100, 70), who + "Elde " + Util.Sum(pl.res) + " kart", Sty.small);
            if (s.cfg.devCardsEnabled) DrawDevHand(s, pl, infoX, r.y, r.xMax - 10);

            int actor = Rules.Actor(s);
            bool myTurn = me == actor;
            bool canBuild = myTurn && Rules.IsBuildPhase(s);
            bool main = myTurn && s.phase == Phase.Main;
            var c = s.cfg;

            // kalan taş sayısı butonun etiketinin yanında, pirinç renkte
            string Left(int n) => "<color=#" + ColorUtility.ToHtmlStringRGB(n > 0 ? Sty.Brass : Sty.Danger) + ">" + n + "</color>";

            var acts = new List<(string label, int[] cost, bool on, bool active, Action act, GUIStyle style)>();
            if (s.phase == Phase.Roll)
                acts.Add(("Zar at", null, myTurn, false, () => app.Send(new GameAction { type = Act.Roll }), Sty.btnAccent));
            acts.Add(("Yol " + Left(pl.roadsLeft), c.roadCost, canBuild && Util.Has(pl.res, c.roadCost) && pl.roadsLeft > 0, build == BuildMode.Road,
                () => ToggleBuild(BuildMode.Road), null));
            acts.Add(("Köy " + Left(pl.settlementsLeft), c.settlementCost, canBuild && Util.Has(pl.res, c.settlementCost) && pl.settlementsLeft > 0, build == BuildMode.Settlement,
                () => ToggleBuild(BuildMode.Settlement), null));
            acts.Add(("Kasaba " + Left(pl.citiesLeft), c.cityCost, canBuild && Util.Has(pl.res, c.cityCost) && pl.citiesLeft > 0, build == BuildMode.City,
                () => ToggleBuild(BuildMode.City), null));
            if (c.devCardsEnabled)
                acts.Add(("Kart al", c.devCost, canBuild && Util.Has(pl.res, c.devCost) && s.devDeckCount > 0, false,
                    () => app.Send(new GameAction { type = Act.BuyDev }), null));
            if (c.playerTrade || c.bankTrade)
                acts.Add(("Ticaret", null, s.phase == Phase.Main && (main || c.playerTrade), tradeOpen, () => { if (tradeOpen) CloseTrade(); else tradeOpen = true; }, null));
            if (s.phase == Phase.SpecialBuild)
                acts.Add(("Pas geç", null, myTurn, false, () => app.Send(new GameAction { type = Act.EndTurn }), Sty.btnAccent));
            else if (s.phase == Phase.RoadBuilding)
                acts.Add(("Yolları bitir", null, myTurn, false, () => app.Send(new GameAction { type = Act.EndTurn }), Sty.btnAccent));
            else
                acts.Add(("Turu bitir", null, main, false, () => { build = BuildMode.None; CloseTrade(); app.Send(new GameAction { type = Act.EndTurn }); }, Sty.btnAccent));

            float gap = 6, bw = Mathf.Min(156, (r.width - 28 - gap * (acts.Count - 1)) / acts.Count);
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                var br = new Rect(r.x + 14 + i * (bw + gap), r.y + 82, bw, 58);
                var st = a.style ?? (a.active ? Sty.btnOn : Sty.btn);
                bool enabled = a.on || a.active;
                if (a.cost == null) { if (Btn(br, a.label, enabled, st)) a.act(); continue; }

                // etiket üstte, maliyet simgeleri altta
                if (Btn(br, "", enabled, st)) a.act();
                var old = GUI.color;
                if (!enabled) GUI.color = new Color(1, 1, 1, 0.45f);
                Label(new Rect(br.x, br.y + 4, br.width, 20), a.label, CenterOf(st));
                float Ic = 28;
                float cw = CardsWidth(a.cost, Ic, Sty.label);
                if (cw > br.width - 10) { Ic = Mathf.Max(14, Ic * (br.width - 10) / cw); cw = CardsWidth(a.cost, Ic, Sty.label); } // dar ekranda butona sığsın
                CardsRow(br.x + (br.width - cw) / 2, br.y + 25, a.cost, Ic, Sty.label, "ücretsiz");
                GUI.color = old;
            }
        }

        readonly Dictionary<GUIStyle, GUIStyle> centerOf = new Dictionary<GUIStyle, GUIStyle>();

        GUIStyle CenterOf(GUIStyle s)
        {
            if (!centerOf.TryGetValue(s, out var c))
            {
                c = new GUIStyle(s) { alignment = TextAnchor.UpperCenter, padding = new RectOffset(0, 0, 0, 0), wordWrap = false, richText = true };
                foreach (var st in new[] { c.normal, c.hover, c.active, c.focused, c.onNormal, c.onHover, c.onActive, c.onFocused })
                {
                    st.background = null;
#if UNITY_EDITOR
                    st.scaledBackgrounds = new Texture2D[0]; // yalnızca editörde var (yüksek DPI arka planı)
#endif
                }
                centerOf[s] = c;
            }
            return c;
        }

        float CardsWidth(int[] cards, float size, GUIStyle numStyle)
        {
            float w = 0;
            bool any = false;
            for (int i = 0; i < T.ResCount; i++)
            {
                if (cards[i] <= 0) continue;
                any = true;
                if (cards[i] > 1) w += numStyle.CalcSize(new GUIContent(cards[i].ToString())).x + 1;
                w += size + 5;
            }
            return any ? w - 5 : Sty.dim.CalcSize(new GUIContent("ücretsiz")).x;
        }

        void ToggleBuild(BuildMode m) => build = build == m ? BuildMode.None : m;

        // ---------------- el: türlerine göre gruplanmış, yelpaze gibi tutulan kartlar ----------------

        const float CardW = 62, CardH = 88;
        const int MaxShownPerKind = 10;

        // kartlar alt panelin üstünden taşar, elde tutuluyormuş gibi
        Rect HandArea(Rect bottom) => new Rect(bottom.x + 12, bottom.y - 44, Mathf.Min(470, bottom.width * 0.6f), 132);

        struct HandCard { public int res; public Vector2 c; public float angle; public bool top; }

        /// <summary>
        /// Kartların yerleşimi: her kart ayrı, bir öncekinin üstüne yarı binerek dizilir (sol üst köşesi görünür),
        /// türler arasında küçük bir boşluk kalır, el hafifçe yelpaze gibi açılır.
        /// </summary>
        List<HandCard> HandLayout(int[] res, Rect area)
        {
            var cards = new List<HandCard>();
            const float Step = CardW * 0.56f, GroupGap = 8;
            var xs = new List<float>();
            float x = 0;
            int prevRes = -1;
            for (int r = 0; r < T.ResCount; r++)
            {
                int n = Mathf.Min(res[r], MaxShownPerKind);
                for (int k = 0; k < n; k++)
                {
                    if (cards.Count > 0) x += Step + (prevRes == r ? 0 : GroupGap);
                    xs.Add(x);
                    cards.Add(new HandCard { res = r, top = k == n - 1 });
                    prevRes = r;
                }
            }
            if (cards.Count == 0) return cards;
            float squeeze = x + CardW > area.width ? (area.width - CardW) / Mathf.Max(1, x) : 1f;
            float w = x * squeeze + CardW;
            float x0 = area.x + (area.width - w) / 2 + CardW / 2;
            int count = cards.Count;
            float spread = Mathf.Min(1f, (count - 1) / 6f); // az kartta yelpaze az açılır
            for (int i = 0; i < count; i++)
            {
                var hc = cards[i];
                float t = count == 1 ? 0 : i / (float)(count - 1) * 2 - 1;
                hc.angle = t * 7f * spread;
                hc.c = new Vector2(x0 + xs[i] * squeeze, area.y + 8 + CardH / 2 + t * t * 10f * spread);
                cards[i] = hc;
            }
            return cards;
        }

        /// <summary>Uçan sembollerin hedefi: o türün en üstteki kartı (yoksa olacağı yer).</summary>
        Vector2 HandCenter(int res)
        {
            var s = app.state;
            int me = app.Me;
            var area = HandArea(BottomRect());
            if (s == null || me < 0) return area.center;
            var counts = Util.Copy(s.players[me].res);
            if (counts[res] <= 0) counts[res] = 1;
            Vector2 at = area.center;
            foreach (var hc in HandLayout(counts, area)) if (hc.res == res) at = hc.c;
            return at;
        }

        /// <summary>Tek kart yüzü: koyu kenar, canlı kaynak rengi, sol üstte küçük, ortada büyük sembol.</summary>
        static void DrawCardFace(Rect rect, int res, Color tint)
        {
            var col = Pal.Res[res];
            GUI.color = new Color(0, 0, 0, 0.4f * tint.a);
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 4, rect.width, rect.height), Icons.Card());
            GUI.color = Pal.Ink * tint;
            GUI.DrawTexture(rect, Icons.Card());
            GUI.color = col * tint;
            GUI.DrawTexture(new Rect(rect.x + 2.5f, rect.y + 2.5f, rect.width - 5, rect.height - 5), Icons.Card());
            var inner = new Rect(rect.x + 6, rect.y + 25, rect.width - 12, rect.height - 31);
            GUI.color = Color.Lerp(col, Color.white, Pal.DarkRes(res) ? 0.1f : 0.22f) * tint;
            GUI.DrawTexture(inner, Icons.Card());
            GUI.color = tint;
            GUI.DrawTexture(new Rect(rect.x + 6, rect.y + 5, 18, 18), Icons.Glyph(res), ScaleMode.ScaleToFit, true);
            float g = Mathf.Min(inner.width, inner.height) - 2;
            GUI.DrawTexture(new Rect(inner.center.x - g / 2, inner.center.y - g / 2, g, g), Icons.Glyph(res), ScaleMode.ScaleToFit, true);
        }

        void DrawHand(int[] res, Rect area)
        {
            Block(area);
            var cards = HandLayout(res, area);
            if (cards.Count == 0)
            {
                Label(new Rect(area.x, area.y + 54, area.width, 30), "Elinde kart yok", Sty.Colored(Sty.center, Sty.Dim));
                return;
            }
            // farenin altındaki kart biraz yukarı kalkar
            var mouse = Event.current.mousePosition;
            int hover = -1;
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                var rr = new Rect(cards[i].c.x - CardW / 2, cards[i].c.y - CardH / 2, CardW, CardH);
                if (rr.Contains(mouse)) { hover = i; break; }
            }
            const float Lift = 16;
            var m = GUI.matrix;
            var old = GUI.color;
            for (int i = 0; i < cards.Count; i++)
            {
                var hc = cards[i];
                var c = hc.c + (i == hover ? new Vector2(0, -Lift) : Vector2.zero);
                GUIUtility.RotateAroundPivot(hc.angle, c);
                DrawCardFace(new Rect(c.x - CardW / 2, c.y - CardH / 2, CardW, CardH), hc.res, old);
                GUI.matrix = m;
            }
            GUI.color = old;

            // gösterilemeyen kartlar varsa grubun son kartına "+k" rozeti
            for (int i = 0; i < cards.Count; i++)
            {
                var hc = cards[i];
                int extra = res[hc.res] - MaxShownPerKind;
                if (!hc.top || extra <= 0) continue;
                var c = hc.c + (i == hover ? new Vector2(0, -Lift) : Vector2.zero);
                var b = new Rect(c.x + CardW / 2 - 18, c.y - CardH / 2 - 8, 28, 22);
                GUI.color = Pal.Ink;
                GUI.DrawTexture(b, Icons.Card());
                GUI.color = Sty.Brass;
                GUI.DrawTexture(new Rect(b.x + 2, b.y + 2, b.width - 4, b.height - 4), Icons.Card());
                GUI.color = old;
                Label(b, "+" + extra, Sty.Colored(Sty.center, Pal.Ink));
            }

            // üstünde durulan kartın türü ve adedi
            if (hover >= 0)
            {
                var hc = cards[hover];
                string tag = T.Res[hc.res] + " · " + res[hc.res];
                float tw = Sty.label.CalcSize(new GUIContent(tag)).x + 20;
                var tr = new Rect(hc.c.x - tw / 2, hc.c.y - CardH / 2 - Lift - 30, tw, 24);
                Sty.Fill(tr, new Color(0.04f, 0.1f, 0.13f, 0.92f));
                Sty.Frame(tr, Pal.Res[hc.res], 1);
                Label(tr, tag, Sty.center);
            }
        }

        // ---------------- sağ panel: kartlar, teklifler, kayıt ----------------

        void DrawSide(GameState s, int me)
        {
            var r = new Rect(sw - 312, 12, 300, sh - 24);
            Panel(r);
            float x = r.x + 14, w = r.width - 28, y = r.y + 10;

            // üst sıra: Kurallar · Oyundan çık · çark (ayarlar)
            GearButton(new Rect(x + w - 26, y, 26, 26));
            w -= 32;
            if (Btn(new Rect(x, y, (w - 6) / 2, 26), "Kurallar", true, Sty.btnSmall)) rulesOpen = !rulesOpen;
            if (!confirmQuit)
            {
                if (Btn(new Rect(x + (w + 6) / 2, y, (w - 6) / 2, 26), "Oyundan çık", true, Sty.btnSmall)) confirmQuit = true;
            }
            else
            {
                if (Btn(new Rect(x + (w + 6) / 2, y, (w - 6) / 4 - 2, 26), "Evet", true, Sty.btnSmall)) { confirmQuit = false; if (app.isHost && !app.isLocal) app.BackToLobby(); else app.Leave(); }
                if (Btn(new Rect(x + (w + 6) / 2 + (w - 6) / 4 + 2, y, (w - 6) / 4 - 2, 26), "Hayır", true, Sty.btnSmall)) confirmQuit = false;
            }
            w += 32;
            y += 36;

            // banka
            if (s.cfg.unlimitedBank) Label(new Rect(x, y, w, 20), "Banka sınırsız", Sty.dim);
            else
            {
                Label(new Rect(x, y, 60, 20), "Banka", MidLeft(Sty.dim));
                float bx = x + 52, step = (w - 52) / 5f;
                for (int i = 0; i < 5; i++)
                {
                    ResIcon(new Rect(bx + i * step, y, 20, 20), i);
                    Label(new Rect(bx + i * step + 23, y, step - 23, 20), s.bank[i].ToString(), MidLeft(Sty.small));
                }
            }
            y += 26;

            // gelişim kartları
            if (s.cfg.devCardsEnabled && me >= 0)
            {
                Label(new Rect(x, y, w, 22), "Gelişim kartların", Sty.h2);
                y += 24;
                var pl = s.players[me];
                if (pl.dev.Count == 0) { Label(new Rect(x, y, w, 18), "Kartın yok.", Sty.dim); y += 22; }
                for (int t = 0; t < 5; t++)
                {
                    int total = pl.dev.Count(d => (int)d.type == t);
                    if (total == 0) continue;
                    int ready = pl.dev.Count(d => (int)d.type == t && (s.cfg.playDevSameTurn || d.boughtTurn < s.turn));
                    var row = new Rect(x, y, w, 44);
                    Sty.Fill(row, Sty.Panel2);
                    Label(new Rect(row.x + 8, row.y + 3, w - 90, 20), "<b>" + T.Dev[t] + "</b> ×" + total, Sty.label);
                    string sub = t == (int)DevCard.Zafer ? "Gizli puan, otomatik sayılır." : ready < total ? (total - ready) + " tanesi bu tur alındı" : T.DevDesc[t];
                    Label(new Rect(row.x + 8, row.y + 22, w - 90, 20), sub, Sty.dim);
                    if (t != (int)DevCard.Zafer)
                    {
                        bool can = Rules.CanPlayDev(s, me, (DevCard)t) == null;
                        if (Btn(new Rect(row.xMax - 74, row.y + 8, 66, 28), "Oyna", can, Sty.btnSmall)) PlayDev(s, me, (DevCard)t);
                    }
                    y += 48;
                }
                y += 6;
            }

            // teklifler
            if (s.offers.Count > 0)
            {
                Label(new Rect(x, y, w, 22), "Ticaret teklifleri", Sty.h2);
                y += 24;
                foreach (var o in s.offers.ToList())
                {
                    if (y > r.yMax - 240) { Label(new Rect(x, y, w, 18), "Daha fazla teklif var...", Sty.dim); y += 22; break; }
                    y = DrawOffer(s, me, o, new Rect(x, y, w, 0)) + 4;
                }
                y += 6;
            }

            // kayıt
            Label(new Rect(x, y, w, 22), "Kayıt", Sty.h2);
            y += 24;
            float logH = r.yMax - y - 8;
            if (logH > 60)
            {
                var view = new Rect(x, y, w, logH);
                Sty.Fill(view, new Color(0, 0, 0, 0.25f));
                float lineH = 0;
                var heights = new float[s.log.Count];
                for (int i = 0; i < s.log.Count; i++) { heights[i] = LogLineHeight(s.log[i], w - 24); lineH += heights[i]; }
                // kayıt 300 satırda sabitlenince sayı değişmez; son satır da değiştiyse yeni kayıt gelmiştir
                string lastLine = s.log.Count > 0 ? s.log[s.log.Count - 1] : null;
                if (s.log.Count != lastLogCount || lastLine != lastLogLine) { lastLogCount = s.log.Count; lastLogLine = lastLine; logScroll.y = float.MaxValue; }
                float contentH = Mathf.Max(lineH + 8, logH);
                logScroll = BeginScroll(view, logScroll, new Rect(0, 0, w - 18, contentH));
                // yalnızca görünen satırlar çizilir; oyun uzadıkça yüzlerce satırı her karede çizmek FPS'i düşürüyordu
                float top = Mathf.Clamp(logScroll.y, 0, contentH - logH), bottom = top + logH;
                float ly = 4;
                for (int i = 0; i < s.log.Count; i++)
                {
                    if (ly + heights[i] >= top && ly <= bottom)
                        FlowLabel(new Rect(6, ly, w - 24, heights[i]), s.log[i], s.log[i].StartsWith("—") ? Sty.Colored(Sty.small, Sty.Brass) : Sty.small);
                    ly += heights[i];
                }
                EndScroll();
            }
        }

        // ---------------- sohbet (oyuncu listesinin altında) ----------------

        Vector2 chatScroll;
        int chatSeen = -1;

        void DrawChat(float top)
        {
            if (app.isLocal) return;
            var r = new Rect(12, top, 266, sh - 12 - top);
            if (r.height < 48) return;
            Panel(r);
            var ev = Event.current;
            bool focused = GUI.GetNameOfFocusedControl() == "chat";
            bool enterKey = ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter);
            // Enter: sohbete odaklan · Esc: sohbetten çık
            if (enterKey && !focused && GUIUtility.keyboardControl == 0 && !BlockingModal()) { GUI.FocusControl("chat"); ev.Use(); }
            if (focused && ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape) { GUI.FocusControl(null); GUIUtility.keyboardControl = 0; ev.Use(); }

            float x = r.x + 10, w = r.width - 20;
            float inputY = r.yMax - 36;
            if (r.height >= 110)
            {
                Label(new Rect(x + 4, r.y + 6, w, 22), "Sohbet", Sty.h2);
                var view = new Rect(x, r.y + 32, w, inputY - 6 - (r.y + 32));
                Sty.Fill(view, new Color(0, 0, 0, 0.25f));
                var lines = app.chat;
                // metinler ve yükseklikler yalnızca yeni mesaj gelince ya da genişlik değişince yeniden hesaplanır
                if (chatCacheVersion != app.chatVersion || chatCacheW != w || chatCacheStyle != Sty.small || chatTexts.Length != lines.Count)
                {
                    chatCacheVersion = app.chatVersion; chatCacheW = w; chatCacheStyle = Sty.small;
                    chatTexts = new string[lines.Count];
                    chatHeights = new float[lines.Count];
                    chatTotal = 0;
                    for (int i = 0; i < lines.Count; i++) { chatTexts[i] = ChatText(lines[i]); chatHeights[i] = Sty.small.CalcHeight(new GUIContent(chatTexts[i]), w - 24) + 2; chatTotal += chatHeights[i]; }
                }
                var texts = chatTexts;
                var heights = chatHeights;
                float total = chatTotal;
                if (app.chatVersion != chatSeen) { chatSeen = app.chatVersion; chatScroll.y = float.MaxValue; }
                if (lines.Count == 0) Label(new Rect(view.x + 8, view.y + 6, view.width - 16, 20), "Henüz mesaj yok. Enter ile yaz.", Sty.dim);
                else
                {
                    float contentH = Mathf.Max(total + 8, view.height);
                    chatScroll = BeginScroll(view, chatScroll, new Rect(0, 0, w - 18, contentH));
                    float visTop = Mathf.Clamp(chatScroll.y, 0, contentH - view.height), visBottom = visTop + view.height;
                    // az mesaj varken alta yaslı dursun
                    float ly = Mathf.Max(4, view.height - total - 4);
                    for (int i = 0; i < texts.Length; i++)
                    {
                        if (ly + heights[i] >= visTop && ly <= visBottom) Label(new Rect(6, ly, w - 24, heights[i]), texts[i], Sty.small);
                        ly += heights[i];
                    }
                    EndScroll();
                }
            }
            else if (app.chat.Count > 0)
            {
                // yer darsa yalnızca son mesaj
                var last = app.chat[app.chat.Count - 1];
                if (r.height >= 76) Label(new Rect(x + 2, r.y + 6, w - 4, 22), ChatText(last), MidLeft(Sty.small));
            }

            bool enter = enterKey && focused;
            GUI.SetNextControlName("chat");
            chat = Field(new Rect(x, inputY, w - 62, 28), chat ?? "", App.ChatMax, Sty.field);
            if ((Btn(new Rect(x + w - 56, inputY, 56, 28), "Yaz", true, Sty.btnSmall) || enter) && !string.IsNullOrWhiteSpace(chat))
            {
                app.SendChat(chat);
                chat = "";
                if (enter) ev.Use();
            }
        }

        static string ChatText(ChatLine l) =>
            "<color=#" + ColorUtility.ToHtmlStringRGB(Pal.PlayerColor(l.color)) + "><b>" + Esc(l.name) + "</b></color>  " + Esc(l.text);

        void PlayDev(GameState s, int me, DevCard t)
        {
            switch (t)
            {
                case DevCard.Bereket: yopSel = new int[5]; yopOpen = true; break;
                case DevCard.Tekel: monoOpen = true; break;
                default: app.Send(new GameAction { type = Act.PlayDev, a = (int)t }); break;
            }
        }

        // ---------------- pencereler ----------------

        Rect Modal(float w, float h, string title)
        {
            var r = new Rect((sw - w) / 2, (sh - h) / 2 - 20, w, h);
            FlushText();
            Sty.Fill(Block(r), Pal.Ink);
            Panel(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4));
            Sty.Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, 3), Sty.Brass);
            Label(new Rect(r.x + 20, r.y + 14, r.width - 40, 28), title, Sty.h1);
            return r;
        }

        bool Stepper(Rect r, ref int v, int min, int max)
        {
            bool changed = false;
            if (Btn(new Rect(r.x, r.y, 26, r.height), "−", v > min, Sty.btnSmall)) { v--; changed = true; }
            Label(new Rect(r.x + 26, r.y, r.width - 52, r.height), v.ToString(), Sty.center);
            if (Btn(new Rect(r.xMax - 26, r.y, 26, r.height), "+", v < max, Sty.btnSmall)) { v++; changed = true; }
            return changed;
        }

        void DrawDiscard(GameState s, int me)
        {
            int need = s.discardPending[me];
            var r = Modal(520, 260, (app.isLocal ? Esc(s.players[me].name) + ": " : "") + need + " kart at");
            Label(new Rect(r.x + 20, r.y + 46, r.width - 40, 20), "Korsan geldi. Elinde " + s.cfg.discardLimit + " karttan fazlası olduğu için yarısını atıyorsun.", Sty.dim);
            var res = s.players[me].res;
            for (int i = 0; i < 5; i++)
            {
                float cx = r.x + 24 + i * 96;
                ResChip(new Rect(cx, r.y + 76, 84, 52), i, res[i] - discardSel[i]);
                Stepper(new Rect(cx, r.y + 134, 84, 26), ref discardSel[i], 0, res[i]);
            }
            int sel = Util.Sum(discardSel);
            Label(new Rect(r.x + 20, r.y + 176, 240, 24), "Seçilen: " + sel + " / " + need, Sty.label);
            if (Btn(new Rect(r.xMax - 180, r.y + 196, 160, 44), "Kartları at", sel == need, Sty.btnAccent))
            {
                app.Send(new GameAction { type = Act.Discard, give = (int[])discardSel.Clone() }, me);
                discardSel = new int[5];
            }
        }

        void DrawSteal(GameState s, int me)
        {
            var targets = Rules.StealTargets(s, me, s.robber);
            var r = Modal(420, 110 + targets.Count * 52, "Kimden çalacaksın?");
            for (int i = 0; i < targets.Count; i++)
            {
                int p = targets[i];
                var br = new Rect(r.x + 20, r.y + 54 + i * 52, r.width - 40, 44);
                if (Btn(br, Esc(s.players[p].name) + "  ·  " + Rules.HandSize(s, p) + " kart", true, Sty.btn))
                    app.Send(new GameAction { type = Act.Steal, target = p });
                Sty.Fill(new Rect(br.x, br.y, 6, br.height), Pal.PlayerColor(s.players[p].color));
            }
        }

        void DrawYop(GameState s, int me)
        {
            int need = s.cfg.yearOfPlentyCount;
            var r = Modal(520, 250, "Bereket: bankadan " + need + " kaynak seç");
            for (int i = 0; i < 5; i++)
            {
                float cx = r.x + 24 + i * 96;
                ResChip(new Rect(cx, r.y + 56, 84, 52), i, yopSel[i]);
                int bankMax = s.cfg.unlimitedBank ? need : Math.Min(need, s.bank[i]);
                Stepper(new Rect(cx, r.y + 114, 84, 26), ref yopSel[i], 0, bankMax);
            }
            int sel = Util.Sum(yopSel);
            if (Btn(new Rect(r.x + 20, r.y + 186, 120, 44), "Vazgeç", true, Sty.btn)) yopOpen = false;
            if (Btn(new Rect(r.xMax - 180, r.y + 186, 160, 44), "Al (" + sel + "/" + need + ")", sel == need, Sty.btnAccent))
            {
                app.Send(new GameAction { type = Act.PlayDev, a = (int)DevCard.Bereket, get = (int[])yopSel.Clone() });
                yopOpen = false;
            }
        }

        void DrawMono(GameState s, int me)
        {
            var r = Modal(520, 200, "Tekel: hangi kaynağı topluyorsun?");
            for (int i = 0; i < 5; i++)
            {
                var br = new Rect(r.x + 24 + i * 96, r.y + 58, 84, 64);
                ResChip(br, i, s.players[me].res[i]);
                if (Button(br, GUIContent.none, GUIStyle.none))
                {
                    app.Send(new GameAction { type = Act.PlayDev, a = (int)DevCard.Tekel, target = i });
                    monoOpen = false;
                }
            }
            if (Btn(new Rect(r.x + 20, r.y + 138, 120, 40), "Vazgeç", true, Sty.btn)) monoOpen = false;
        }

        static string Cards0(int[] c) => Util.Sum(c) == 0 ? "hiçbir şey" : T.Cards(c);

        // tek ekranda kabul ve onay aynı anda yapılır
        void LocalTrade(int id, int from, int p)
        {
            app.Send(new GameAction { type = Act.AcceptOffer, target = id }, p);
            var st = app.state;
            if (st != null && st.offers.Exists(x => x.id == id && x.accepted.Contains(p)))
                app.Send(new GameAction { type = Act.ConfirmOffer, target = id, a = p }, from);
        }

        void OpenCounter(GameState s, int me, TradeOffer o)
        {
            tGive = new int[5]; tGet = new int[5];
            for (int i = 0; i < 5; i++) { tGive[i] = Math.Min(o.get[i], s.players[me].res[i]); tGet[i] = o.give[i]; }
            tTarget = o.from;
            tCounter = o.id;
            tradeOpen = true;
        }

        void CloseTrade() { tradeOpen = false; tCounter = -1; }

        void ClearTrade() { tGive = new int[5]; tGet = new int[5]; tCounter = -1; }

        void TradeToasts(GameState prev, GameState s)
        {
            int me = app.Me;
            if (me < 0) return;
            foreach (var o in s.offers)
            {
                var old = prev.offers.Find(x => x.id == o.id);
                if (old == null && o.from != me && Rules.OfferParty(s, o, me))
                    Toast(Name(s, o.from) + (o.counterOf > 0 ? " karşı teklif yaptı." : " sana teklif yaptı."));
                if (o.from == me)
                    foreach (var q in o.accepted)
                        if (old == null || !old.accepted.Contains(q)) Toast(Name(s, q) + " teklifini kabul etti. Takası onayla.");
            }
        }

        // ---------------- ticaret: üstte alınacak kartlar, altta el, sağda oyuncu/banka butonları ----------------

        const float TCardW = 58, TCardH = 82, TCardStep = 72;

        GUIStyle smallCenter;

        GUIStyle SmallCenter()
        {
            if (smallCenter == null) smallCenter = new GUIStyle(Sty.small) { alignment = TextAnchor.UpperCenter, wordWrap = true };
            return smallCenter;
        }

        /// <summary>Tıklanabilir kart: sol tık +1, sağ tık -1 döndürür; işlem yoksa 0.</summary>
        int TradeCard(Rect rect, int res, bool enabled)
        {
            var e = Event.current;
            bool hot = enabled && rect.Contains(e.mousePosition);
            var old = GUI.color;
            var draw = hot ? new Rect(rect.x, rect.y - 4, rect.width, rect.height) : rect;
            DrawCardFace(draw, res, enabled ? old : new Color(old.r, old.g, old.b, old.a * 0.3f));
            GUI.color = old;
            if (!hot || e.type != EventType.MouseDown) return 0;
            if (e.button == 0) { e.Use(); return 1; }
            if (e.button == 1) { e.Use(); return -1; }
            return 0;
        }

        /// <summary>Kartın sağ üst köşesinde "+2" / "−1" rozeti.</summary>
        void TradeBadge(Rect card, string text, Color col)
        {
            var b = new Rect(card.xMax - 22, card.y - 9, 32, 22);
            var old = GUI.color;
            GUI.color = Pal.Ink;
            GUI.DrawTexture(b, Icons.Card());
            GUI.color = col;
            GUI.DrawTexture(new Rect(b.x + 2, b.y + 2, b.width - 4, b.height - 4), Icons.Card());
            GUI.color = old;
            Label(b, text, Sty.Colored(Sty.center, Pal.Ink));
        }

        /// <summary>Büyük simgeli kare buton (oyuncular / banka).</summary>
        bool IconButton(Rect r, string symbol, string fallback, string label, bool enabled)
        {
            bool clicked = Btn(r, "", enabled, Sty.btn);
            var col = enabled ? Sty.Brass : new Color(Sty.Dim.r, Sty.Dim.g, Sty.Dim.b, 0.45f);
            var ic = new Rect(r.x + (r.width - 52) / 2, r.y + 10, 52, 52);
            if (!DrawSymbol(ic, symbol, col)) Label(ic, fallback, Sty.Colored(Sty.centerBig, col));
            Label(new Rect(r.x + 2, r.y + 66, r.width - 4, 34), label, Sty.Colored(SmallCenter(), enabled ? Sty.Text : Sty.Dim));
            return clicked;
        }

        void DrawTrade(GameState s, int me)
        {
            if (s.phase != Phase.Main || me < 0) { CloseTrade(); return; }
            var c = s.cfg;
            var pl = s.players[me];
            bool active = me == s.current;
            bool bank = active && c.bankTrade;
            bool players = c.playerTrade;
            bool chooseTarget = players && (active || !c.tradeOnlyWithActive);
            if (tTarget == me || tTarget >= s.players.Count) tTarget = -1;
            int to = chooseTarget ? tTarget : s.current;
            for (int i = 0; i < 5; i++) tGive[i] = Math.Min(tGive[i], pl.res[i]);

            const float ExSumH = 140, Summary = ExSumH + 44;
            float h = 54 + 30 + TCardH + 18 + 12 + Summary + (chooseTarget ? 36 : 0) + 12 + 30 + TCardH + 30;
            var r = Modal(660, h, tCounter > 0 ? "Karşı teklif" : "Ticaret");
            if (Btn(new Rect(r.xMax - 100, r.y + 14, 80, 26), "Kapat", true, Sty.btnSmall)) CloseTrade();
            if (Btn(new Rect(r.xMax - 190, r.y + 14, 84, 26), "Temizle", Util.Sum(tGive) + Util.Sum(tGet) > 0 || tCounter > 0, Sty.btnSmall)) ClearTrade();

            float x0 = r.x + 24;
            float cx0 = r.x + (r.width - (TCardStep * 4 + TCardW)) / 2;
            float y = r.y + 54;

            // ---- üst: alınabilecek kartlar ----
            Label(new Rect(x0, y, 200, 24), "Alacakların", Sty.h2);
            Label(new Rect(r.xMax - 324, y + 2, 300, 20), "tıkla: ekle · sağ tık: çıkar", MidRight(Sty.dim));
            y += 30;
            for (int i = 0; i < 5; i++)
            {
                var cr = new Rect(cx0 + i * TCardStep, y, TCardW, TCardH);
                int d = TradeCard(cr, i, true);
                if (d != 0) { tGet[i] = Mathf.Clamp(tGet[i] + d, 0, 19); if (tGet[i] > 0) tGive[i] = 0; }
                if (tGet[i] > 0) TradeBadge(cr, "+" + tGet[i], Sty.Good);
            }
            y += TCardH + 18;

            // ---- orta: özet ve gönder butonları ----
            Sty.Fill(new Rect(r.x + 20, y, r.width - 40, 1), Sty.Line);
            y += 12;
            const float BtnW = 92;
            float sumW = r.width - 48 - BtnW * 2 - 12;
            // özet: üstte sen; teklifi yapan sensin, o yüzden önce istediklerin (aşağı ok), altta verdiklerin (yukarı ok)
            var (meName, meCol) = PlayerEnd(s, me, me);
            DrawExchange(new Rect(x0, y, sumW, ExSumH), meName, meCol, tGive, tGet, true, 26, Sty.Panel, Sty.small);

            string berr = bank ? Rules.BankTradeError(s, me, tGive, tGet) : null;
            string perr = players ? Rules.OfferError(s, me, to, tGive, tGet) : null;
            string info;
            if (Util.Sum(tGive) + Util.Sum(tGet) == 0) info = "Üstten alacağın, alttan vereceğin kartlara tıkla.";
            else
            {
                var lines = new List<string>();
                if (players) lines.Add("Oyuncular: " + (perr ?? (to >= 0 ? Esc(Name(s, to)) + " kabul ederse takas hemen yapılır." : "kabul edenler arasından sen seçersin.")));
                if (bank) lines.Add("Banka: " + (berr ?? "takasa hazır."));
                info = string.Join("\n", lines);
            }
            Label(new Rect(x0, y + ExSumH + 6, sumW, 38), info, Sty.dim);

            var pr = new Rect(r.xMax - 24 - BtnW * 2 - 8, y, BtnW, Summary - 6);
            var bkr = new Rect(r.xMax - 24 - BtnW, y, BtnW, Summary - 6);
            if (players && IconButton(pr, "oyuncular", "O", tCounter > 0 ? "Karşı teklif gönder" : "Oyunculara teklif", perr == null))
            {
                app.Send(new GameAction { type = Act.Offer, give = (int[])tGive.Clone(), get = (int[])tGet.Clone(), target = to, a = tCounter });
                ClearTrade();
                Toast("Teklif gönderildi.");
            }
            if (bank && IconButton(bkr, "banka", "B", "Bankayla takas", berr == null))
            {
                app.Send(new GameAction { type = Act.BankTrade, give = (int[])tGive.Clone(), get = (int[])tGet.Clone() });
                ClearTrade();
            }
            y += Summary;

            if (chooseTarget)
            {
                Label(new Rect(x0, y + 4, 60, 20), "Kime:", Sty.label);
                // hepsi seçiliyken birine tıklamak yalnız onu seçer; seçili olana yeniden tıklamak hepsini geri seçer
                int n = s.players.Count, others = Mathf.Max(1, n - 1);
                float bx = x0 + 60;
                float bw = Mathf.Min(110, (r.xMax - 24 - bx - 4 * (others - 1)) / others);
                for (int p = 0; p < n; p++)
                {
                    if (p == me) continue;
                    var chip = new Rect(bx, y, bw, 28);
                    bool on = tTarget < 0 || tTarget == p;
                    if (Btn(chip, Esc(s.players[p].name), true, on ? Sty.btnOn : Sty.btnSmall)) tTarget = tTarget == p ? -1 : p;
                    Sty.Fill(new Rect(chip.x, chip.y, 4, chip.height), Pal.PlayerColor(s.players[p].color));
                    bx += bw + 4;
                }
                y += 36;
            }

            // ---- alt: eldeki kartlar ----
            Sty.Fill(new Rect(r.x + 20, y, r.width - 40, 1), Sty.Line);
            y += 12;
            Label(new Rect(x0, y, 200, 24), "Elindekiler", Sty.h2);
            if (bank) Label(new Rect(r.xMax - 324, y + 2, 300, 20), "kartın altında: adet · banka oranı", MidRight(Sty.dim));
            y += 30;
            for (int i = 0; i < 5; i++)
            {
                var cr = new Rect(cx0 + i * TCardStep, y, TCardW, TCardH);
                int d = TradeCard(cr, i, pl.res[i] > 0);
                if (d != 0) { tGive[i] = Mathf.Clamp(tGive[i] + d, 0, pl.res[i]); if (tGive[i] > 0) tGet[i] = 0; }
                if (tGive[i] > 0) TradeBadge(cr, "−" + tGive[i], Sty.Danger);
                string under = pl.res[i] + (bank ? " · " + Rules.Ratio(s, me, i) + ":1" : "");
                Label(new Rect(cr.x - 8, cr.yMax + 4, TCardW + 16, 18), under, Sty.Colored(SmallCenter(), pl.res[i] > 0 ? Sty.Text : Sty.Dim));
            }
        }

        readonly Dictionary<GUIStyle, GUIStyle> midRight = new Dictionary<GUIStyle, GUIStyle>();

        GUIStyle MidRight(GUIStyle s)
        {
            if (!midRight.TryGetValue(s, out var m))
            {
                m = new GUIStyle(s) { alignment = TextAnchor.MiddleRight, wordWrap = false };
                midRight[s] = m;
            }
            return m;
        }

        void DrawRulesModal()
        {
            var r = new Rect((sw - 600) / 2, 30, 600, sh - 60);
            var saved = app.lobby.cfg;
            app.lobby.cfg = app.state.cfg;
            DrawRulesEditor(r, false);
            app.lobby.cfg = saved;
            if (Btn(new Rect(r.xMax - 96, r.y + 12, 80, 26), "Kapat", true, Sty.btnSmall)) rulesOpen = false;
        }

        void DrawGameOver(GameState s)
        {
            var order = Enumerable.Range(0, s.players.Count).OrderByDescending(p => Rules.TotalVP(s, p)).ToList();
            var r = Modal(480, 150 + order.Count * 34, (s.winner >= 0 ? Esc(Name(s, s.winner)) : "?") + " kazandı!");
            for (int i = 0; i < order.Count; i++)
            {
                int p = order[i];
                var row = new Rect(r.x + 20, r.y + 52 + i * 34, r.width - 40, 30);
                Sty.Fill(row, Sty.Panel2);
                Sty.Fill(new Rect(row.x, row.y, 6, row.height), Pal.PlayerColor(s.players[p].color));
                int hidden = Rules.HiddenVP(s, p);
                Label(new Rect(row.x + 14, row.y + 5, row.width - 80, 22), (i + 1) + ". " + Esc(s.players[p].name) + (hidden > 0 ? "  (" + hidden + " gizli zafer kartı)" : ""), Sty.label);
                Label(new Rect(row.xMax - 60, row.y + 2, 52, 26), Rules.TotalVP(s, p).ToString(), Sty.Colored(Sty.centerBig, Sty.Brass));
            }
            float by = r.yMax - 60;
            if (app.isHost)
            {
                if (Btn(new Rect(r.x + 20, by, 200, 42), "Lobiye dön", true, Sty.btnAccent)) app.BackToLobby();
            }
            else Label(new Rect(r.x + 20, by + 10, 240, 24), "Kurucu lobiye döndürebilir.", Sty.dim);
            if (Btn(new Rect(r.xMax - 180, by, 160, 42), "Ana menü", true, Sty.btn)) app.Leave();
        }
    }
}
