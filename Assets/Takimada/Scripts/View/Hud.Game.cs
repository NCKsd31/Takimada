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
        string chat = "";

        void ResetDialogs()
        {
            yopOpen = monoOpen = tradeOpen = rulesOpen = confirmQuit = false;
            discardSel = new int[5]; yopSel = new int[5]; tGive = new int[5]; tGet = new int[5];
            tTarget = -1; tCounter = -1;
        }

        bool BlockingModal()
        {
            var s = app.state;
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
            DrawPlayers(s, me);
            DrawBanner(s, me);
            DrawBottom(s, me);
            DrawSide(s, me);

            if (s.phase == Phase.GameOver) { DrawGameOver(s); return; }
            if (s.phase == Phase.Discard && me >= 0 && s.discardPending[me] > 0) DrawDiscard(s, me);
            else if (s.phase == Phase.Steal && me == s.current) DrawSteal(s, me);
            else if (yopOpen) DrawYop(s, me);
            else if (monoOpen) DrawMono(s, me);
            else if (tradeOpen) DrawTrade(s, me);
            else if (rulesOpen) DrawRulesModal();
        }

        // ---------------- oyuncu listesi ----------------

        void DrawPlayers(GameState s, int me)
        {
            int n = s.players.Count;
            float rowH = 84;
            var r = new Rect(12, 12, 266, 40 + n * (rowH + 6));
            Panel(r);
            GUI.Label(new Rect(r.x + 14, r.y + 8, 200, 24), "Kaptanlar · hedef " + s.cfg.victoryPoints, Sty.h2);
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
                GUI.Label(new Rect(row.x + 14, row.y + 6, row.width - 70, 22), "<b>" + Esc(pl.name) + "</b>" + tag, Sty.label);
                GUI.Label(new Rect(row.xMax - 60, row.y + 4, 52, 40), vp.ToString(), Sty.Colored(Sty.centerBig, Sty.Brass));
                GUI.Label(new Rect(row.xMax - 60, row.y + 38, 52, 16), "puan", Sty.Colored(Sty.small, Sty.Dim));

                int hand = Rules.HandSize(s, p);
                var stats = "Kart " + hand + "   Gelişim " + pl.dev.Count + "   Muhafız " + pl.knights + "   Yol " + pl.roadLength;
                GUI.Label(new Rect(row.x + 14, row.y + 30, row.width - 74, 20), stats, Sty.small);

                var badges = new List<string>();
                if (s.longestHolder == p) badges.Add("En Uzun Yol");
                if (s.armyHolder == p) badges.Add("En Güçlü Ordu");
                if (hand > s.cfg.discardLimit && s.cfg.robberEnabled) badges.Add("korsan riski");
                if (badges.Count > 0) GUI.Label(new Rect(row.x + 14, row.y + 52, row.width - 74, 22), string.Join(" · ", badges), Sty.Colored(Sty.small, Sty.Brass));
            }
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
            GUI.Label(new Rect(r.x + 18, r.y + 8, r.width - 140, 22), (mine && !app.isLocal ? "SIRA SENDE · " : "") + "Tur " + Math.Max(1, s.round) + " · " + T.Phase[(int)s.phase], Sty.Colored(Sty.small, Sty.Dim));
            GUI.Label(new Rect(r.x + 18, r.y + 28, r.width - 140, 36), line, Sty.Colored(Sty.wrap, mine ? Sty.Text : Sty.Dim));

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

        void DrawBottom(GameState s, int me)
        {
            var r = new Rect(290, sh - 158, sw - 290 - 324, 146);
            Panel(r);
            if (me < 0) { GUI.Label(new Rect(r.x + 16, r.y + 16, r.width - 32, 30), "İzleyici", Sty.h2); return; }
            var pl = s.players[me];
            float x = r.x + 14;
            if (app.isLocal)
            {
                GUI.Label(new Rect(x, r.y + 6, 300, 20), "Ekrandaki oyuncu: <b>" + Esc(pl.name) + "</b>", Sty.small);
            }
            for (int i = 0; i < T.ResCount; i++)
                ResChip(new Rect(x + i * 70, r.y + 26, 64, 52), i, pl.res[i]);
            float infoX = x + 5 * 70 + 8;
            GUI.Label(new Rect(infoX, r.y + 26, r.xMax - infoX - 10, 54),
                "Elde " + Util.Sum(pl.res) + " kart\nKalan taş: " + pl.roadsLeft + " yol · " + pl.settlementsLeft + " köy · " + pl.citiesLeft + " kasaba", Sty.small);

            int actor = Rules.Actor(s);
            bool myTurn = me == actor;
            bool canBuild = myTurn && Rules.IsBuildPhase(s);
            bool main = myTurn && s.phase == Phase.Main;
            var c = s.cfg;

            var acts = new List<(string label, string sub, bool on, bool active, Action act, GUIStyle style)>();
            if (s.phase == Phase.Roll)
                acts.Add(("Zar at", "", myTurn, false, () => app.Send(new GameAction { type = Act.Roll }), Sty.btnAccent));
            acts.Add(("Yol", CostShort(c.roadCost), canBuild && Util.Has(pl.res, c.roadCost) && pl.roadsLeft > 0, build == BuildMode.Road,
                () => ToggleBuild(BuildMode.Road), null));
            acts.Add(("Köy", CostShort(c.settlementCost), canBuild && Util.Has(pl.res, c.settlementCost) && pl.settlementsLeft > 0, build == BuildMode.Settlement,
                () => ToggleBuild(BuildMode.Settlement), null));
            acts.Add(("Kasaba", CostShort(c.cityCost), canBuild && Util.Has(pl.res, c.cityCost) && pl.citiesLeft > 0, build == BuildMode.City,
                () => ToggleBuild(BuildMode.City), null));
            if (c.devCardsEnabled)
                acts.Add(("Kart al", CostShort(c.devCost) + " · " + s.devDeckCount + " kaldı", canBuild && Util.Has(pl.res, c.devCost) && s.devDeckCount > 0, false,
                    () => app.Send(new GameAction { type = Act.BuyDev }), null));
            if (c.playerTrade || c.bankTrade)
                acts.Add(("Ticaret", "", s.phase == Phase.Main && (main || c.playerTrade), tradeOpen, () => { if (tradeOpen) CloseTrade(); else tradeOpen = true; }, null));
            if (s.phase == Phase.SpecialBuild)
                acts.Add(("Pas geç", "", myTurn, false, () => app.Send(new GameAction { type = Act.EndTurn }), Sty.btnAccent));
            else if (s.phase == Phase.RoadBuilding)
                acts.Add(("Yolları bitir", "", myTurn, false, () => app.Send(new GameAction { type = Act.EndTurn }), Sty.btnAccent));
            else
                acts.Add(("Turu bitir", "", main, false, () => { build = BuildMode.None; CloseTrade(); app.Send(new GameAction { type = Act.EndTurn }); }, Sty.btnAccent));

            float gap = 6, bw = Mathf.Min(124, (r.width - 28 - gap * (acts.Count - 1)) / acts.Count);
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                var br = new Rect(r.x + 14 + i * (bw + gap), r.y + 88, bw, 46);
                string text = string.IsNullOrEmpty(a.sub) ? a.label : a.label + "\n<size=10>" + a.sub + "</size>";
                var st = a.style ?? (a.active ? Sty.btnOn : Sty.btn);
                st.richText = true;
                if (Btn(br, text, a.on || a.active, st)) a.act();
            }
        }

        void ToggleBuild(BuildMode m) => build = build == m ? BuildMode.None : m;

        static string CostShort(int[] cost)
        {
            var parts = new List<string>();
            for (int i = 0; i < T.ResCount; i++) if (cost[i] > 0) parts.Add(cost[i] + " " + T.ResShort[i]);
            return parts.Count == 0 ? "ücretsiz" : string.Join(" ", parts);
        }

        // ---------------- sağ panel: kartlar, teklifler, kayıt ----------------

        void DrawSide(GameState s, int me)
        {
            var r = new Rect(sw - 312, 12, 300, sh - 24);
            Panel(r);
            float x = r.x + 14, w = r.width - 28, y = r.y + 10;

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
            y += 36;

            // banka
            var bankLine = s.cfg.unlimitedBank ? "Banka sınırsız" : "Banka: " + string.Join("  ", Enumerable.Range(0, 5).Select(i => T.ResShort[i] + " " + s.bank[i]));
            GUI.Label(new Rect(x, y, w, 18), bankLine, Sty.dim);
            y += 24;

            // gelişim kartları
            if (s.cfg.devCardsEnabled && me >= 0)
            {
                GUI.Label(new Rect(x, y, w, 22), "Gelişim kartların", Sty.h2);
                y += 24;
                var pl = s.players[me];
                if (pl.dev.Count == 0) { GUI.Label(new Rect(x, y, w, 18), "Kartın yok.", Sty.dim); y += 22; }
                for (int t = 0; t < 5; t++)
                {
                    int total = pl.dev.Count(d => (int)d.type == t);
                    if (total == 0) continue;
                    int ready = pl.dev.Count(d => (int)d.type == t && (s.cfg.playDevSameTurn || d.boughtTurn < s.turn));
                    var row = new Rect(x, y, w, 44);
                    Sty.Fill(row, Sty.Panel2);
                    GUI.Label(new Rect(row.x + 8, row.y + 3, w - 90, 20), "<b>" + T.Dev[t] + "</b> ×" + total, Sty.label);
                    string sub = t == (int)DevCard.Zafer ? "Gizli puan, otomatik sayılır." : ready < total ? (total - ready) + " tanesi bu tur alındı" : T.DevDesc[t];
                    GUI.Label(new Rect(row.x + 8, row.y + 22, w - 90, 20), sub, Sty.dim);
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
                GUI.Label(new Rect(x, y, w, 22), "Ticaret teklifleri", Sty.h2);
                y += 24;
                foreach (var o in s.offers.ToList())
                {
                    if (y > r.yMax - 170) { GUI.Label(new Rect(x, y, w, 18), "Daha fazla teklif var...", Sty.dim); y += 22; break; }
                    y = DrawOffer(s, me, o, new Rect(x, y, w, 0)) + 4;
                }
                y += 6;
            }

            // kayıt
            GUI.Label(new Rect(x, y, w, 22), "Kayıt", Sty.h2);
            y += 24;
            float logH = r.yMax - y - 44;
            if (logH > 60)
            {
                var view = new Rect(x, y, w, logH);
                Sty.Fill(view, new Color(0, 0, 0, 0.25f));
                float lineH = 0;
                var heights = new float[s.log.Count];
                for (int i = 0; i < s.log.Count; i++) { heights[i] = Sty.small.CalcHeight(new GUIContent(s.log[i]), w - 24); lineH += heights[i]; }
                if (s.log.Count != lastLogCount) { lastLogCount = s.log.Count; logScroll.y = float.MaxValue; }
                logScroll = GUI.BeginScrollView(view, logScroll, new Rect(0, 0, w - 18, Mathf.Max(lineH + 8, logH)));
                float ly = 4;
                for (int i = 0; i < s.log.Count; i++)
                {
                    GUI.Label(new Rect(6, ly, w - 24, heights[i]), s.log[i], s.log[i].StartsWith("—") ? Sty.Colored(Sty.small, Sty.Brass) : Sty.small);
                    ly += heights[i];
                }
                GUI.EndScrollView();
            }
            if (!app.isLocal)
            {
                var ev = Event.current;
                bool enter = ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "chat";
                GUI.SetNextControlName("chat");
                chat = GUI.TextField(new Rect(x, r.yMax - 38, w - 66, 28), chat ?? "", 140, Sty.field);
                if ((Btn(new Rect(x + w - 60, r.yMax - 38, 60, 28), "Yaz", true, Sty.btnSmall) || enter) && !string.IsNullOrWhiteSpace(chat))
                {
                    app.Send(new GameAction { type = Act.Chat, text = chat.Trim() });
                    chat = "";
                    if (enter) ev.Use();
                }
            }
        }

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
            Sty.Fill(Block(r), Pal.Ink);
            Panel(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4));
            Sty.Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, 3), Sty.Brass);
            GUI.Label(new Rect(r.x + 20, r.y + 14, r.width - 40, 28), title, Sty.h1);
            return r;
        }

        bool Stepper(Rect r, ref int v, int min, int max)
        {
            bool changed = false;
            if (Btn(new Rect(r.x, r.y, 26, r.height), "−", v > min, Sty.btnSmall)) { v--; changed = true; }
            GUI.Label(new Rect(r.x + 26, r.y, r.width - 52, r.height), v.ToString(), Sty.center);
            if (Btn(new Rect(r.xMax - 26, r.y, 26, r.height), "+", v < max, Sty.btnSmall)) { v++; changed = true; }
            return changed;
        }

        void DrawDiscard(GameState s, int me)
        {
            int need = s.discardPending[me];
            var r = Modal(520, 260, (app.isLocal ? Esc(s.players[me].name) + ": " : "") + need + " kart at");
            GUI.Label(new Rect(r.x + 20, r.y + 46, r.width - 40, 20), "Korsan geldi. Elinde " + s.cfg.discardLimit + " karttan fazlası olduğu için yarısını atıyorsun.", Sty.dim);
            var res = s.players[me].res;
            for (int i = 0; i < 5; i++)
            {
                float cx = r.x + 24 + i * 96;
                ResChip(new Rect(cx, r.y + 76, 84, 52), i, res[i] - discardSel[i]);
                Stepper(new Rect(cx, r.y + 134, 84, 26), ref discardSel[i], 0, res[i]);
            }
            int sel = Util.Sum(discardSel);
            GUI.Label(new Rect(r.x + 20, r.y + 176, 240, 24), "Seçilen: " + sel + " / " + need, Sty.label);
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
                if (GUI.Button(br, GUIContent.none, GUIStyle.none))
                {
                    app.Send(new GameAction { type = Act.PlayDev, a = (int)DevCard.Tekel, target = i });
                    monoOpen = false;
                }
            }
            if (Btn(new Rect(r.x + 20, r.y + 138, 120, 40), "Vazgeç", true, Sty.btn)) monoOpen = false;
        }

        static string Cards0(int[] c) => Util.Sum(c) == 0 ? "hiçbir şey" : T.Cards(c);

        /// <summary>Tek bir teklif kartı. Kartın alt kenarının y değerini döndürür.</summary>
        float DrawOffer(GameState s, int me, TradeOffer o, Rect at)
        {
            float x = at.x, w = at.width;
            var parties = Rules.OfferParties(s, o);
            var buttons = new List<(string label, bool on, Action act)>();
            int id = o.id, from = o.from;
            if (app.isLocal)
            {
                foreach (var p in parties)
                {
                    if (o.rejected.Contains(p)) continue;
                    int pp = p;
                    buttons.Add((Esc(s.players[p].name) + " ile takas", Util.Has(s.players[p].res, o.get), () => LocalTrade(id, from, pp)));
                }
                buttons.Add(("İptal", true, () => app.Send(new GameAction { type = Act.CancelOffer, target = id }, from)));
            }
            else if (o.from == me)
            {
                foreach (var p in o.accepted)
                {
                    int pp = p;
                    buttons.Add((Esc(Name(s, p)) + " ile takas", true, () => app.Send(new GameAction { type = Act.ConfirmOffer, target = id, a = pp })));
                }
                buttons.Add(("İptal", true, () => app.Send(new GameAction { type = Act.CancelOffer, target = id })));
            }
            else if (me >= 0 && parties.Contains(me) && !o.rejected.Contains(me))
            {
                bool has = Util.Has(s.players[me].res, o.get);
                bool acc = o.accepted.Contains(me);
                buttons.Add((acc ? "Kabul ettin" : has ? "Kabul et" : "Kartın yok", has && !acc, () => app.Send(new GameAction { type = Act.AcceptOffer, target = id })));
                buttons.Add(("Reddet", true, () => app.Send(new GameAction { type = Act.RejectOffer, target = id })));
                buttons.Add(("Karşı teklif", true, () => OpenCounter(s, me, o)));
            }

            // yanıt durumu: kim kabul etti, kim reddetti
            string status = app.isLocal ? "" : string.Join(" · ", parties.Select(p => Esc(Name(s, p)) + ": " +
                (o.accepted.Contains(p) ? "<color=#7CC47F>kabul</color>" : o.rejected.Contains(p) ? "<color=#E0675A>ret</color>" : "bekliyor")));

            float bx0 = x + 10, maxX = x + w - 6, fx = bx0;
            int lines = 1;
            var widths = buttons.Select(b => Mathf.Max(60, Sty.btnSmall.CalcSize(new GUIContent(b.label)).x + 14)).ToList();
            foreach (var bw in widths) { if (fx + bw > maxX && fx > bx0) { lines++; fx = bx0; } fx += bw + 4; }
            float statusH = status.Length > 0 ? Sty.small.CalcHeight(new GUIContent(status), w - 16) : 0;
            float rowH = 58 + statusH + (buttons.Count > 0 ? lines * 26 : 0) + 4;

            var row = new Rect(x, at.y, w, rowH);
            Sty.Fill(row, Sty.Panel2);
            Sty.Fill(new Rect(row.x, row.y, 4, row.height), Pal.PlayerColor(s.players[o.from].color));
            string head = "<b>" + Esc(Name(s, o.from)) + "</b> → " + (o.to >= 0 ? Esc(Name(s, o.to)) : "herkes") + (o.counterOf > 0 ? "  · karşı teklif" : "");
            GUI.Label(new Rect(row.x + 10, row.y + 3, w - 16, 18), head, Sty.small);
            GUI.Label(new Rect(row.x + 10, row.y + 20, w - 16, 18), "Verir: " + Cards0(o.give), Sty.small);
            GUI.Label(new Rect(row.x + 10, row.y + 37, w - 16, 18), "İster: " + Cards0(o.get), Sty.small);
            float by = row.y + 56;
            if (statusH > 0) { GUI.Label(new Rect(row.x + 10, by, w - 16, statusH), status, Sty.dim); by += statusH + 2; }
            fx = bx0;
            for (int k = 0; k < buttons.Count; k++)
            {
                if (fx + widths[k] > maxX && fx > bx0) { by += 26; fx = bx0; }
                if (Btn(new Rect(fx, by, widths[k], 22), buttons[k].label, buttons[k].on, Sty.btnSmall)) buttons[k].act();
                fx += widths[k] + 4;
            }
            return row.yMax;
        }

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

        void DrawTrade(GameState s, int me)
        {
            if (s.phase != Phase.Main || me < 0) { CloseTrade(); return; }
            var c = s.cfg;
            var pl = s.players[me];
            bool active = me == s.current;
            bool bank = active && c.bankTrade;
            bool players = c.playerTrade;
            bool chooseTarget = players && (active || !c.tradeOnlyWithActive);
            float h = 218 + (bank ? 74 : 0) + (players ? (chooseTarget ? 118 : 84) : 0);
            var r = Modal(640, h, tCounter > 0 ? "Karşı teklif" : "Ticaret");
            if (Btn(new Rect(r.xMax - 100, r.y + 14, 80, 26), "Kapat", true, Sty.btnSmall)) CloseTrade();

            float colX = r.x + 120, colW = 98;
            float y = r.y + 54;
            for (int i = 0; i < 5; i++)
            {
                float cx = colX + i * colW;
                GUI.Label(new Rect(cx, y, colW - 6, 18), T.Res[i], Sty.Colored(Sty.small, Pal.Res[i]));
                GUI.Label(new Rect(cx, y + 17, colW - 6, 16), "elinde " + pl.res[i] + (bank ? " · " + Rules.Ratio(s, me, i) + ":1" : ""), Sty.dim);
            }
            y += 40;
            GUI.Label(new Rect(r.x + 20, y + 4, 100, 20), "Veriyorum:", Sty.label);
            for (int i = 0; i < 5; i++)
            {
                tGive[i] = Math.Min(tGive[i], pl.res[i]);
                if (Stepper(new Rect(colX + i * colW, y, colW - 6, 26), ref tGive[i], 0, pl.res[i]) && tGive[i] > 0) tGet[i] = 0;
            }
            y += 34;
            GUI.Label(new Rect(r.x + 20, y + 4, 100, 20), "İstiyorum:", Sty.label);
            for (int i = 0; i < 5; i++)
                if (Stepper(new Rect(colX + i * colW, y, colW - 6, 26), ref tGet[i], 0, 19) && tGet[i] > 0) tGive[i] = 0;
            y += 34;
            var after = Util.Copy(pl.res);
            Util.Add(after, tGive, -1);
            Util.Add(after, tGet);
            GUI.Label(new Rect(r.x + 20, y + 4, r.width - 150, 20), "Takastan sonra elin: " + string.Join("   ", Enumerable.Range(0, 5).Select(i => T.ResShort[i] + " " + after[i])), Sty.dim);
            if (Btn(new Rect(r.xMax - 120, y, 100, 26), "Temizle", Util.Sum(tGive) + Util.Sum(tGet) > 0 || tCounter > 0, Sty.btnSmall)) ClearTrade();
            y += 40;

            if (bank)
            {
                Sty.Fill(new Rect(r.x + 20, y, r.width - 40, 1), Sty.Line);
                y += 10;
                GUI.Label(new Rect(r.x + 20, y, 300, 22), "Banka / liman", Sty.h2);
                string berr = Rules.BankTradeError(s, me, tGive, tGet);
                string info = berr ?? "Bankaya " + T.Cards(tGive) + " verip " + T.Cards(tGet) + " alacaksın.";
                GUI.Label(new Rect(r.x + 20, y + 24, r.width - 220, 36), info, Sty.Colored(Sty.small, berr == null ? Sty.Good : Sty.Dim));
                if (Btn(new Rect(r.xMax - 180, y + 14, 160, 38), "Bankayla takas", berr == null, Sty.btnAccent))
                {
                    app.Send(new GameAction { type = Act.BankTrade, give = (int[])tGive.Clone(), get = (int[])tGet.Clone() });
                    ClearTrade();
                }
                y += 64;
            }

            if (players)
            {
                Sty.Fill(new Rect(r.x + 20, y, r.width - 40, 1), Sty.Line);
                y += 10;
                GUI.Label(new Rect(r.x + 20, y, 400, 22), chooseTarget ? "Oyunculara teklif" : Esc(Name(s, s.current)) + " oyuncusuna teklif", Sty.h2);
                y += 28;
                if (tTarget == me || tTarget >= s.players.Count) tTarget = -1;
                int to = chooseTarget ? tTarget : s.current;
                if (chooseTarget)
                {
                    GUI.Label(new Rect(r.x + 20, y + 4, 100, 20), "Kime:", Sty.label);
                    int n = s.players.Count;
                    float avail = r.xMax - 20 - colX;
                    float bw = Mathf.Min(92, (avail - 4 * (n - 1)) / n);
                    float bx = colX;
                    if (Btn(new Rect(bx, y, bw, 26), "Herkes", true, tTarget < 0 ? Sty.btnOn : Sty.btnSmall)) tTarget = -1;
                    bx += bw + 4;
                    for (int p = 0; p < n; p++)
                    {
                        if (p == me) continue;
                        if (Btn(new Rect(bx, y, bw, 26), Esc(s.players[p].name), true, tTarget == p ? Sty.btnOn : Sty.btnSmall)) tTarget = p;
                        bx += bw + 4;
                    }
                    y += 34;
                }
                string perr = Rules.OfferError(s, me, to, tGive, tGet);
                string info = perr ?? (to >= 0 ? Esc(Name(s, to)) + " kabul ederse takas hemen yapılır." : "Kabul edenler arasından kiminle takas yapacağını sen seçersin.");
                GUI.Label(new Rect(r.x + 20, y + 2, r.width - 220, 36), info, Sty.Colored(Sty.small, perr == null ? Sty.Good : Sty.Dim));
                if (Btn(new Rect(r.xMax - 180, y, 160, 38), tCounter > 0 ? "Karşı teklif gönder" : "Teklif gönder", perr == null, Sty.btnAccent))
                {
                    app.Send(new GameAction { type = Act.Offer, give = (int[])tGive.Clone(), get = (int[])tGet.Clone(), target = to, a = tCounter });
                    ClearTrade();
                    Toast("Teklif gönderildi.");
                }
            }
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
                GUI.Label(new Rect(row.x + 14, row.y + 5, row.width - 80, 22), (i + 1) + ". " + Esc(s.players[p].name) + (hidden > 0 ? "  (" + hidden + " gizli zafer kartı)" : ""), Sty.label);
                GUI.Label(new Rect(row.xMax - 60, row.y + 2, 52, 26), Rules.TotalVP(s, p).ToString(), Sty.Colored(Sty.centerBig, Sty.Brass));
            }
            float by = r.yMax - 60;
            if (app.isHost)
            {
                if (Btn(new Rect(r.x + 20, by, 200, 42), "Lobiye dön", true, Sty.btnAccent)) app.BackToLobby();
            }
            else GUI.Label(new Rect(r.x + 20, by + 10, 240, 24), "Kurucu lobiye döndürebilir.", Sty.dim);
            if (Btn(new Rect(r.xMax - 180, by, 160, 42), "Ana menü", true, Sty.btn)) app.Leave();
        }
    }
}
