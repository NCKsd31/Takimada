using System;
using System.Collections.Generic;
using System.Linq;

namespace Takimada
{
    public struct SeatInfo
    {
        public string name;
        public int color;
        public int seat;
    }

    /// <summary>
    /// Sunucuda çalışan yetkili kural motoru. Her hamle doğrulanır, hata varsa açıklaması döner,
    /// yoksa durum güncellenir. İstemciler yalnızca sonucu görür.
    /// </summary>
    public class Engine
    {
        public GameState S;
        readonly Random rng;     // oyun içi şans: zar, kart destesi, çalma, otomatik hamleler (her oyunda farklı)
        readonly Random mapRng;  // harita üretimi (tohum verilmişse tekrarlanabilir)

        Engine(GameState s, Random r, Random map) { S = s; rng = r; mapRng = map; }

        public static Engine Create(GameConfig cfgIn, List<SeatInfo> seats)
        {
            var cfg = cfgIn.Clone();
            cfg.Sanitize();
            // Oyun içi şans (zar, kart destesi, sıra, çalma) her zaman tohumsuz ve gerçek rastgele.
            // Harita da öyle; yalnızca kurallarda elle "Tohum" girildiyse o haritayı yeniden üretmek için tohum kullanılır.
            var rng = new TrueRandom();
            Random mapRng = cfg.mapSeed != 0 ? new Random(cfg.mapSeed) : new TrueRandom();
            var s = new GameState { cfg = cfg };
            s.board = BoardGen.Generate(cfg, mapRng);
            if (cfg.numbersAfterSetup) BoardGen.HideNumbers(s.board);
            s.vOwner = Enumerable.Repeat(-1, s.board.verts.Count).ToArray();
            s.vLevel = new int[s.board.verts.Count];
            s.eOwner = Enumerable.Repeat(-1, s.board.edges.Count).ToArray();

            var order = seats.ToList();
            if (cfg.randomTurnOrder) Util.Shuffle(order, rng);
            foreach (var st in order)
            {
                s.players.Add(new PlayerState
                {
                    name = st.name, color = st.color, seat = st.seat,
                    res = Util.Copy(cfg.startingResources),
                    roadsLeft = cfg.roadsPerPlayer, settlementsLeft = cfg.settlementsPerPlayer, citiesLeft = cfg.citiesPerPlayer
                });
            }
            for (int r = 0; r < T.ResCount; r++)
                s.bank[r] = cfg.unlimitedBank ? 9999 : Math.Max(0, cfg.bankPerResource - cfg.startingResources[r] * seats.Count);

            for (int t = 0; t < 5; t++)
                for (int i = 0; i < cfg.devDeck[t]; i++) s.devDeck.Add(t);
            Util.Shuffle(s.devDeck, rng);
            s.devDeckCount = s.devDeck.Count;

            s.robber = -1;
            if (cfg.robberEnabled && cfg.robberStartsOnDesert)
                s.robber = s.board.hexes.FindIndex(h => h.terrain == Terrain.Volkan);

            int n = s.players.Count;
            var so = new List<int>();
            for (int round = 0; round < cfg.setupRounds; round++)
                for (int i = 0; i < n; i++)
                    so.Add(cfg.snakeOrder && round % 2 == 1 ? n - 1 - i : i);
            s.setupOrder = so.ToArray();
            s.discardPending = new int[n];
            s.phase = Phase.SetupSettlement;
            s.current = s.setupOrder[0];
            s.turn = 0;
            var e = new Engine(s, rng, mapRng);
            e.Log("Oyun başladı. Hedef: " + cfg.victoryPoints + " zafer puanı.");
            e.Log("Sıra: " + string.Join(", ", s.players.Select(p => p.name)));
            return e;
        }

        void Log(string m)
        {
            S.log.Add(m);
            if (S.log.Count > 300) S.log.RemoveAt(0);
        }

        string N(int p) => S.players[p].name;

        // =====================================================================

        public string Apply(GameAction a)
        {
            if (S.phase == Phase.GameOver) return "Oyun bitti.";
            if (a.player < 0 || a.player >= S.players.Count) return "Geçersiz oyuncu.";
            if (a.give == null || a.give.Length != 5) a.give = new int[5];
            if (a.get == null || a.get.Length != 5) a.get = new int[5];
            var prevPhase = S.phase;
            int prevActor = Rules.Actor(S), prevTurn = S.turn;
            tradeDone = false;
            string err = ApplyInner(a);
            if (err == null) UpdateTimer(prevPhase, prevActor, prevTurn, !autoAct && Completed(a, prevActor));
            return err;
        }

        string ApplyInner(GameAction a)
        {
            string err;
            switch (a.type)
            {
                case Act.PlaceSettlement: err = DoSettlement(a); break;
                case Act.PlaceRoad: err = DoRoad(a); break;
                case Act.PlaceCity: err = DoCity(a); break;
                case Act.Roll: err = DoRoll(a); break;
                case Act.EndTurn: err = DoEndTurn(a); break;
                case Act.BuyDev: err = DoBuyDev(a); break;
                case Act.PlayDev: err = DoPlayDev(a); break;
                case Act.Discard: err = DoDiscard(a); break;
                case Act.MoveRobber: err = DoMoveRobber(a); break;
                case Act.Steal: err = DoSteal(a); break;
                case Act.BankTrade: err = DoBankTrade(a); break;
                case Act.Offer: err = DoOffer(a); break;
                case Act.AcceptOffer: err = DoAccept(a); break;
                case Act.RejectOffer: err = DoReject(a); break;
                case Act.CancelOffer: err = DoCancel(a); break;
                case Act.ConfirmOffer: err = DoConfirm(a); break;
                case Act.Chat:
                    if (string.IsNullOrWhiteSpace(a.text)) return "Boş mesaj.";
                    Log(N(a.player) + ": " + (a.text.Length > 140 ? a.text.Substring(0, 140) : a.text));
                    err = null; break;
                default: err = "Bilinmeyen hamle."; break;
            }
            if (err == null)
            {
                CheckWin();
                PruneOffers();
                S.version++;
            }
            return err;
        }

        bool IsActor(GameAction a) => a.player == Rules.Actor(S);

        // ---------------- Yerleştirme ----------------

        string DoSettlement(GameAction a)
        {
            int p = a.player, v = a.target;
            if (S.phase == Phase.SetupSettlement)
            {
                if (p != S.current) return "Sıra sende değil.";
                if (!Rules.CanSettle(S, p, v, true)) return "Buraya köy kurulamaz (komşu köşeler boş olmalı).";
                Build(p, v, 1);
                int round = S.setupIndex / S.players.Count;
                if (S.cfg.setupResourcesFromLast && round == S.cfg.setupRounds - 1)
                {
                    var gain = new int[5];
                    foreach (var h in S.board.verts[v].hexes)
                    {
                        int r = T.TerrainRes(S.board.hexes[h].terrain);
                        if (r >= 0 && S.bank[r] > gain[r]) gain[r]++;
                    }
                    Util.Add(S.players[p].res, gain);
                    Util.Add(S.bank, gain, -1);
                    if (Util.Sum(gain) > 0) Log(N(p) + " başlangıç kaynağı aldı: " + T.Cards(gain));
                }
                S.lastSetupVertex = v;
                S.phase = Phase.SetupRoad;
                return null;
            }
            if (!Rules.IsBuildPhase(S)) return "Şu an inşa edilemez.";
            if (!IsActor(a)) return "Sıra sende değil.";
            if (!Rules.CanSettle(S, p, v, false)) return "Buraya köy kurulamaz.";
            if (!Pay(p, S.cfg.settlementCost)) return "Yeterli kaynağın yok.";
            Build(p, v, 1);
            Log(N(p) + " bir köy kurdu.");
            UpdateLongestRoad();
            return null;
        }

        void Build(int p, int v, int level)
        {
            S.vOwner[v] = p;
            S.vLevel[v] = level;
            if (level == 1) S.players[p].settlementsLeft--;
        }

        string DoRoad(GameAction a)
        {
            int p = a.player, e = a.target;
            if (S.phase == Phase.SetupRoad)
            {
                if (p != S.current) return "Sıra sende değil.";
                if (!Rules.CanRoad(S, p, e, S.lastSetupVertex)) return "Yol yeni köyüne bitişik olmalı.";
                PlaceRoad(p, e);
                S.setupIndex++;
                if (S.setupIndex >= S.setupOrder.Length) BeginFirstTurn();
                else
                {
                    S.current = S.setupOrder[S.setupIndex];
                    S.phase = Phase.SetupSettlement;
                }
                return null;
            }
            if (S.phase == Phase.RoadBuilding)
            {
                if (p != S.current) return "Sıra sende değil.";
                if (!Rules.CanRoad(S, p, e)) return "Buraya yol yapılamaz.";
                PlaceRoad(p, e);
                S.freeRoads--;
                if (S.freeRoads <= 0 || S.players[p].roadsLeft <= 0 || Rules.RoadSpots(S, p).Count == 0) EndRoadBuilding();
                return null;
            }
            if (!Rules.IsBuildPhase(S)) return "Şu an inşa edilemez.";
            if (!IsActor(a)) return "Sıra sende değil.";
            if (!Rules.CanRoad(S, p, e)) return "Buraya yol yapılamaz (kendi yoluna/yapına bağlı olmalı).";
            if (!Pay(p, S.cfg.roadCost)) return "Yeterli kaynağın yok.";
            PlaceRoad(p, e);
            Log(N(p) + " yol yaptı.");
            return null;
        }

        void PlaceRoad(int p, int e)
        {
            S.eOwner[e] = p;
            S.players[p].roadsLeft--;
            UpdateLongestRoad();
        }

        void EndRoadBuilding()
        {
            S.freeRoads = 0;
            S.phase = S.rolled ? Phase.Main : Phase.Roll;
        }

        string DoCity(GameAction a)
        {
            int p = a.player, v = a.target;
            if (!Rules.IsBuildPhase(S)) return "Şu an inşa edilemez.";
            if (!IsActor(a)) return "Sıra sende değil.";
            if (!Rules.CanCity(S, p, v)) return "Kasaba yalnızca kendi köyünün yerine kurulur.";
            if (!Pay(p, S.cfg.cityCost)) return "Yeterli kaynağın yok.";
            S.vLevel[v] = 2;
            S.players[p].citiesLeft--;
            S.players[p].settlementsLeft++;
            Log(N(p) + " köyünü kasabaya çevirdi.");
            return null;
        }

        bool Pay(int p, int[] cost)
        {
            var res = S.players[p].res;
            if (!Util.Has(res, cost)) return false;
            Util.Add(res, cost, -1);
            Util.Add(S.bank, cost);
            return true;
        }

        void BeginFirstTurn()
        {
            S.current = 0;
            S.turn = 1;
            S.round = 1;
            S.rolled = false;
            S.devPlays = 0;
            S.phase = Phase.Roll;
            S.lastSetupVertex = -1;
            if (S.cfg.numbersAfterSetup)
            {
                BoardGen.AssignNumbers(S.board, S.cfg, mapRng);
                Log("Sayı pulları dağıtıldı!");
            }
            Log("Başlangıç bitti. İlk tur: " + N(0));
        }

        // ---------------- Zar ve üretim ----------------

        string DoRoll(GameAction a)
        {
            if (S.phase != Phase.Roll) return "Şu an zar atılamaz.";
            if (a.player != S.current) return "Sıra sende değil.";
            if (S.cfg.diceMode == 1)
            {
                if (S.diceDeck.Count <= 5)
                {
                    S.diceDeck.Clear();
                    for (int x = 1; x <= 6; x++) for (int y = 1; y <= 6; y++) S.diceDeck.Add(x * 10 + y);
                    Util.Shuffle(S.diceDeck, rng);
                }
                int c = S.diceDeck[S.diceDeck.Count - 1];
                S.diceDeck.RemoveAt(S.diceDeck.Count - 1);
                S.d1 = c / 10; S.d2 = c % 10;
            }
            else
            {
                S.d1 = rng.Next(1, 7);
                S.d2 = rng.Next(1, 7);
            }
            S.rolled = true;
            int sum = S.d1 + S.d2;
            Log(N(a.player) + " zar attı: " + S.d1 + " + " + S.d2 + " = " + sum);

            bool robberRoll = S.cfg.robberEnabled && sum == S.cfg.robberRoll;
            if (robberRoll && S.round <= S.cfg.robberGraceRounds)
            {
                Log("Korsanlar henüz gelmedi (koruma turu).");
                S.lastProduction = new int[0];
                S.phase = Phase.Main;
                return null;
            }
            if (robberRoll)
            {
                S.lastProduction = new int[0];
                bool any = false;
                for (int p = 0; p < S.players.Count; p++)
                {
                    int hand = Rules.HandSize(S, p);
                    S.discardPending[p] = hand > S.cfg.discardLimit ? hand / 2 : 0;
                    if (S.discardPending[p] > 0) { any = true; Log(N(p) + " " + S.discardPending[p] + " kart atmalı."); }
                }
                S.phase = any ? Phase.Discard : Phase.MoveRobber;
                if (!any) Log("Korsan geliyor! " + N(S.current) + " korsanı taşıyacak.");
                return null;
            }
            Produce(sum);
            S.phase = Phase.Main;
            return null;
        }

        void Produce(int sum)
        {
            int n = S.players.Count;
            var want = new int[n, 5];
            var prodHexes = new List<int>();
            for (int h = 0; h < S.board.hexes.Count; h++)
            {
                var hex = S.board.hexes[h];
                if (hex.number != sum) continue;
                if (S.cfg.robberBlocksProduction && h == S.robber) continue;
                int r = T.TerrainRes(hex.terrain);
                if (r < 0) continue;
                prodHexes.Add(h);
                foreach (var v in hex.verts)
                {
                    int o = S.vOwner[v];
                    if (o < 0) continue;
                    want[o, r] += S.vLevel[v] == 2 ? S.cfg.cityProduction : S.cfg.settlementProduction;
                }
            }
            S.lastProduction = prodHexes.ToArray();
            var got = new int[n][];
            for (int p = 0; p < n; p++) got[p] = new int[5];
            for (int r = 0; r < 5; r++)
            {
                int total = 0, takers = 0, only = -1;
                for (int p = 0; p < n; p++) if (want[p, r] > 0) { total += want[p, r]; takers++; only = p; }
                if (total == 0) continue;
                if (S.cfg.unlimitedBank || S.bank[r] >= total)
                {
                    for (int p = 0; p < n; p++) got[p][r] = want[p, r];
                }
                else if (takers == 1)
                {
                    got[only][r] = S.bank[r];
                    Log("Bankada yeterli " + T.Res[r] + " yok; " + N(only) + " kalanı aldı.");
                }
                else
                {
                    Log("Bankada yeterli " + T.Res[r] + " yok; kimse alamadı.");
                }
            }
            for (int p = 0; p < n; p++)
            {
                if (Util.Sum(got[p]) == 0) continue;
                Util.Add(S.players[p].res, got[p]);
                if (!S.cfg.unlimitedBank) Util.Add(S.bank, got[p], -1);
                Log(N(p) + " aldı: " + T.Cards(got[p]));
            }
        }

        string DoDiscard(GameAction a)
        {
            if (S.phase != Phase.Discard) return "Şu an kart atılmıyor.";
            int p = a.player;
            int need = S.discardPending[p];
            if (need <= 0) return "Kart atman gerekmiyor.";
            if (!Util.Valid5(a.give) || Util.Sum(a.give) != need) return need + " kart seçmelisin.";
            if (!Util.Has(S.players[p].res, a.give)) return "Bu kartlar elinde yok.";
            Util.Add(S.players[p].res, a.give, -1);
            Util.Add(S.bank, a.give);
            S.discardPending[p] = 0;
            Log(N(p) + " " + need + " kart attı.");
            if (S.discardPending.All(x => x == 0))
            {
                S.phase = Phase.MoveRobber;
                Log(N(S.current) + " korsanı taşıyacak.");
            }
            return null;
        }

        string DoMoveRobber(GameAction a)
        {
            if (S.phase != Phase.MoveRobber) return "Şu an korsan taşınmaz.";
            if (a.player != S.current) return "Sıra sende değil.";
            int h = a.target;
            if (!Rules.RobberSpots(S).Contains(h)) return "Korsan oraya taşınamaz.";
            S.robber = h;
            Log(N(a.player) + " korsanı " + T.Terrain[(int)S.board.hexes[h].terrain] + " (" + S.board.hexes[h].number + ") karosuna taşıdı.");
            var targets = Rules.StealTargets(S, a.player, h);
            if (targets.Count == 0) FinishRobber();
            else if (targets.Count == 1) { StealFrom(a.player, targets[0]); FinishRobber(); }
            else S.phase = Phase.Steal;
            return null;
        }

        string DoSteal(GameAction a)
        {
            if (S.phase != Phase.Steal) return "Şu an çalma yok.";
            if (a.player != S.current) return "Sıra sende değil.";
            if (!Rules.StealTargets(S, a.player, S.robber).Contains(a.target)) return "Bu oyuncudan çalınamaz.";
            StealFrom(a.player, a.target);
            FinishRobber();
            return null;
        }

        void StealFrom(int p, int victim)
        {
            var vr = S.players[victim].res;
            int total = Util.Sum(vr);
            if (total == 0) return;
            int pick = rng.Next(total);
            for (int r = 0; r < 5; r++)
            {
                if (pick < vr[r]) { vr[r]--; S.players[p].res[r]++; break; }
                pick -= vr[r];
            }
            Log(N(p) + ", " + N(victim) + " oyuncusundan 1 kart çaldı.");
        }

        void FinishRobber() => S.phase = S.rolled ? Phase.Main : Phase.Roll;

        // ---------------- Tur sonu ----------------

        string DoEndTurn(GameAction a)
        {
            if (S.phase == Phase.RoadBuilding && a.player == S.current) { EndRoadBuilding(); return null; }
            if (S.phase == Phase.SpecialBuild)
            {
                if (a.player != S.specialBuilder) return "Ek inşa sırası sende değil.";
                AdvanceSpecialBuild();
                return null;
            }
            if (S.phase != Phase.Main) return "Turu bitirmeden önce zar atmalısın.";
            if (a.player != S.current) return "Sıra sende değil.";
            S.offers.Clear();
            if (S.cfg.specialBuildPhase && S.players.Count > 1)
            {
                S.specialBuilder = (S.current + 1) % S.players.Count;
                S.phase = Phase.SpecialBuild;
                return null;
            }
            NextTurn();
            return null;
        }

        void AdvanceSpecialBuild()
        {
            S.specialBuilder = (S.specialBuilder + 1) % S.players.Count;
            if (S.specialBuilder == S.current) { S.specialBuilder = -1; NextTurn(); }
        }

        void NextTurn()
        {
            S.current = (S.current + 1) % S.players.Count;
            if (S.current == 0) S.round++;
            S.turn++;
            S.rolled = false;
            S.devPlays = 0;
            S.freeRoads = 0;
            S.offers.Clear();
            S.phase = Phase.Roll;
            Log("— Sıra: " + N(S.current));
        }

        // ---------------- Gelişim kartları ----------------

        string DoBuyDev(GameAction a)
        {
            if (!S.cfg.devCardsEnabled) return "Gelişim kartları kapalı.";
            if (!Rules.IsBuildPhase(S)) return "Şu an kart alınamaz.";
            if (!IsActor(a)) return "Sıra sende değil.";
            if (S.devDeck.Count == 0) return "Deste bitti.";
            if (!Pay(a.player, S.cfg.devCost)) return "Yeterli kaynağın yok.";
            int t = S.devDeck[S.devDeck.Count - 1];
            S.devDeck.RemoveAt(S.devDeck.Count - 1);
            S.devDeckCount = S.devDeck.Count;
            S.players[a.player].dev.Add(new DevEntry { type = (DevCard)t, boughtTurn = S.turn });
            Log(N(a.player) + " bir gelişim kartı aldı.");
            return null;
        }

        string DoPlayDev(GameAction a)
        {
            var type = (DevCard)a.a;
            int p = a.player;
            string err = Rules.CanPlayDev(S, p, type);
            if (err != null) return err;
            var pl = S.players[p];

            switch (type)
            {
                case DevCard.Bereket:
                    if (!Util.Valid5(a.get) || Util.Sum(a.get) != S.cfg.yearOfPlentyCount) return S.cfg.yearOfPlentyCount + " kaynak seçmelisin.";
                    if (!S.cfg.unlimitedBank && !Util.Has(S.bank, a.get)) return "Bankada o kadar kaynak yok.";
                    break;
                case DevCard.Tekel:
                    if (a.target < 0 || a.target > 4) return "Bir kaynak seçmelisin.";
                    break;
            }

            int idx = pl.dev.FindIndex(d => d.type == type && (S.cfg.playDevSameTurn || d.boughtTurn < S.turn));
            pl.dev.RemoveAt(idx);
            S.devPlays++;

            switch (type)
            {
                case DevCard.Muhafiz:
                    pl.knights++;
                    Log(N(p) + " Muhafız oynadı (" + pl.knights + ").");
                    UpdateLargestArmy(p);
                    if (S.cfg.robberEnabled) S.phase = Phase.MoveRobber;
                    break;
                case DevCard.YolYapimi:
                    S.freeRoads = Math.Min(S.cfg.roadBuildingRoads, pl.roadsLeft);
                    Log(N(p) + " Yol Yapımı oynadı.");
                    if (S.freeRoads > 0 && Rules.RoadSpots(S, p).Count > 0) S.phase = Phase.RoadBuilding;
                    break;
                case DevCard.Bereket:
                    Util.Add(pl.res, a.get);
                    if (!S.cfg.unlimitedBank) Util.Add(S.bank, a.get, -1);
                    Log(N(p) + " Bereket oynadı: " + T.Cards(a.get));
                    break;
                case DevCard.Tekel:
                    int r = a.target, taken = 0;
                    for (int o = 0; o < S.players.Count; o++)
                    {
                        if (o == p) continue;
                        taken += S.players[o].res[r];
                        S.players[o].res[r] = 0;
                    }
                    pl.res[r] += taken;
                    Log(N(p) + " Tekel oynadı: " + taken + " " + T.Res[r] + " topladı.");
                    break;
            }
            return null;
        }

        // ---------------- Ticaret ----------------

        string DoBankTrade(GameAction a)
        {
            int p = a.player;
            string err = Rules.BankTradeError(S, p, a.give, a.get);
            if (err != null) return err;
            Util.Add(S.players[p].res, a.give, -1);
            Util.Add(S.bank, a.give);
            Util.Add(S.players[p].res, a.get);
            if (!S.cfg.unlimitedBank) Util.Add(S.bank, a.get, -1);
            Log(N(p) + " bankayla takas yaptı: " + T.Cards(a.give) + " → " + T.Cards(a.get));
            return null;
        }

        string DoOffer(GameAction a)
        {
            int p = a.player;
            int to = a.target < 0 ? -1 : a.target;
            if (S.cfg.tradeOnlyWithActive && p != S.current) to = S.current; // aktif olmayan yalnız aktif oyuncuya teklif yapar
            string err = Rules.OfferError(S, p, to, a.give, a.get);
            if (err != null) return err;

            // aynı kişiye eski teklifin yerini alır; açık teklif sınırı aşılırsa en eskisi düşer
            S.offers.RemoveAll(o => o.from == p && o.to == to);
            var mine = S.offers.Where(o => o.from == p).ToList();
            while (mine.Count >= S.cfg.maxOffersPerPlayer) { S.offers.Remove(mine[0]); mine.RemoveAt(0); }

            var offer = new TradeOffer { id = S.nextOfferId++, from = p, to = to, give = Util.Copy(a.give), get = Util.Copy(a.get) };
            var orig = a.a > 0 ? S.offers.Find(o => o.id == a.a) : null;
            if (orig != null && (orig.to == p || Rules.OfferParty(S, orig, p))) offer.counterOf = orig.id;
            S.offers.Add(offer);
            Log(N(p) + (offer.counterOf > 0 ? " karşı teklif: " : " teklif: ") + Describe(offer.give) + " ↔ " + Describe(offer.get) + (to >= 0 ? " (" + N(to) + ")" : " (herkese)"));
            return null;
        }

        static string Describe(int[] c) => Util.Sum(c) == 0 ? "hiçbir şey" : T.Cards(c);

        string DoAccept(GameAction a)
        {
            if (S.phase != Phase.Main) return "Şu an ticaret yok.";
            var o = S.offers.Find(x => x.id == a.target);
            if (o == null) return "Teklif artık geçerli değil.";
            int p = a.player;
            if (!Rules.OfferParty(S, o, p)) return "Bu teklif sana yapılmadı.";
            if (!Util.Has(S.players[p].res, o.get)) return "İstenen kartlar elinde yok.";
            if (!Util.Has(S.players[o.from].res, o.give)) { S.offers.Remove(o); return "Teklif sahibinin kartları artık yetmiyor."; }
            o.rejected.Remove(p);
            // tek muhatap varsa (kişiye özel teklif veya tek aday) takas hemen olur
            if (o.to >= 0 || Rules.OfferParties(S, o).Count == 1) { ExecuteTrade(o, p); return null; }
            if (!o.accepted.Contains(p))
            {
                o.accepted.Add(p);
                Log(N(p) + ", " + N(o.from) + " oyuncusunun teklifini kabul etti.");
            }
            return null;
        }

        string DoConfirm(GameAction a)
        {
            if (S.phase != Phase.Main) return "Şu an ticaret yok.";
            var o = S.offers.Find(x => x.id == a.target);
            if (o == null) return "Teklif artık geçerli değil.";
            if (o.from != a.player) return "Bu teklif senin değil.";
            int partner = a.a;
            if (!o.accepted.Contains(partner)) return "Bu oyuncu teklifi kabul etmedi.";
            if (!Util.Has(S.players[partner].res, o.get)) { o.accepted.Remove(partner); return N(partner) + " artık o kartlara sahip değil."; }
            if (!Util.Has(S.players[o.from].res, o.give)) { S.offers.Remove(o); return "Kartların artık yetmiyor."; }
            ExecuteTrade(o, partner);
            return null;
        }

        void ExecuteTrade(TradeOffer o, int partner)
        {
            Util.Add(S.players[o.from].res, o.give, -1);
            Util.Add(S.players[partner].res, o.give);
            Util.Add(S.players[partner].res, o.get, -1);
            Util.Add(S.players[o.from].res, o.get);
            S.offers.Remove(o);
            if (o.from == S.current || partner == S.current) tradeDone = true;
            Log(N(o.from) + " ile " + N(partner) + " takas yaptı: " + Describe(o.give) + " ↔ " + Describe(o.get));
        }

        string DoReject(GameAction a)
        {
            var o = S.offers.Find(x => x.id == a.target);
            if (o == null) return null;
            int p = a.player;
            if (!Rules.OfferParty(S, o, p)) return "Bu teklif sana yapılmadı.";
            o.accepted.Remove(p);
            if (!o.rejected.Contains(p)) o.rejected.Add(p);
            if (Rules.OfferParties(S, o).All(q => o.rejected.Contains(q)))
            {
                S.offers.Remove(o);
                Log(N(p) + ", " + N(o.from) + " oyuncusunun teklifini reddetti.");
            }
            return null;
        }

        string DoCancel(GameAction a)
        {
            var o = S.offers.Find(x => x.id == a.target);
            if (o == null) return null;
            if (o.from != a.player) return "Bu teklif senin değil.";
            S.offers.Remove(o);
            return null;
        }

        /// <summary>Her hamleden sonra: kartı yetmeyen teklifler düşer, kartı yetmeyen kabuller geri alınır.</summary>
        void PruneOffers()
        {
            if (S.phase != Phase.Main && S.phase != Phase.MoveRobber && S.phase != Phase.Steal && S.phase != Phase.RoadBuilding) { S.offers.Clear(); return; }
            S.offers.RemoveAll(o => !Util.Has(S.players[o.from].res, o.give));
            foreach (var o in S.offers)
                o.accepted.RemoveAll(q => !Rules.OfferParty(S, o, q) || !Util.Has(S.players[q].res, o.get));
        }

        // ---------------- Ödüller ve kazanma ----------------

        void UpdateLongestRoad()
        {
            int n = S.players.Count;
            for (int p = 0; p < n; p++) S.players[p].roadLength = Rules.LongestRoad(S, p);
            if (!S.cfg.longestRoadEnabled) { S.longestHolder = -1; return; }
            int min = S.cfg.longestRoadMin;
            int max = 0;
            for (int p = 0; p < n; p++) max = Math.Max(max, S.players[p].roadLength);
            var top = Enumerable.Range(0, n).Where(p => S.players[p].roadLength == max).ToList();
            int holder = S.longestHolder;
            int before = holder;

            if (max < min) holder = -1;
            else if (holder >= 0 && S.players[holder].roadLength == max) { /* sahibi korur (beraberlikte de) */ }
            else if (top.Count == 1) holder = top[0];
            else holder = -1; // eski sahip kaybetti ve beraberlik var: kimse almaz

            if (holder != before)
            {
                S.longestHolder = holder;
                if (holder >= 0) Log(N(holder) + " En Uzun Yol ödülünü aldı (" + max + ").");
                else if (before >= 0) Log("En Uzun Yol ödülü kimsede değil.");
            }
        }

        void UpdateLargestArmy(int p)
        {
            if (!S.cfg.largestArmyEnabled) return;
            int k = S.players[p].knights;
            if (k < S.cfg.largestArmyMin || S.armyHolder == p) return;
            if (S.armyHolder >= 0 && S.players[S.armyHolder].knights >= k) return;
            S.armyHolder = p;
            Log(N(p) + " En Güçlü Ordu ödülünü aldı (" + k + " muhafız).");
        }

        void CheckWin()
        {
            if (S.phase == Phase.SetupSettlement || S.phase == Phase.SetupRoad || S.phase == Phase.GameOver) return;
            int p = S.current;
            if (Rules.TotalVP(S, p) >= S.cfg.victoryPoints)
            {
                S.winner = p;
                S.phase = Phase.GameOver;
                S.offers.Clear();
                Log(N(p) + " " + Rules.TotalVP(S, p) + " puanla kazandı!");
            }
        }

        // ---------------- Süre ----------------

        /// <summary>Saniye cinsinden gerçek zaman; App (Unity) bağlar. Motor Unity'den bağımsız kalır.</summary>
        public Func<double> Clock = () => 0;
        double deadline = -1;
        float timerTotal;
        int mainTurn = -1;   // ana süresi başlamış olan tur
        bool autoAct;

        public float TimeLeft => deadline < 0 ? -1f : (float)Math.Max(0, deadline - Clock());
        public float TimeTotal => timerTotal;

        /// <summary>Oyun başlarken ilk aşamanın süresini başlatır (Clock bağlandıktan sonra çağrılır).</summary>
        public void StartClock() => UpdateTimer(Phase.GameOver, -2, -1, false);

        void StartTimer(int seconds)
        {
            timerTotal = seconds;
            deadline = Clock() + seconds;
        }

        bool tradeDone; // bu hamlede sırası gelen oyuncunun taraf olduğu bir takas gerçekleşti

        /// <summary>
        /// Hamle ek süre kazandıran, tamamlanmış bir işlem mi: inşa, kart alma/oynama, banka takası
        /// (sırası gelen yaptıysa) ya da sırası gelen oyuncunun taraf olduğu gerçekleşmiş bir oyuncu takası.
        /// Teklif vermek, reddetmek, iptal etmek süre kazandırmaz.
        /// </summary>
        bool Completed(GameAction a, int actor)
        {
            if (tradeDone) return true;
            if (a.player != actor) return false;
            switch (a.type)
            {
                case Act.PlaceRoad:
                case Act.PlaceSettlement:
                case Act.PlaceCity:
                case Act.BuyDev:
                case Act.PlayDev:
                case Act.BankTrade:
                    return true;
                default:
                    return false;
            }
        }

        void Bonus()
        {
            if (deadline < 0) return;
            deadline += S.cfg.actionBonusSeconds;
            timerTotal = Math.Max(timerTotal, (float)(deadline - Clock()));
        }

        /// <summary>
        /// Aşama değiştiyse süreyi yeniden kurar. Ticaret/inşa süresi tur başına bir kez başlar;
        /// tur içindeki ara aşamalar (muhafızla korsan, yol yapımı) bu süreden yer. Tamamlanan her işlem süreyi uzatır.
        /// </summary>
        void UpdateTimer(Phase prevPhase, int prevActor, int prevTurn, bool byActor)
        {
            var c = S.cfg;
            if (!c.turnTimer || S.phase == Phase.GameOver) { deadline = -1; return; }
            int actor = Rules.Actor(S);
            bool changed = S.phase != prevPhase || actor != prevActor || S.turn != prevTurn;
            bool mainClock = mainTurn == S.turn && S.turn > 0 &&
                (S.phase == Phase.Main || S.phase == Phase.RoadBuilding || S.phase == Phase.MoveRobber || S.phase == Phase.Steal);
            if (!changed)
            {
                if (byActor && mainClock) Bonus();
                return;
            }
            switch (S.phase)
            {
                case Phase.SetupSettlement:
                case Phase.SetupRoad:
                    StartTimer(c.turnSeconds); break;
                case Phase.Roll:
                    StartTimer(c.rollSeconds); break;
                case Phase.Discard:
                case Phase.SpecialBuild:
                    StartTimer(c.shortPhaseSeconds); break;
                default:
                    if (mainClock) { if (byActor) Bonus(); }
                    else if (S.phase == Phase.Main) { mainTurn = S.turn; StartTimer(c.turnSeconds); }
                    else StartTimer(c.shortPhaseSeconds); // 7 sonrası korsan, zardan önce oynanan kart
                    break;
            }
        }

        /// <summary>Sunucu her kare çağırır. Süre dolduysa sıradaki hamleyi kendisi yapar; durum değiştiyse true.</summary>
        public bool Tick()
        {
            if (deadline < 0 || S.phase == Phase.GameOver || Clock() < deadline) return false;
            bool any = false;
            for (int guard = 0; guard < 24 && deadline >= 0 && S.phase != Phase.GameOver && Clock() >= deadline; guard++)
            {
                if (!Timeout()) { deadline = -1; break; }
                any = true;
            }
            return any;
        }

        int PickRandom(List<int> list) => list[rng.Next(list.Count)];

        bool Timeout()
        {
            int p = Rules.Actor(S);
            if (p < 0) return false;
            switch (S.phase)
            {
                case Phase.SetupSettlement:
                {
                    var spots = Rules.SettleSpots(S, p, true);
                    return spots.Count > 0 && Auto(new GameAction { type = Act.PlaceSettlement, player = p, target = PickRandom(spots) }, N(p) + " için süre doldu, köy rastgele yerleştirildi.");
                }
                case Phase.SetupRoad:
                {
                    var spots = Rules.RoadSpots(S, p);
                    return spots.Count > 0 && Auto(new GameAction { type = Act.PlaceRoad, player = p, target = PickRandom(spots) }, N(p) + " için süre doldu, yol rastgele yerleştirildi.");
                }
                case Phase.Roll:
                    return Auto(new GameAction { type = Act.Roll, player = p }, N(p) + " için süre doldu, zar otomatik atıldı.");
                case Phase.Discard:
                {
                    int q = Array.FindIndex(S.discardPending, x => x > 0);
                    if (q < 0) return false;
                    var hand = new List<int>();
                    for (int r = 0; r < 5; r++) for (int k = 0; k < S.players[q].res[r]; k++) hand.Add(r);
                    Util.Shuffle(hand, rng);
                    var give = new int[5];
                    for (int k = 0; k < S.discardPending[q] && k < hand.Count; k++) give[hand[k]]++;
                    return Auto(new GameAction { type = Act.Discard, player = q, give = give }, N(q) + " için süre doldu, kartlar rastgele atıldı.");
                }
                case Phase.MoveRobber:
                {
                    var spots = Rules.RobberSpots(S);
                    return spots.Count > 0 && Auto(new GameAction { type = Act.MoveRobber, player = p, target = PickRandom(spots) }, N(p) + " için süre doldu, korsan rastgele taşındı.");
                }
                case Phase.Steal:
                {
                    var targets = Rules.StealTargets(S, p, S.robber);
                    return targets.Count > 0 && Auto(new GameAction { type = Act.Steal, player = p, target = PickRandom(targets) }, N(p) + " için süre doldu, rastgele birinden çalındı.");
                }
                case Phase.RoadBuilding:
                case Phase.Main:
                case Phase.SpecialBuild:
                    return Auto(new GameAction { type = Act.EndTurn, player = p }, N(p) + " için süre doldu, tur bitti.");
            }
            return false;
        }

        bool Auto(GameAction a, string msg)
        {
            Log(msg);
            autoAct = true;
            string err;
            try { err = Apply(a); }
            finally { autoAct = false; }
            if (err != null) S.log.RemoveAt(S.log.Count - 1);
            return err == null;
        }

        public void SetConnected(int p, bool c)
        {
            if (p < 0 || p >= S.players.Count) return;
            S.players[p].connected = c;
            Log(N(p) + (c ? " yeniden bağlandı." : " bağlantısı koptu."));
            S.version++;
        }
    }
}
