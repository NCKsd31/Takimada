using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Takimada
{
    public partial class Hud
    {
        static readonly string[] RuleTabs = { "Genel", "Harita", "Taş & Maliyet", "Korsan", "Ticaret & Kart", "Ödüller" };
        int rulesTab;
        Vector2 rulesScroll;
        string rulesNote = "";
        const float LabelW = 290;

        /// <summary>Kural düzenleyici. editable=false iken salt okunur gösterilir (istemciler ve oyun içi).</summary>
        void DrawRulesEditor(Rect r, bool editable)
        {
            Panel(r);
            var c = app.lobby.cfg;
            BeginArea(new Rect(r.x + 16, r.y + 14, r.width - 32, r.height - 28));
            GUILayout.BeginHorizontal();
            LLabel("Kurallar", Sty.h1);
            GUILayout.FlexibleSpace();
            LLabel(editable ? "Kurucu düzenliyor · " + c.presetName : "Salt okunur · " + c.presetName, Sty.dim);
            GUILayout.EndHorizontal();

            if (editable)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < GameConfig.PresetNames.Length; i++)
                {
                    if (LBtn(GameConfig.PresetNames[i], true, Sty.btnSmall, GUILayout.Height(28)))
                    {
                        app.SetConfig(GameConfig.Preset(i));
                        rulesNote = GameConfig.PresetNames[i] + " kuralları yüklendi.";
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (LBtn("Kaydet", true, Sty.btnSmall, GUILayout.Height(26))) rulesNote = app.SaveConfig();
                if (LBtn("Kayıtlıyı yükle", true, Sty.btnSmall, GUILayout.Height(26))) rulesNote = app.LoadConfig();
                if (LBtn("Panodan kopyala (JSON)", true, Sty.btnSmall, GUILayout.Height(26)))
                {
                    GUIUtility.systemCopyBuffer = JsonUtility.ToJson(c, true);
                    rulesNote = "Kurallar JSON olarak panoya kopyalandı.";
                }
                if (LBtn("Panodan yapıştır", true, Sty.btnSmall, GUILayout.Height(26)))
                {
                    try
                    {
                        var nc = JsonUtility.FromJson<GameConfig>(GUIUtility.systemCopyBuffer);
                        nc.Sanitize();
                        app.SetConfig(nc);
                        rulesNote = "Panodaki kurallar yüklendi.";
                    }
                    catch { rulesNote = "Panoda geçerli kural JSON'u yok."; }
                }
                GUILayout.EndHorizontal();
                if (!string.IsNullOrEmpty(rulesNote)) LLabel(rulesNote, Sty.dim);
            }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < RuleTabs.Length; i++)
                if (LButton(RuleTabs[i], i == rulesTab ? Sty.tabOn : Sty.tab, GUILayout.Height(30))) rulesTab = i;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            rulesScroll = BeginLayoutScroll(1, rulesScroll);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = editable;
            GUI.changed = false;
            switch (rulesTab)
            {
                case 0: TabGeneral(c); break;
                case 1: TabMap(c); break;
                case 2: TabCosts(c); break;
                case 3: TabRobber(c); break;
                case 4: TabTrade(c); break;
                case 5: TabAwards(c); break;
            }
            if (GUI.changed && editable)
            {
                c.presetName = "Özel";
                app.MarkLobbyDirty();
            }
            GUI.enabled = oldEnabled;
            EndLayoutScroll(1);

            var warn = c.Warnings();
            if (warn.Count > 0)
            {
                GUILayout.Space(4);
                foreach (var w in warn) LLabel("• " + w, Sty.Colored(Sty.small, Sty.Brass));
            }
            EndArea();
        }

        // ---------------- satır bileşenleri ----------------

        void Section(string title, string hint = null)
        {
            GUILayout.Space(10);
            LLabel(title, Sty.h2);
            if (!string.IsNullOrEmpty(hint)) LLabel(hint, Sty.dim);
        }

        void IntRow(string label, ref int v, int min, int max, int step = 1, string unit = "")
        {
            GUILayout.BeginHorizontal(GUILayout.Height(28));
            LLabel(label, Sty.label, GUILayout.Width(LabelW));
            if (LButton("−", Sty.btnSmall, GUILayout.Width(28), GUILayout.Height(24))) { v = Math.Max(min, v - step); GUI.changed = true; }
            LLabel(v + unit, Sty.center, GUILayout.Width(64));
            if (LButton("+", Sty.btnSmall, GUILayout.Width(28), GUILayout.Height(24))) { v = Math.Min(max, v + step); GUI.changed = true; }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void BoolRow(string label, ref bool v, string hint = null)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(28));
            LLabel(label, Sty.label, GUILayout.Width(LabelW));
            if (LButton(v ? "Açık" : "Kapalı", v ? Sty.btnOn : Sty.btnSmall, GUILayout.Width(126), GUILayout.Height(24))) { v = !v; GUI.changed = true; }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(hint)) LLabel(hint, Sty.dim);
        }

        void ChoiceRow(string label, ref int v, string[] options)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(28));
            LLabel(label, Sty.label, GUILayout.Width(LabelW));
            for (int i = 0; i < options.Length; i++)
                if (LButton(options[i], i == v ? Sty.btnOn : Sty.btnSmall, GUILayout.Height(24))) { v = i; GUI.changed = true; }
            GUILayout.EndHorizontal();
        }

        void ResRow(string label, int[] arr, int max = 9)
        {
            LLabel(label, Sty.label);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < T.ResCount; i++)
            {
                GUILayout.BeginVertical(GUILayout.Width(96));
                LLabel(T.Res[i], Sty.Colored(Sty.small, Pal.Res[i]));
                GUILayout.BeginHorizontal();
                if (LButton("−", Sty.btnSmall, GUILayout.Width(24), GUILayout.Height(22))) { arr[i] = Math.Max(0, arr[i] - 1); GUI.changed = true; }
                LLabel(arr[i].ToString(), Sty.center, GUILayout.Width(28));
                if (LButton("+", Sty.btnSmall, GUILayout.Width(24), GUILayout.Height(22))) { arr[i] = Math.Min(max, arr[i] + 1); GUI.changed = true; }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
        }

        string TextRow(string label, string v)
        {
            LLabel(label, Sty.label);
            return LField(v ?? "", 400, Sty.field, GUILayout.Height(32));
        }

        // ---------------- sekmeler ----------------

        void TabGeneral(GameConfig c)
        {
            Section("Kazanma");
            IntRow("Kazanmak için zafer puanı", ref c.victoryPoints, 3, 30);
            IntRow("En fazla oyuncu", ref c.maxPlayers, 2, 8);
            BoolRow("Oyuncu sırası rastgele", ref c.randomTurnOrder);

            Section("Süre", "Zar süresi dolunca zar kendiliğinden atılır, tur süresi dolunca tur biter. Sırası gelen oyuncu her hamle yaptığında süresi uzar (tur süresini aşmaz).");
            BoolRow("Tur süresi", ref c.turnTimer);
            if (c.turnTimer)
            {
                IntRow("Zar atma süresi", ref c.rollSeconds, 3, 120, 1, " sn");
                IntRow("Ticaret ve inşa süresi", ref c.turnSeconds, 10, 600, 5, " sn");
                IntRow("Hamle başına ek süre", ref c.actionBonusSeconds, 0, 120, 1, " sn");
                IntRow("Kart atma / korsan / ek inşa", ref c.shortPhaseSeconds, 5, 120, 5, " sn");
            }

            Section("Zar");
            ChoiceRow("Zar türü", ref c.diceMode, new[] { "Gerçek zar", "Dengeli deste" });
            LLabel("Dengeli deste: 36 zar kombinasyonu karıştırılır, 5 kart kalınca yeniden karılır. Şans daha adil dağılır.", Sty.dim);

            Section("Başlangıç dizilimi");
            IntRow("Başlangıç köyü sayısı (tur)", ref c.setupRounds, 1, 4);
            BoolRow("Yılan sırası (1→N, N→1)", ref c.snakeOrder);
            BoolRow("Son köy başlangıç kaynağı verir", ref c.setupResourcesFromLast);
            ResRow("Herkese verilen başlangıç kaynakları", c.startingResources);

            Section("Ek inşa aşaması", "Bir oyuncu turunu bitirince diğerleri sırayla (ticaret yapmadan) inşa edebilir. 5-6 kişide sıkılmayı önler.");
            BoolRow("Ek inşa aşaması", ref c.specialBuildPhase);
        }

        static readonly (string name, string rows)[] Shapes =
        {
            ("Klasik 19", "3,4,5,4,3"), ("Büyük 30", "3,4,5,6,5,4,3"), ("Dev 37", "4,5,6,7,6,5,4"), ("Uzun 26", "2,3,4,5,4,3,2,3"), ("Mini 12", "3,4,5")
        };

        void TabMap(GameConfig c)
        {
            Section("Ada şekli", "Satır uzunlukları yukarıdan aşağıya. Komşu satırlar 1 farklı olmalı.");
            GUILayout.BeginHorizontal();
            foreach (var s in Shapes)
                if (LButton(s.name, Sty.btnSmall, GUILayout.Height(26)))
                {
                    c.rowLengths = s.rows;
                    AutoBalanceTerrain(c);
                    c.numberTokens = AutoTokens(c.ProducingCount());
                    GUI.changed = true;
                }
            GUILayout.EndHorizontal();
            c.rowLengths = TextRow("Satır uzunlukları", c.rowLengths);
            LLabel("Kara karosu: " + c.LandCount() + " · Arazi toplamı: " + c.TerrainTotal() + " · Üretim karosu: " + c.ProducingCount(), Sty.dim);

            Section("Araziler");
            for (int t = 0; t < 6; t++)
            {
                string res = t == 0 ? "üretmez, korsan başlar" : T.Res[t - 1];
                IntRow(T.Terrain[t] + "  (" + res + ")", ref c.terrainCounts[t], 0, 30);
            }
            if (LButton("Arazileri harita boyutuna dengele", Sty.btnSmall, GUILayout.Height(26))) { AutoBalanceTerrain(c); GUI.changed = true; }

            Section("Sayı pulları", "2-12 arası sayılar. Eksikse tekrar edilerek tamamlanır.");
            c.numberTokens = TextRow("Pullar", c.numberTokens);
            GUILayout.BeginHorizontal();
            LLabel("Pul sayısı: " + Util.ParseInts(c.numberTokens).Count + " / gereken " + c.ProducingCount(), Sty.dim);
            if (LButton("Otomatik doldur", Sty.btnSmall, GUILayout.Width(140), GUILayout.Height(24))) { c.numberTokens = AutoTokens(c.ProducingCount()); GUI.changed = true; }
            GUILayout.EndHorizontal();
            BoolRow("Pulları karıştır", ref c.shuffleNumbers);
            BoolRow("6 ve 8 yan yana gelmesin", ref c.separateRedNumbers);
            BoolRow("Sayıları başlangıç yerleşiminden sonra dağıt", ref c.numbersAfterSetup, "Açıkken herkes ilk köylerini sayıları görmeden, yalnızca arazilere bakarak kurar. Pullar ilk zar turundan önce rastgele dağıtılır.");

            Section("Limanlar");
            BoolRow("Limanlar", ref c.harborsEnabled);
            IntRow("Genel liman (3:1) sayısı", ref c.genericHarbors, 0, 12);
            IntRow("Her kaynak için özel liman", ref c.specificHarborsPerResource, 0, 3);

            Section("Harita tohumu", "0 = her oyunda yeni harita: araziler, sayılar ve limanlar oyun başlarken rastgele dağıtılır. Aynı sayı her seferinde aynı haritayı üretir.");
            IntRow("Tohum", ref c.mapSeed, 0, 99999);
            GUILayout.BeginHorizontal();
            if (LButton("Rastgele tohum", Sty.btnSmall, GUILayout.Height(26))) { c.mapSeed = UnityEngine.Random.Range(1, 99999); GUI.changed = true; }
            bool old = GUI.enabled; GUI.enabled = true;
            if (LButton("Önizlemeyi yenile", Sty.btnSmall, GUILayout.Height(26))) previewSeed = UnityEngine.Random.Range(1, 999999);
            GUI.enabled = old;
            GUILayout.EndHorizontal();
        }

        void TabCosts(GameConfig c)
        {
            Section("Oyuncu başına taş");
            IntRow("Yol", ref c.roadsPerPlayer, 1, 40);
            IntRow("Köy", ref c.settlementsPerPlayer, 1, 15);
            IntRow("Kasaba", ref c.citiesPerPlayer, 0, 15);

            Section("Puan ve üretim");
            IntRow("Köy zafer puanı", ref c.settlementVP, 0, 5);
            IntRow("Kasaba zafer puanı", ref c.cityVP, 0, 6);
            IntRow("Köy üretimi (kart)", ref c.settlementProduction, 0, 5);
            IntRow("Kasaba üretimi (kart)", ref c.cityProduction, 0, 6);

            Section("Maliyetler");
            ResRow("Yol", c.roadCost);
            ResRow("Köy", c.settlementCost);
            ResRow("Kasaba (köyün üzerine)", c.cityCost);
            ResRow("Gelişim kartı", c.devCost);

            Section("Banka");
            IntRow("Kaynak başına kart", ref c.bankPerResource, 5, 60);
            BoolRow("Sınırsız banka", ref c.unlimitedBank, "Kapalıyken banka bir kaynağı herkese yetmiyorsa o kaynaktan kimse alamaz (tek kişiyse kalanı alır).");
        }

        void TabRobber(GameConfig c)
        {
            Section("Korsan");
            BoolRow("Korsan", ref c.robberEnabled);
            IntRow("Korsanı getiren zar", ref c.robberRoll, 2, 12);
            IntRow("Kart atma sınırı", ref c.discardLimit, 0, 30);
            LLabel("Korsan zarı gelince elinde bu sayıdan fazla kart olan, yarısını (aşağı yuvarlanır) atar.", Sty.dim);
            BoolRow("Korsan volkanda başlar", ref c.robberStartsOnDesert);
            BoolRow("Korsanlı karo üretmez", ref c.robberBlocksProduction);
            IntRow("Dostane korsan: koruma puanı", ref c.friendlyRobberVP, 0, 10);
            LLabel("0 kapalı. Örn. 2: puanı 2 veya altındaki oyunculardan çalınamaz, karolarına korsan konamaz.", Sty.dim);
            IntRow("Korsansız ilk tur sayısı", ref c.robberGraceRounds, 0, 10);
        }

        void TabTrade(GameConfig c)
        {
            Section("Ticaret");
            BoolRow("Oyuncular arası ticaret", ref c.playerTrade, "Teklif herkese veya tek kişiye yapılır. Tek kişiye yapılan teklif kabul edilince takas hemen olur; herkese açık teklifte kabul edenler arasından teklif sahibi seçer. Karşı teklif verilebilir.");
            BoolRow("Takasta sırası gelen oyuncu taraf olmalı", ref c.tradeOnlyWithActive, "Kapalıyken sırası gelen oyuncunun ticaret aşamasında herkes birbiriyle takas yapabilir.");
            BoolRow("Hediye ve karşılıksız istek", ref c.allowGifts, "Açıkken bir taraf hiç kart koymadan teklif yapılabilir.");
            IntRow("Oyuncu başına açık teklif", ref c.maxOffersPerPlayer, 1, 6);
            BoolRow("Banka ticareti", ref c.bankTrade);
            IntRow("Banka oranı", ref c.bankRatio, 1, 8, 1, ":1");
            IntRow("Genel liman oranı", ref c.genericHarborRatio, 1, 8, 1, ":1");
            IntRow("Özel liman oranı", ref c.specificHarborRatio, 1, 8, 1, ":1");

            Section("Gelişim kartları");
            BoolRow("Gelişim kartları", ref c.devCardsEnabled);
            for (int i = 0; i < 5; i++) IntRow("Destede " + T.Dev[i], ref c.devDeck[i], 0, 40);
            IntRow("Yol Yapımı: ücretsiz yol", ref c.roadBuildingRoads, 0, 6);
            IntRow("Bereket: alınan kaynak", ref c.yearOfPlentyCount, 0, 6);
            IntRow("Tur başına oynanabilir kart", ref c.maxDevPlaysPerTurn, 0, 5);
            BoolRow("Alındığı tur oynanabilir", ref c.playDevSameTurn);
        }

        void TabAwards(GameConfig c)
        {
            Section("En Uzun Yol", "Kesintisiz yol uzunluğu. Rakip köyü/kasabası yolu böler. Beraberlikte mevcut sahibi korur.");
            BoolRow("En Uzun Yol", ref c.longestRoadEnabled);
            IntRow("En az uzunluk", ref c.longestRoadMin, 1, 15);
            IntRow("Zafer puanı", ref c.longestRoadVP, 0, 5);

            Section("En Güçlü Ordu", "Oynanan muhafız kartı sayısı. Geçmek için sahibinden fazla oynamak gerekir.");
            BoolRow("En Güçlü Ordu", ref c.largestArmyEnabled);
            IntRow("En az muhafız", ref c.largestArmyMin, 1, 15);
            IntRow("Zafer puanı", ref c.largestArmyVP, 0, 5);
        }

        // ---------------- yardımcılar ----------------

        static readonly int[] BaseTokens = { 2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12 };

        static string AutoTokens(int n)
        {
            var list = new List<int>();
            for (int i = 0; i < n; i++) list.Add(BaseTokens[i % BaseTokens.Length]);
            list.Sort();
            return string.Join(",", list);
        }

        static void AutoBalanceTerrain(GameConfig c)
        {
            int land = c.LandCount();
            // klasik oranlar: volkan 1/19, orman/otlak/tarla 4/19, kil/maden 3/19
            double[] w = { 1, 4, 3, 4, 4, 3 };
            double sum = w.Sum();
            var counts = new int[6];
            int total = 0;
            for (int t = 0; t < 6; t++) { counts[t] = (int)Math.Floor(land * w[t] / sum); total += counts[t]; }
            if (counts[0] == 0 && land >= 7) { counts[0] = 1; total++; }
            int k = 1;
            while (total < land) { counts[k]++; total++; k = k % 5 + 1; }
            while (total > land) { int m = Array.IndexOf(counts, counts.Max()); counts[m]--; total--; }
            c.terrainCounts = counts;
        }
    }
}
