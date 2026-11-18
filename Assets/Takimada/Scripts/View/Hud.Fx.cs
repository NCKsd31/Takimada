using System.Collections.Generic;
using UnityEngine;

namespace Takimada
{
    /// <summary>
    /// Oyun içi efektler: kazanılan kaynakların kaynağından (karo, banka, diğer oyuncu) ele ya da
    /// rakibin paneline uçan sembolleri ve tur süresi göstergesi.
    /// </summary>
    public partial class Hud
    {
        struct Fly
        {
            public int res;          // -1 = kapalı kart (ne olduğu bu oyuncuya gizli)
            public Vector2 from, to; // GUI (ölçekli) koordinatları
            public float start, dur;
        }

        readonly List<Fly> flies = new List<Fly>();
        float stateAt;
        const float FlyDur = 0.95f, FlySize = 36f;
        const int MaxFliesPerKind = 8;

        // ---------------- süre ----------------

        /// <summary>Sunucunun gönderdiği kalan süreden, durum geldikten beri geçen zaman düşülür. -1 = süre yok.</summary>
        float TimerLeft(GameState s)
        {
            if (s == null || s.timerLeft < 0 || s.phase == Phase.GameOver) return -1;
            return Mathf.Max(0, s.timerLeft - (Time.realtimeSinceStartup - stateAt));
        }

        void DrawTimer(GameState s, Rect banner, float left, bool mine)
        {
            float total = Mathf.Max(1, s.timerTotal);
            float k = Mathf.Clamp01(left / total);
            bool low = left <= Mathf.Min(5f, total * 0.34f);
            var col = low ? Sty.Danger : Sty.Brass;
            if (low && mine) col = Color.Lerp(Sty.Danger, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 10f)));
            var bar = new Rect(banner.x + 6, banner.yMax - 6, banner.width - 6, 6);
            Sty.Fill(bar, new Color(0, 0, 0, 0.35f));
            Sty.Fill(new Rect(bar.x, bar.y, bar.width * k, bar.height), col);
            var num = new Rect(banner.xMax - 112 - 64, banner.y + 10, 58, 34);
            Label(num, Mathf.CeilToInt(left).ToString(), Sty.Colored(Sty.centerBig, col));
            Label(new Rect(num.x, num.yMax - 2, num.width, 16), "saniye", Sty.Colored(Sty.small, Sty.Dim));
        }

        // ---------------- uçan semboller ----------------

        Vector2 HandSlot(int res) => HandCenter(res);

        // oyuncu satırındaki arkası dönük kaynak kartı (DrawPlayers ile aynı ölçüler)
        Vector2 PlayerSlot(int p) => new Vector2(12 + 8 + 14 + 16, 12 + 36 + p * 90 + 34 + 22);

        Vector2 BankSlot() => new Vector2(sw - 312 + 150, 12 + 10 + 36 + 9);

        Vector2 Dest(GameState s, int p, int res, int viewer) =>
            p == viewer && viewer >= 0 && res >= 0 ? HandSlot(res) : PlayerSlot(p);

        bool WorldToGui(Vector3 world, out Vector2 gui)
        {
            if (!Board.Project(world, out var raw, out _)) { gui = Vector2.zero; return false; }
            gui = raw / scale;
            return true;
        }

        /// <summary>İki durum arasında artan kaynakları bulur ve her biri için uçan sembol oluşturur.</summary>
        void SpawnFlies(GameState prev, GameState s)
        {
            if (prev == null || s == null || prev.players.Count != s.players.Count) return;
            if (!Board.Matches(s.board)) return;
            int n = s.players.Count;
            int viewer = app.Me;
            var delta = new int[n, T.ResCount];
            for (int p = 0; p < n; p++)
                for (int r = 0; r < T.ResCount; r++)
                    delta[p, r] = s.players[p].res[r] - prev.players[p].res[r];

            bool production = s.rolled && (!prev.rolled || prev.turn != s.turn) && s.lastProduction != null && s.lastProduction.Length > 0;
            bool setupGain = prev.phase == Phase.SetupSettlement && s.phase != Phase.SetupSettlement && s.lastSetupVertex >= 0;
            bool robbery = prev.phase == Phase.MoveRobber || prev.phase == Phase.Steal;
            float now = Time.realtimeSinceStartup;

            for (int p = 0; p < n; p++)
                for (int r = 0; r < T.ResCount; r++)
                {
                    int d = delta[p, r];
                    if (d <= 0) continue;
                    int count = Mathf.Min(d, MaxFliesPerKind);
                    var sources = new List<Vector2>();
                    bool hidden = false;

                    if (production)
                    {
                        // sayısı gelen ve p'nin yapısı olan karolar; kasaba iki kez sayılır
                        foreach (var h in s.lastProduction)
                        {
                            if (h < 0 || h >= s.board.hexes.Count || T.TerrainRes(s.board.hexes[h].terrain) != r) continue;
                            if (!WorldToGui(Board.TokenAnchor(h), out var at)) continue;
                            foreach (var v in s.board.hexes[h].verts)
                                if (s.vOwner[v] == p)
                                    for (int k = 0; k < s.vLevel[v]; k++) sources.Add(at);
                        }
                    }
                    else if (setupGain)
                    {
                        foreach (var h in s.board.verts[s.lastSetupVertex].hexes)
                            if (T.TerrainRes(s.board.hexes[h].terrain) == r && WorldToGui(Board.TokenAnchor(h), out var at)) sources.Add(at);
                    }

                    if (sources.Count == 0)
                    {
                        // takas, çalma, tekel: aynı kaynağı kaybeden oyuncudan; yoksa bankadan
                        int loser = -1;
                        for (int q = 0; q < n; q++) if (q != p && delta[q, r] < 0) { loser = q; break; }
                        if (loser >= 0)
                        {
                            sources.Add(loser == viewer ? HandSlot(r) : PlayerSlot(loser));
                            // çalınan kartın ne olduğunu yalnızca çalan ve çalınan görür
                            hidden = robbery && viewer != p && viewer != loser;
                        }
                        else sources.Add(BankSlot());
                    }

                    var to = Dest(s, p, hidden ? -1 : r, viewer);
                    for (int k = 0; k < count; k++)
                    {
                        var from = sources[k % sources.Count];
                        // aynı noktadan çıkanlar biraz dağılsın
                        from += new Vector2((k % 3 - 1) * 10f, -(k / 3) * 8f);
                        flies.Add(new Fly
                        {
                            res = hidden ? -1 : r,
                            from = from,
                            to = to,
                            start = now + 0.12f * k + 0.05f * r + (production ? 0.25f : 0f),
                            dur = FlyDur
                        });
                    }
                }
            if (flies.Count > 120) flies.RemoveRange(0, flies.Count - 120);
        }

        void DrawFlies()
        {
            if (flies.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            flies.RemoveAll(f => now > f.start + f.dur);
            var old = GUI.color;
            foreach (var f in flies)
            {
                float t = (now - f.start) / f.dur;
                if (t < 0) continue;
                float e = t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2; // yumuşak hızlanıp yavaşlama
                var mid = (f.from + f.to) / 2 + new Vector2(0, -Mathf.Min(140, Vector2.Distance(f.from, f.to) * 0.35f));
                var a = Vector2.Lerp(f.from, mid, e);
                var b = Vector2.Lerp(mid, f.to, e);
                var p = Vector2.Lerp(a, b, e);
                float size = FlySize * (t < 0.15f ? Mathf.Lerp(0.4f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 0.75f, (t - 0.15f) / 0.85f));
                float alpha = t > 0.85f ? Mathf.Lerp(1, 0, (t - 0.85f) / 0.15f) : 1;
                var rect = new Rect(p.x - size / 2, p.y - size / 2, size, size);
                var inner = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);
                FillA(rect, Pal.Ink, alpha);
                if (f.res >= 0)
                {
                    FillA(inner, Pal.Res[f.res], alpha);
                    GUI.color = new Color(1, 1, 1, alpha);
                    GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8), Icons.Res(f.res), ScaleMode.ScaleToFit, true);
                }
                else
                {
                    FillA(inner, Sty.Panel2, alpha);
                    GUI.color = new Color(1, 1, 1, alpha);
                    Label(rect, "?", Sty.Colored(Sty.centerBig, Sty.Brass));
                }
                GUI.color = old;
            }
            GUI.color = old;
        }

        static void FillA(Rect r, Color c, float alpha)
        {
            c.a *= alpha;
            Sty.Fill(r, c);
        }
    }
}
