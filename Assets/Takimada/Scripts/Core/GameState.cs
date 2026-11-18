using System;
using System.Collections.Generic;

namespace Takimada
{
    [Serializable]
    public class DevEntry
    {
        public DevCard type;
        public int boughtTurn;
    }

    [Serializable]
    public class PlayerState
    {
        public string name;
        public int color;
        public int seat;
        public int[] res = new int[5];
        public List<DevEntry> dev = new List<DevEntry>();
        public int knights;
        public int roadsLeft, settlementsLeft, citiesLeft;
        public int roadLength;
        public bool connected = true;
    }

    [Serializable]
    public class TradeOffer
    {
        public int id;
        public int from;
        public int to = -1;                 // -1 = herkese açık
        public int[] give = new int[5];     // teklifi yapanın vereceği
        public int[] get = new int[5];      // teklifi yapanın isteği
        public List<int> rejected = new List<int>();
        public List<int> accepted = new List<int>();   // herkese açık teklifte kabul edenler; teklif sahibi aralarından seçer
        public int counterOf = -1;                     // karşı teklifse yanıtlanan teklifin numarası
    }

    [Serializable]
    public class GameState
    {
        public GameConfig cfg;
        public BoardData board;
        public List<PlayerState> players = new List<PlayerState>();

        public int[] vOwner;   // köşe sahibi, -1 boş
        public int[] vLevel;   // 0 boş, 1 köy, 2 kasaba
        public int[] eOwner;   // yol sahibi
        public int robber = -1;

        public int[] bank = new int[5];
        public List<int> devDeck = new List<int>();
        public int devDeckCount;
        public List<int> diceDeck = new List<int>();

        public Phase phase;
        public int current;
        public int turn;          // her tur başında artar (kart alındığı tur kontrolü için)
        public int round;         // tam tur sayısı
        public int setupIndex;
        public int[] setupOrder = new int[0];
        public int lastSetupVertex = -1;

        public int d1, d2;
        public bool rolled;
        public int devPlays;
        public int freeRoads;
        public int[] discardPending = new int[0];
        public int specialBuilder = -1;

        public int longestHolder = -1, armyHolder = -1;
        public int winner = -1;

        public List<TradeOffer> offers = new List<TradeOffer>();
        public int nextOfferId = 1;
        public List<string> log = new List<string>();
        public int[] lastProduction = new int[0]; // son zarla üreten altıgenler (görsel vurgu)
        public int version;

        // Süre: sunucu her gönderimde doldurur. İstemci, durumu aldığı andan itibaren kendisi geri sayar.
        public float timerLeft = -1;   // -1 = süre yok
        public float timerTotal;

        public GameState Clone() => UnityEngine.JsonUtility.FromJson<GameState>(UnityEngine.JsonUtility.ToJson(this));

        /// <summary>Belirli bir oyuncuya gönderilecek kopya: rakiplerin gelişim kartları ve deste sırası gizlenir.</summary>
        public GameState ForViewer(int viewer)
        {
            var s = Clone();
            s.devDeckCount = devDeck.Count;
            s.devDeck.Clear();
            s.diceDeck.Clear();
            for (int p = 0; p < s.players.Count; p++)
            {
                if (p == viewer || s.phase == Phase.GameOver) continue;
                foreach (var d in s.players[p].dev) { d.type = DevCard.Gizli; d.boughtTurn = 0; }
            }
            return s;
        }
    }
}
