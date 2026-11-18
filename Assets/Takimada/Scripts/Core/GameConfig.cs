using System;
using System.Collections.Generic;

namespace Takimada
{
    /// <summary>
    /// Oyunun bütün kuralları. Lobide sunucu düzenler, herkes aynı kopyayı görür.
    /// Varsayılanlar klasik 3-4 kişilik kurallardır.
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        public string presetName = "Standart (3-4)";

        // --- Genel ---
        public int victoryPoints = 10;
        public bool randomTurnOrder = true;
        public int diceMode = 0;                 // 0 = gerçek zar, 1 = dengeli zar destesi (36 kombinasyon)
        public bool specialBuildPhase = false;   // 5-6 kişilik oyunlardaki ek inşa aşaması

        // --- Süre ---
        public bool turnTimer = true;
        public int rollSeconds = 10;             // zar atmak için süre; dolunca zar kendiliğinden atılır
        public int turnSeconds = 50;             // ticaret/inşa süresi; dolunca tur biter
        public int actionBonusSeconds = 10;      // sırası gelen her hamle yaptığında eklenen süre (turnSeconds'ı aşmaz)
        public int shortPhaseSeconds = 20;       // kart atma, korsan taşıma, çalma, ek inşa

        // --- Harita ---
        public string rowLengths = "3,4,5,4,3";
        public int[] terrainCounts = { 1, 4, 3, 4, 4, 3 }; // Volkan, Orman, Kil Ocağı, Otlak, Tarla, Dağ
        public string numberTokens = "2,3,3,4,4,5,5,6,6,8,8,9,9,10,10,11,11,12";
        public bool shuffleNumbers = true;
        public bool separateRedNumbers = true;   // 6 ve 8 yan yana gelmesin
        public bool harborsEnabled = true;
        public int genericHarbors = 4;
        public int specificHarborsPerResource = 1;
        public int mapSeed = 0;                  // 0 = her oyunda rastgele
        public bool numbersAfterSetup = false;   // sayı pulları başlangıç yerleşimi bitince dağıtılır

        // --- Taşlar ve puanlar ---
        public int roadsPerPlayer = 15;
        public int settlementsPerPlayer = 5;
        public int citiesPerPlayer = 4;
        public int settlementVP = 1;
        public int cityVP = 2;
        public int settlementProduction = 1;
        public int cityProduction = 2;

        // --- Maliyetler (Odun, Tuğla, Koyun, Buğday, Taş) ---
        public int[] roadCost = { 1, 1, 0, 0, 0 };
        public int[] settlementCost = { 1, 1, 1, 1, 0 };
        public int[] cityCost = { 0, 0, 0, 2, 3 };
        public int[] devCost = { 0, 0, 1, 1, 1 };

        // --- Banka ---
        public int bankPerResource = 19;
        public bool unlimitedBank = false;

        // --- Başlangıç ---
        public int setupRounds = 2;
        public bool snakeOrder = true;
        public bool setupResourcesFromLast = true;
        public int[] startingResources = { 0, 0, 0, 0, 0 };

        // --- Korsan ---
        public bool robberEnabled = true;
        public int robberRoll = 7;
        public int discardLimit = 7;             // elinde bundan FAZLA kart olan yarısını atar
        public bool robberStartsOnDesert = true;
        public bool robberBlocksProduction = true;
        public int friendlyRobberVP = 0;         // >0 ise bu puan ve altındakilerden çalınamaz / karoları bloklanamaz
        public int robberGraceRounds = 0;        // ilk N turda korsan gelmez

        // --- Ticaret ---
        public bool playerTrade = true;
        public bool bankTrade = true;
        public int bankRatio = 4;
        public int genericHarborRatio = 3;
        public int specificHarborRatio = 2;
        public bool tradeOnlyWithActive = true;  // her takasta sırası gelen oyuncu taraf olmalı
        public bool allowGifts = false;          // karşılıksız kart verme / isteme
        public int maxOffersPerPlayer = 3;       // bir oyuncunun aynı anda açık teklif sayısı

        // --- Gelişim kartları ---
        public bool devCardsEnabled = true;
        public int[] devDeck = { 14, 5, 2, 2, 2 }; // Muhafız, Zafer, Yol Yapımı, Bereket, Tekel
        public int roadBuildingRoads = 2;
        public int yearOfPlentyCount = 2;
        public int maxDevPlaysPerTurn = 1;
        public bool playDevSameTurn = false;

        // --- Ödüller ---
        public bool longestRoadEnabled = true;
        public int longestRoadMin = 5;
        public int longestRoadVP = 2;
        public bool largestArmyEnabled = true;
        public int largestArmyMin = 3;
        public int largestArmyVP = 2;

        public int maxPlayers = 6;

        public GameConfig Clone() => UnityEngine.JsonUtility.FromJson<GameConfig>(UnityEngine.JsonUtility.ToJson(this));

        // ---------------- Hazır ayarlar ----------------

        public static GameConfig Standard() => new GameConfig();

        public static GameConfig Large()
        {
            var c = new GameConfig
            {
                presetName = "Büyük (5-6)",
                rowLengths = "3,4,5,6,5,4,3",
                terrainCounts = new[] { 2, 6, 5, 6, 6, 5 },
                numberTokens = "2,2,3,3,3,4,4,4,5,5,5,6,6,6,8,8,8,9,9,9,10,10,10,11,11,11,12,12",
                genericHarbors = 6,
                specificHarborsPerResource = 1,
                bankPerResource = 24,
                devDeck = new[] { 20, 5, 3, 3, 3 },
                specialBuildPhase = true
            };
            return c;
        }

        public static GameConfig Quick()
        {
            var c = new GameConfig
            {
                presetName = "Hızlı",
                victoryPoints = 7,
                discardLimit = 9,
                startingResources = new[] { 1, 1, 1, 1, 0 },
                friendlyRobberVP = 2,
                diceMode = 1,
                longestRoadMin = 4,
                largestArmyMin = 2
            };
            return c;
        }

        public static GameConfig Epic()
        {
            var c = new GameConfig
            {
                presetName = "Dev Ada",
                rowLengths = "4,5,6,7,6,5,4",
                terrainCounts = new[] { 2, 8, 7, 8, 7, 5 },
                numberTokens = "",
                genericHarbors = 7,
                specificHarborsPerResource = 1,
                victoryPoints = 13,
                roadsPerPlayer = 20,
                settlementsPerPlayer = 7,
                citiesPerPlayer = 5,
                bankPerResource = 30,
                devDeck = new[] { 22, 6, 3, 3, 3 },
                specialBuildPhase = true
            };
            return c;
        }

        public static readonly string[] PresetNames = { "Standart (3-4)", "Büyük (5-6)", "Hızlı", "Dev Ada" };

        public static GameConfig Preset(int i)
        {
            switch (i)
            {
                case 1: return Large();
                case 2: return Quick();
                case 3: return Epic();
                default: return Standard();
            }
        }

        // ---------------- Doğrulama ----------------

        public int LandCount()
        {
            int n = 0;
            foreach (var r in Util.ParseInts(rowLengths)) if (r > 0) n += r;
            return n;
        }

        public int TerrainTotal() => Util.Sum(terrainCounts);

        public int ProducingCount()
        {
            int land = LandCount();
            int desert = Math.Min(terrainCounts[0], land);
            return Math.Max(0, land - desert);
        }

        /// <summary>Lobide gösterilecek uyarılar. Hiçbiri oyunu engellemez; motor eksikleri kendisi tamamlar.</summary>
        public List<string> Warnings()
        {
            var w = new List<string>();
            var rows = Util.ParseInts(rowLengths);
            if (rows.Count == 0) w.Add("Satır uzunlukları okunamadı; standart harita kullanılacak.");
            for (int i = 1; i < rows.Count; i++)
                if (Math.Abs(rows[i] - rows[i - 1]) > 1)
                    w.Add($"{i}. ve {i + 1}. satır arasındaki fark 1'den büyük; harita boşluklu olabilir.");
            int land = LandCount(), tt = TerrainTotal();
            if (tt != land) w.Add($"Haritada {land} kara karosu var ama arazi toplamı {tt}. Fark rastgele doldurulur/kırpılır.");
            int tokens = Util.ParseInts(numberTokens).Count, need = ProducingCount();
            if (tokens != need) w.Add($"{need} üretim karosu için {tokens} sayı pulu var. Pullar otomatik tamamlanır.");
            if (victoryPoints < 3) w.Add("Zafer puanı çok düşük; oyun ilk turlarda bitebilir.");
            if (robberRoll < 2 || robberRoll > 12) w.Add("Korsan zarı 2-12 dışında; korsan hiç gelmez.");
            if (devCardsEnabled && Util.Sum(devDeck) == 0) w.Add("Gelişim destesi boş.");
            return w;
        }

        public void Sanitize()
        {
            if (terrainCounts == null || terrainCounts.Length != 6) terrainCounts = new[] { 1, 4, 3, 4, 4, 3 };
            if (devDeck == null || devDeck.Length != 5) devDeck = new[] { 14, 5, 2, 2, 2 };
            if (!Util.Valid5(roadCost)) roadCost = new[] { 1, 1, 0, 0, 0 };
            if (!Util.Valid5(settlementCost)) settlementCost = new[] { 1, 1, 1, 1, 0 };
            if (!Util.Valid5(cityCost)) cityCost = new[] { 0, 0, 0, 2, 3 };
            if (!Util.Valid5(devCost)) devCost = new[] { 0, 0, 1, 1, 1 };
            if (!Util.Valid5(startingResources)) startingResources = new int[5];
            victoryPoints = Math.Max(1, victoryPoints);
            setupRounds = Math.Max(1, Math.Min(4, setupRounds));
            bankRatio = Math.Max(1, bankRatio);
            genericHarborRatio = Math.Max(1, genericHarborRatio);
            specificHarborRatio = Math.Max(1, specificHarborRatio);
            maxPlayers = Math.Max(1, Math.Min(8, maxPlayers));
            roadBuildingRoads = Math.Max(0, roadBuildingRoads);
            yearOfPlentyCount = Math.Max(0, yearOfPlentyCount);
            maxDevPlaysPerTurn = Math.Max(0, maxDevPlaysPerTurn);
            maxOffersPerPlayer = Math.Max(1, Math.Min(6, maxOffersPerPlayer));
            rollSeconds = Math.Max(3, Math.Min(120, rollSeconds));
            turnSeconds = Math.Max(10, Math.Min(600, turnSeconds));
            actionBonusSeconds = Math.Max(0, Math.Min(120, actionBonusSeconds));
            shortPhaseSeconds = Math.Max(5, Math.Min(120, shortPhaseSeconds));
        }
    }
}
