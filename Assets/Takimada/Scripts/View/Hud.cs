using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Takimada
{
    /// <summary>Tüm ekranlar (IMGUI). Referans çözünürlük 860 piksel yükseklik; her ekrana ölçeklenir.</summary>
    public partial class Hud
    {
        readonly App app;
        BoardView Board => app.board;

        float scale = 1, sw = 1600, sh = 860;
        readonly List<Rect> blockers = new List<Rect>();
        List<Rect> lastBlockers = new List<Rect>();

        enum BuildMode { None, Road, Settlement, City }
        BuildMode build;
        BoardView.PickKind pickKind;
        List<int> candidates;
        int hover = -1;

        Vector2 mouseRaw, downPos;
        bool mouseDown, dragging;
        int dragButton;
        float flash;
        bool cameraFitted;

        struct ToastItem { public string text; public float until; public bool err; }
        readonly List<ToastItem> toasts = new List<ToastItem>();

        public Hud(App app) { this.app = app; }

        public void Toast(string text, bool err = false)
        {
            toasts.Add(new ToastItem { text = text, until = Time.time + (err ? 4.5f : 3f), err = err });
            if (toasts.Count > 5) toasts.RemoveAt(0);
        }

        // =====================================================================

        public void Tick()
        {
            var hc = app.state != null ? app.state.cfg : app.lobby.cfg;
            if (hc != null) Board.SetHarborRatios(hc.genericHarborRatio, hc.specificHarborRatio);
            if (app.view != View.Game) { TickPreview(); Board.UpdateOverlay(null, BoardView.PickKind.None, null, -1, Color.white, 0); return; }
            Board.SetVisible(true);
            previewKey = "";
            ComputePick();
            hover = candidates == null ? -1 : Board.Nearest(app.state, pickKind, candidates, Board.MouseWorld(mouseRaw));
            flash = Mathf.Max(0, flash - Time.deltaTime * 0.6f);
            int me = app.Me;
            var accent = me >= 0 ? Pal.PlayerColor(app.state.players[me].color) : Color.white;
            Board.UpdateOverlay(app.state, pickKind, candidates, hover, accent, flash, build == BuildMode.City ? 2 : 1);
        }

        public void OnState(GameState prev, GameState s)
        {
            if (prev == null) { build = BuildMode.None; cameraFitted = false; ResetDialogs(); }
            if (prev != null && s.rolled && (!prev.rolled || prev.turn != s.turn)) flash = 1f;
            if (prev != null && prev.current != s.current && s.phase == Phase.Roll && app.Me == s.current && !app.isLocal)
                Toast("Sıra sende!");
            if (!Rules.IsBuildPhase(s) || app.Me != Rules.Actor(s)) build = BuildMode.None;
            if (prev == null || prev.phase != s.phase) { discardSel = new int[5]; }
            if (prev != null && prev.board.hexes.Exists(h => h.number < 0) && !s.board.hexes.Exists(h => h.number < 0))
                Toast("Sayı pulları dağıtıldı!");
            if (prev != null && !app.isLocal) TradeToasts(prev, s);
            if (!cameraFitted) FitCamera();
        }

        public void FitCamera()
        {
            float s = scale <= 0 ? 1 : scale;
            if (app.view == View.Game) Board.Fit(290 * s, 324 * s, 166 * s, 90 * s);
            else if (app.view == View.Lobby) Board.Fit(340 * s, 610 * s, 40 * s, 40 * s);
            else Board.Fit(520 * s, 40 * s, 40 * s, 40 * s);
            cameraFitted = true;
        }

        // ---------------- menü/lobi arka planı: harita önizlemesi ----------------

        string previewKey = "";
        public int previewSeed = 1234;
        View lastView = View.Game;

        void TickPreview()
        {
            Board.SetVisible(true);
            if (app.view == View.Menu) Board.Orbit(Time.deltaTime * 3f, 0);
            var c = app.lobby.cfg;
            string key = c.rowLengths + "|" + string.Join(",", c.terrainCounts) + "|" + c.numberTokens + "|" + c.harborsEnabled + c.genericHarbors + c.specificHarborsPerResource + "|" + c.shuffleNumbers + c.separateRedNumbers + "|" + (c.mapSeed != 0 ? c.mapSeed : previewSeed) + c.numbersAfterSetup;
            if (key != previewKey || lastView != app.view)
            {
                previewKey = key;
                lastView = app.view;
                var cc = c.Clone();
                cc.Sanitize();
                var b = BoardGen.Generate(cc, new System.Random(c.mapSeed != 0 ? c.mapSeed : previewSeed));
                b.fog = c.mapSeed == 0;
                if (!b.fog && c.numbersAfterSetup) BoardGen.HideNumbers(b);
                Board.EnsureBoard(b);
                Board.UpdatePieces(new GameState { board = b, vOwner = new int[0], vLevel = new int[0], eOwner = new int[0], robber = -1, players = new List<PlayerState>() });
                previewBoard = b;
                FitCamera();
            }
        }

        BoardData previewBoard;

        // ---------------- tıklanabilir yerler ----------------

        void ComputePick()
        {
            candidates = null;
            pickKind = BoardView.PickKind.None;
            var s = app.state;
            if (s == null) return;
            int me = app.Me;
            if (me < 0 || me != Rules.Actor(s)) return;
            switch (s.phase)
            {
                case Phase.SetupSettlement:
                    pickKind = BoardView.PickKind.Vertex; candidates = Rules.SettleSpots(s, me, true); break;
                case Phase.SetupRoad:
                case Phase.RoadBuilding:
                    pickKind = BoardView.PickKind.Edge; candidates = Rules.RoadSpots(s, me); break;
                case Phase.MoveRobber:
                    pickKind = BoardView.PickKind.Hex; candidates = Rules.RobberSpots(s); break;
                case Phase.Main:
                case Phase.SpecialBuild:
                    if (build == BuildMode.Road) { pickKind = BoardView.PickKind.Edge; candidates = Rules.RoadSpots(s, me); }
                    else if (build == BuildMode.Settlement) { pickKind = BoardView.PickKind.Vertex; candidates = Rules.SettleSpots(s, me, false); }
                    else if (build == BuildMode.City) { pickKind = BoardView.PickKind.Vertex; candidates = Rules.CitySpots(s, me); }
                    break;
            }
        }

        void Click()
        {
            var s = app.state;
            if (s == null || hover < 0) return;
            switch (pickKind)
            {
                case BoardView.PickKind.Vertex:
                    app.Send(new GameAction { type = build == BuildMode.City ? Act.PlaceCity : Act.PlaceSettlement, target = hover });
                    build = BuildMode.None;
                    break;
                case BoardView.PickKind.Edge:
                    app.Send(new GameAction { type = Act.PlaceRoad, target = hover });
                    build = BuildMode.None;
                    break;
                case BoardView.PickKind.Hex:
                    app.Send(new GameAction { type = Act.MoveRobber, target = hover });
                    break;
            }
        }

        // =====================================================================

        public void Draw()
        {
            Sty.Init();
            var e = Event.current;
            scale = Mathf.Clamp(Screen.height / 860f, 0.6f, 3f);
            sw = Screen.width / scale;
            sh = Screen.height / scale;

            HandleBoardInput(e);
            if (app.view == View.Game && app.state != null && e.type == EventType.Repaint) DrawBoardLabels(app.state.board);
            else if (app.view != View.Game && previewBoard != null && e.type == EventType.Repaint) DrawBoardLabels(previewBoard);

            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            blockers.Clear();
            switch (app.view)
            {
                case View.Menu: DrawMenu(); break;
                case View.Lobby: DrawLobby(); break;
                case View.Game: DrawGame(); break;
            }
            DrawToasts();
            lastBlockers = new List<Rect>(blockers);
            GUI.matrix = Matrix4x4.identity;
        }

        Rect Block(Rect r) { blockers.Add(r); return r; }

        bool OverUI(Vector2 rawMouse)
        {
            var g = rawMouse / scale;
            foreach (var r in lastBlockers) if (r.Contains(g)) return true;
            return false;
        }

        // Sol sürükle: kaydır · Sağ sürükle: döndür/eğ · Tekerlek: yakınlaş · Q/E: döndür · F: görünümü sıfırla
        void HandleBoardInput(Event e)
        {
            if (e.isMouse || e.type == EventType.Repaint || e.type == EventType.ScrollWheel) mouseRaw = e.mousePosition;
            if (Board.cam == null) return;
            bool over = OverUI(e.mousePosition) || BlockingModal();
            switch (e.type)
            {
                case EventType.ScrollWheel:
                    if (over) return;
                    Board.Zoom(1f + Mathf.Clamp(e.delta.y, -6f, 6f) * 0.05f, e.mousePosition);
                    e.Use();
                    break;
                case EventType.MouseDown:
                    if (over) return;
                    mouseDown = true; dragging = false; downPos = e.mousePosition; dragButton = e.button;
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (!mouseDown) return;
                    if ((e.mousePosition - downPos).magnitude > 6) dragging = true;
                    if (dragging)
                    {
                        if (dragButton == 1) Board.Orbit(e.delta.x * 0.3f, e.delta.y * 0.2f);
                        else Board.PanDrag(e.mousePosition - e.delta, e.mousePosition);
                    }
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (!mouseDown) return;
                    mouseDown = false;
                    if (!dragging && app.view == View.Game)
                    {
                        if (e.button == 0) Click();
                        else if (e.button == 1) build = BuildMode.None;
                    }
                    e.Use();
                    break;
                case EventType.KeyDown:
                    if (GUIUtility.keyboardControl != 0) return;
                    if (e.keyCode == KeyCode.Q) { Board.Orbit(-5f, 0); e.Use(); }
                    else if (e.keyCode == KeyCode.E) { Board.Orbit(5f, 0); e.Use(); }
                    else if (e.keyCode == KeyCode.F) { Board.ResetAngles(); FitCamera(); e.Use(); }
                    break;
            }
        }

        // ---------------- karo sayıları ve liman etiketleri ----------------

        // Pul sayıları, noktalar ve liman oranları artık tahtanın üstüne 3B olarak basılıyor (BoardView.BuildLabels).
        // Burada yalnızca harita henüz gizliyken gösterilen başlık çizilir.
        void DrawBoardLabels(BoardData b)
        {
            if (Board.cam == null || !Board.Matches(b)) return;
            bool hidden = b.fog || b.hexes.Exists(h => h.number < 0);
            if (!hidden) return;
            float top = float.MaxValue, cx = 0, ppuSum = 0;
            int seen = 0;
            for (int i = 0; i < b.hexes.Count; i++)
            {
                if (!Board.Project(Board.TokenAnchor(i), out var p, out float ppu)) continue;
                top = Mathf.Min(top, p.y); cx += p.x; ppuSum += ppu; seen++;
            }
            if (seen == 0) return;
            cx /= seen;
            float avg = ppuSum / seen;
            string msg = b.fog ? "Araziler, sayılar ve limanlar oyun başlayınca rastgele dağıtılır"
                               : "Sayı pulları başlangıç yerleşimi bitince rastgele dağıtılacak";
            var size = Sty.label.CalcSize(new GUIContent(msg));
            var r = new Rect(cx - size.x / 2 - 14, Mathf.Max(8, top - avg * 1.3f - size.y), size.x + 28, size.y + 10);
            Sty.Fill(r, new Color(0.04f, 0.1f, 0.13f, 0.85f));
            GUI.Label(r, msg, Sty.center);
        }

        // ---------------- bildirimler ----------------

        void DrawToasts()
        {
            toasts.RemoveAll(t => t.until < Time.time);
            float y = sh - 210;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                var size = Sty.wrap.CalcSize(new GUIContent(t.text));
                float w = Mathf.Min(560, size.x + 40);
                var r = new Rect((sw - w) / 2, y - 40, w, 36);
                Sty.Fill(r, t.err ? new Color(0.35f, 0.08f, 0.06f, 0.95f) : new Color(0.06f, 0.16f, 0.2f, 0.95f));
                Sty.Frame(r, t.err ? Sty.Danger : Sty.Brass, 1);
                GUI.Label(r, t.text, Sty.center);
                y -= 42;
            }
        }

        // ---------------- ortak araçlar ----------------

        bool Btn(Rect r, string text, bool enabled = true, GUIStyle style = null)
        {
            bool old = GUI.enabled;
            GUI.enabled = old && enabled;
            bool clicked = GUI.Button(r, text, style ?? Sty.btn);
            GUI.enabled = old;
            return clicked;
        }

        bool LBtn(string text, bool enabled = true, GUIStyle style = null, params GUILayoutOption[] opts)
        {
            bool old = GUI.enabled;
            GUI.enabled = old && enabled;
            bool clicked = GUILayout.Button(text, style ?? Sty.btn, opts);
            GUI.enabled = old;
            return clicked;
        }

        void Panel(Rect r) => GUI.Box(Block(r), GUIContent.none, Sty.panel);

        void ResChip(Rect r, int res, int count, bool dimmed = false)
        {
            var c = Pal.Res[res];
            if (dimmed) c = Color.Lerp(c, Sty.Panel2, 0.6f);
            Sty.Fill(r, Pal.Ink);
            Sty.Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), c);
            var dark = Sty.Colored(Sty.small, Pal.Ink);
            dark.alignment = TextAnchor.UpperCenter;
            GUI.Label(new Rect(r.x, r.y + 3, r.width, 16), T.Res[res], dark);
            var big = Sty.Colored(Sty.centerBig, Pal.Ink);
            GUI.Label(new Rect(r.x, r.y + 14, r.width, r.height - 14), count.ToString(), big);
        }
    }
}
