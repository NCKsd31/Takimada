using System;
using System.Collections.Generic;

namespace Takimada
{
    /// <summary>Durumu değiştirmeyen sorgular. İstemci de bunları kullanarak geçerli yerleri vurgular.</summary>
    public static class Rules
    {
        public static int Actor(GameState s) => s.phase == Phase.SpecialBuild ? s.specialBuilder : s.current;

        public static bool IsBuildPhase(GameState s) => s.phase == Phase.Main || s.phase == Phase.SpecialBuild;

        public static int HandSize(GameState s, int p) => Util.Sum(s.players[p].res);

        public static bool CanSettle(GameState s, int p, int v, bool setup)
        {
            if (v < 0 || v >= s.vOwner.Length) return false;
            if (s.vOwner[v] != -1) return false;
            if (s.players[p].settlementsLeft <= 0) return false;
            var vert = s.board.verts[v];
            foreach (var n in vert.verts) if (s.vOwner[n] != -1) return false; // mesafe kuralı
            if (setup) return true;
            foreach (var e in vert.edges) if (s.eOwner[e] == p) return true;
            return false;
        }

        public static bool CanRoad(GameState s, int p, int e, int mustTouch = -1)
        {
            if (e < 0 || e >= s.eOwner.Length) return false;
            if (s.eOwner[e] != -1) return false;
            if (s.players[p].roadsLeft <= 0) return false;
            var edge = s.board.edges[e];
            if (mustTouch >= 0) return edge.a == mustTouch || edge.b == mustTouch;
            return Connects(s, p, edge.a, e) || Connects(s, p, edge.b, e);
        }

        static bool Connects(GameState s, int p, int v, int exceptEdge)
        {
            if (s.vOwner[v] == p) return true;
            if (s.vOwner[v] != -1) return false; // rakip yapısı yolu keser
            foreach (var e in s.board.verts[v].edges)
                if (e != exceptEdge && s.eOwner[e] == p) return true;
            return false;
        }

        public static bool CanCity(GameState s, int p, int v)
        {
            if (v < 0 || v >= s.vOwner.Length) return false;
            return s.vOwner[v] == p && s.vLevel[v] == 1 && s.players[p].citiesLeft > 0;
        }

        public static List<int> SettleSpots(GameState s, int p, bool setup)
        {
            var r = new List<int>();
            for (int v = 0; v < s.vOwner.Length; v++) if (CanSettle(s, p, v, setup)) r.Add(v);
            return r;
        }

        public static List<int> RoadSpots(GameState s, int p)
        {
            var r = new List<int>();
            int must = s.phase == Phase.SetupRoad ? s.lastSetupVertex : -1;
            for (int e = 0; e < s.eOwner.Length; e++) if (CanRoad(s, p, e, must)) r.Add(e);
            return r;
        }

        public static List<int> CitySpots(GameState s, int p)
        {
            var r = new List<int>();
            for (int v = 0; v < s.vOwner.Length; v++) if (CanCity(s, p, v)) r.Add(v);
            return r;
        }

        public static List<int> RobberSpots(GameState s)
        {
            var r = new List<int>();
            for (int h = 0; h < s.board.hexes.Count; h++)
            {
                if (h == s.robber) continue;
                if (s.cfg.friendlyRobberVP > 0 && ProtectedHex(s, h)) continue;
                r.Add(h);
            }
            if (r.Count == 0)
                for (int h = 0; h < s.board.hexes.Count; h++) if (h != s.robber) r.Add(h);
            return r;
        }

        /// <summary>Dostane korsan: karoda düşük puanlı bir rakip varsa o karo korunur.</summary>
        public static bool ProtectedHex(GameState s, int h)
        {
            foreach (var v in s.board.hexes[h].verts)
            {
                int o = s.vOwner[v];
                if (o >= 0 && o != s.current && PublicVP(s, o) <= s.cfg.friendlyRobberVP) return true;
            }
            return false;
        }

        public static List<int> StealTargets(GameState s, int p, int hex)
        {
            var r = new List<int>();
            if (hex < 0) return r;
            foreach (var v in s.board.hexes[hex].verts)
            {
                int o = s.vOwner[v];
                if (o < 0 || o == p || r.Contains(o)) continue;
                if (HandSize(s, o) == 0) continue;
                if (s.cfg.friendlyRobberVP > 0 && PublicVP(s, o) <= s.cfg.friendlyRobberVP) continue;
                r.Add(o);
            }
            return r;
        }

        public static int Ratio(GameState s, int p, int res)
        {
            int best = s.cfg.bankRatio;
            if (!s.cfg.harborsEnabled) return best;
            for (int v = 0; v < s.vOwner.Length; v++)
            {
                if (s.vOwner[v] != p) continue;
                int h = s.board.verts[v].harbor;
                if (h == T.GenericHarbor) best = Math.Min(best, s.cfg.genericHarborRatio);
                else if (h == res) best = Math.Min(best, s.cfg.specificHarborRatio);
            }
            return best;
        }

        public static int CountBuildings(GameState s, int p, int level)
        {
            int n = 0;
            for (int v = 0; v < s.vOwner.Length; v++) if (s.vOwner[v] == p && s.vLevel[v] == level) n++;
            return n;
        }

        /// <summary>Herkesin görebildiği puan (gizli zafer kartları hariç).</summary>
        public static int PublicVP(GameState s, int p)
        {
            int vp = CountBuildings(s, p, 1) * s.cfg.settlementVP + CountBuildings(s, p, 2) * s.cfg.cityVP;
            if (s.longestHolder == p) vp += s.cfg.longestRoadVP;
            if (s.armyHolder == p) vp += s.cfg.largestArmyVP;
            return vp;
        }

        public static int HiddenVP(GameState s, int p)
        {
            int n = 0;
            foreach (var d in s.players[p].dev) if (d.type == DevCard.Zafer) n++;
            return n;
        }

        public static int TotalVP(GameState s, int p) => PublicVP(s, p) + HiddenVP(s, p);

        public static string CanPlayDev(GameState s, int p, DevCard type)
        {
            if (!s.cfg.devCardsEnabled) return "Gelişim kartları kapalı.";
            if (p != s.current) return "Sıra sende değil.";
            if (s.phase != Phase.Roll && s.phase != Phase.Main) return "Şu an kart oynanamaz.";
            if (type == DevCard.Zafer || type == DevCard.Gizli) return "Zafer kartları oynanmaz, otomatik sayılır.";
            if (s.devPlays >= s.cfg.maxDevPlaysPerTurn) return "Bu tur kart oynama hakkın doldu.";
            foreach (var d in s.players[p].dev)
                if (d.type == type && (s.cfg.playDevSameTurn || d.boughtTurn < s.turn)) return null;
            return "Bu karttan oynanabilir olan yok (bu tur alınan kart oynanamaz).";
        }

        /// <summary>Bir oyuncunun en uzun kesintisiz yolu. Rakip yapıları yolu böler.</summary>
        public static int LongestRoad(GameState s, int p)
        {
            int best = 0;
            var used = new bool[s.eOwner.Length];
            for (int v = 0; v < s.vOwner.Length; v++)
            {
                bool touches = false;
                foreach (var e in s.board.verts[v].edges) if (s.eOwner[e] == p) { touches = true; break; }
                if (!touches) continue;
                best = Math.Max(best, Dfs(s, p, v, used, true));
            }
            return best;
        }

        static int Dfs(GameState s, int p, int v, bool[] used, bool start)
        {
            if (!start && s.vOwner[v] != -1 && s.vOwner[v] != p) return 0;
            int best = 0;
            foreach (var e in s.board.verts[v].edges)
            {
                if (used[e] || s.eOwner[e] != p) continue;
                used[e] = true;
                var edge = s.board.edges[e];
                int other = edge.a == v ? edge.b : edge.a;
                best = Math.Max(best, 1 + Dfs(s, p, other, used, false));
                used[e] = false;
            }
            return best;
        }

        public static bool OfferParty(GameState s, TradeOffer o, int p)
        {
            if (p == o.from || p < 0 || p >= s.players.Count) return false;
            if (o.to >= 0) return p == o.to;
            if (!s.cfg.tradeOnlyWithActive) return true;
            // aktif oyuncu her işlemin tarafı olmak zorunda
            return o.from == s.current || p == s.current;
        }

        public static List<int> OfferParties(GameState s, TradeOffer o)
        {
            var r = new List<int>();
            for (int p = 0; p < s.players.Count; p++) if (OfferParty(s, o, p)) r.Add(p);
            return r;
        }

        /// <summary>Verilen kartların bankada kaç kart ettiği. exact=false ise bir kaynak oranın katı değil.</summary>
        public static int BankCredit(GameState s, int p, int[] give, out bool exact)
        {
            int credit = 0;
            exact = true;
            for (int r = 0; r < T.ResCount; r++)
            {
                if (give[r] <= 0) continue;
                int ratio = Ratio(s, p, r);
                credit += give[r] / ratio;
                if (give[r] % ratio != 0) exact = false;
            }
            return credit;
        }

        /// <summary>Bankayla takas geçerli mi? null = geçerli, değilse sebebi.</summary>
        public static string BankTradeError(GameState s, int p, int[] give, int[] get)
        {
            if (!s.cfg.bankTrade) return "Banka ticareti kapalı.";
            if (s.phase != Phase.Main) return "Ticaret yalnızca zar attıktan sonra yapılır.";
            if (p != s.current) return "Bankayla yalnızca sırası gelen oyuncu takas yapabilir.";
            if (!Util.Valid5(give) || !Util.Valid5(get)) return "Geçersiz takas.";
            for (int r = 0; r < T.ResCount; r++)
                if (give[r] > 0 && get[r] > 0) return "Aynı kaynağı hem verip hem alamazsın.";
            int want = Util.Sum(get);
            if (Util.Sum(give) == 0 || want == 0) return "Vereceğin ve alacağın kartları seç.";
            if (!Util.Has(s.players[p].res, give)) return "Bu kartlar elinde yok.";
            for (int r = 0; r < T.ResCount; r++)
            {
                int ratio = Ratio(s, p, r);
                if (give[r] > 0 && give[r] % ratio != 0)
                    return T.Res[r] + " oranın " + ratio + ":1, " + ratio + "'in katı kadar vermelisin.";
            }
            int credit = BankCredit(s, p, give, out _);
            if (want != credit) return "Bu kartlar bankada " + credit + " kart eder, " + want + " kart seçtin.";
            if (!s.cfg.unlimitedBank && !Util.Has(s.bank, get)) return "Bankada o kadar kaynak yok.";
            return null;
        }

        /// <summary>Oyuncu teklifi geçerli mi? to = -1 herkese açık.</summary>
        public static string OfferError(GameState s, int p, int to, int[] give, int[] get)
        {
            if (!s.cfg.playerTrade) return "Oyuncular arası ticaret kapalı.";
            if (s.phase != Phase.Main) return "Ticaret yalnızca sırası gelen oyuncu zar attıktan sonra yapılır.";
            if (!Util.Valid5(give) || !Util.Valid5(get)) return "Geçersiz teklif.";
            int sg = Util.Sum(give), sr = Util.Sum(get);
            if (sg == 0 && sr == 0) return "Teklif boş.";
            if (!s.cfg.allowGifts && (sg == 0 || sr == 0)) return "Hediye yok: iki taraf da en az bir kart koymalı.";
            for (int r = 0; r < T.ResCount; r++)
                if (give[r] > 0 && get[r] > 0) return "Aynı kaynağı hem verip hem isteyemezsin.";
            if (!Util.Has(s.players[p].res, give)) return "Teklif ettiğin kartlar elinde yok.";
            if (to == p) return "Kendine teklif yapamazsın.";
            if (to < -1 || to >= s.players.Count) return "Geçersiz oyuncu.";
            if (s.cfg.tradeOnlyWithActive && p != s.current && to != s.current)
                return "Yalnızca sırası gelen oyuncuyla takas yapabilirsin.";
            return null;
        }
    }
}
