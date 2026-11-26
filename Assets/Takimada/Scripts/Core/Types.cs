using System;
using System.Collections.Generic;
using System.Text;

namespace Takimada
{
    public enum Terrain { Volkan = 0, Orman = 1, KilOcagi = 2, Otlak = 3, Tarla = 4, Maden = 5 }

    public enum DevCard { Muhafiz = 0, Zafer = 1, YolYapimi = 2, Bereket = 3, Tekel = 4, Gizli = 9 }

    public enum Phase { SetupSettlement, SetupRoad, Roll, Discard, MoveRobber, Steal, Main, RoadBuilding, SpecialBuild, GameOver }

    public enum Act
    {
        PlaceSettlement, PlaceRoad, PlaceCity, Roll, EndTurn, BuyDev, PlayDev,
        Discard, MoveRobber, Steal, BankTrade, Offer, AcceptOffer, RejectOffer, CancelOffer, Chat, ConfirmOffer
    }

    /// <summary>Oyuncunun sunucuya gönderdiği tek bir hamle. Sunucu "player" alanını gönderenin koltuğuna göre yeniden yazar.</summary>
    [Serializable]
    public class GameAction
    {
        public Act type;
        public int player = -1;
        public int target = -1;   // köşe / kenar / altıgen / oyuncu / kaynak, hamleye göre
        public int a = -1;        // ek parametre (kart türü, verilen kaynak, adet)
        public int b = -1;
        public int[] give = new int[5];
        public int[] get = new int[5];
        public string text = "";
    }

    public static class T
    {
        public const int ResCount = 5;
        public const int GenericHarbor = 5;

        public static readonly string[] Res = { "Odun", "Tuğla", "Koyun", "Buğday", "Taş" };
        public static readonly string[] ResShort = { "ODN", "TUĞ", "KYN", "BUĞ", "TAŞ" };
        public static readonly string[] Terrain = { "Volkan", "Orman", "Kil Ocağı", "Otlak", "Tarla", "Dağ" };
        public static readonly string[] Dev = { "Muhafız", "Zafer Puanı", "Yol Yapımı", "Bereket", "Tekel" };
        public static readonly string[] DevDesc =
        {
            "Korsanı istediğin karoya taşı ve oradaki bir oyuncudan rastgele 1 kart çal. Ordu sayına eklenir.",
            "Gizli 1 zafer puanı. Oynanmaz; elinde durduğu sürece sayılır.",
            "Ücretsiz yollar yerleştir.",
            "Bankadan istediğin kaynakları al.",
            "Bir kaynak seç; diğer oyuncuların elindeki o kaynağın tamamı sana gelir."
        };
        public static readonly string[] Phase =
        {
            "Başlangıç: köy yerleştir", "Başlangıç: yol yerleştir", "Zar atılacak", "Kart atılıyor",
            "Korsanı taşı", "Kimden çalınacak?", "Ticaret ve inşa", "Ücretsiz yollar", "Ek inşa aşaması", "Oyun bitti"
        };

        public static int TerrainRes(Takimada.Terrain t) => t == Takimada.Terrain.Volkan ? -1 : (int)t - 1;

        public static string Cards(int[] c)
        {
            if (c == null) return "-";
            var sb = new StringBuilder();
            for (int i = 0; i < ResCount && i < c.Length; i++)
            {
                if (c[i] <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(c[i]).Append(' ').Append(Res[i]);
            }
            return sb.Length == 0 ? "hiçbir şey" : sb.ToString();
        }

        public static string HarborName(int type) => type == GenericHarbor ? "3:1 Liman" : Res[type] + " Limanı";
    }

    public static class Util
    {
        public static int Sum(int[] a) { int s = 0; if (a != null) foreach (var x in a) s += x; return s; }

        public static bool Has(int[] have, int[] need)
        {
            for (int i = 0; i < T.ResCount; i++) if (have[i] < need[i]) return false;
            return true;
        }

        public static void Add(int[] to, int[] what, int sign = 1)
        {
            for (int i = 0; i < T.ResCount; i++) to[i] += what[i] * sign;
        }

        public static int[] Copy(int[] a) => a == null ? new int[T.ResCount] : (int[])a.Clone();

        public static bool Valid5(int[] a)
        {
            if (a == null || a.Length != T.ResCount) return false;
            foreach (var x in a) if (x < 0) return false;
            return true;
        }

        public static void Shuffle<TItem>(IList<TItem> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public static List<int> ParseInts(string s)
        {
            var r = new List<int>();
            if (string.IsNullOrEmpty(s)) return r;
            foreach (var part in s.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part, out int v)) r.Add(v);
            return r;
        }
    }

    /// <summary>
    /// Tohumsuz, gerçek rastgele sayı kaynağı: her sayı işletim sisteminin kriptografik üretecinden
    /// ayrı ayrı çekilir. Bir önceki sayıdan sonrakini tahmin etmek, oyunlar arasında tekrar eden
    /// bir sıra ya da desen yoktur. System.Random yerine her yerde kullanılabilir.
    /// </summary>
    public sealed class TrueRandom : Random
    {
        static readonly System.Security.Cryptography.RandomNumberGenerator Gen =
            System.Security.Cryptography.RandomNumberGenerator.Create();
        readonly byte[] buf = new byte[8];

        /// <summary>[0, max) aralığında eşit olasılıklı tam sayı (modülo yanlılığı yok).</summary>
        uint Below(uint max)
        {
            if (max <= 1) return 0;
            uint limit = uint.MaxValue - uint.MaxValue % max; // bu sınırın üstü reddedilir
            uint x;
            do { Gen.GetBytes(buf, 0, 4); x = BitConverter.ToUInt32(buf, 0); } while (x >= limit);
            return x % max;
        }

        public override int Next() => (int)Below(int.MaxValue);

        public override int Next(int maxValue)
        {
            if (maxValue < 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
            return (int)Below((uint)maxValue);
        }

        public override int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
            return (int)(minValue + (long)Below((uint)((long)maxValue - minValue)));
        }

        public override double NextDouble() => Sample();

        protected override double Sample()
        {
            Gen.GetBytes(buf, 0, 8);
            return (BitConverter.ToUInt64(buf, 0) >> 11) * (1.0 / (1UL << 53)); // 53 bit, [0, 1)
        }

        public override void NextBytes(byte[] buffer) => Gen.GetBytes(buffer);
    }
}
