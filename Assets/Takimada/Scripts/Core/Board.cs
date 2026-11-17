using System;
using System.Collections.Generic;
using System.Linq;

namespace Takimada
{
    [Serializable]
    public class Hex
    {
        public float x, y;
        public Terrain terrain;
        public int number;
        public int[] verts = new int[6];
        public int[] edges = new int[6];
        public int[] nbrs = new int[0];
    }

    [Serializable]
    public class Vertex
    {
        public float x, y;
        public int[] hexes;
        public int[] verts;
        public int[] edges;
        public int harbor = -1; // -1 yok, 0-4 kaynak limanı, 5 genel liman
    }

    [Serializable]
    public class Edge
    {
        public int a, b;
        public float x, y;
        public int[] hexes;
    }

    [Serializable]
    public class Harbor
    {
        public int edge;
        public int type;
        public float x, y;
    }

    [Serializable]
    public class SeaHex
    {
        public float x, y;
    }

    [Serializable]
    public class BoardData
    {
        public List<Hex> hexes = new List<Hex>();
        public List<SeaHex> sea = new List<SeaHex>();
        public List<Vertex> verts = new List<Vertex>();
        public List<Edge> edges = new List<Edge>();
        public List<Harbor> harbors = new List<Harbor>();
        public bool fog;   // yalnızca önizleme: araziler ve sayılar henüz belli değil
    }

    /// <summary>
    /// Sivri tepeli altıgenlerden harita üretir. Satır uzunlukları her şekli tanımlar
    /// ("3,4,5,4,3" klasik ada, "3,4,5,6,5,4,3" büyük ada). Komşuluk tamamen geometriden çıkarılır.
    /// </summary>
    public static class BoardGen
    {
        public const float W = 1.7320508f; // altıgen genişliği (boyut = 1)
        static readonly int[] DefaultTokens = { 2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12 };

        static long Key(double x, double y) => (long)Math.Round(x * 100) * 1000000L + (long)Math.Round(y * 100);

        public static BoardData Generate(GameConfig c, Random rng)
        {
            var b = new BoardData();
            var rows = Util.ParseInts(c.rowLengths).Where(r => r > 0).ToList();
            if (rows.Count == 0) rows = new List<int> { 3, 4, 5, 4, 3 };

            // --- karo merkezleri ---
            double start = 0;
            for (int r = 0; r < rows.Count; r++)
            {
                if (r > 0)
                {
                    int diff = rows[r] - rows[r - 1];
                    start -= diff * W / 2.0;
                    if (diff % 2 == 0) start += (r % 2 == 0 ? -W / 2.0 : W / 2.0);
                }
                for (int i = 0; i < rows[r]; i++)
                    b.hexes.Add(new Hex { x = (float)(start + i * W), y = -r * 1.5f });
            }
            float minX = b.hexes.Min(h => h.x), maxX = b.hexes.Max(h => h.x);
            float minY = b.hexes.Min(h => h.y), maxY = b.hexes.Max(h => h.y);
            float cx = (minX + maxX) / 2f, cy = (minY + maxY) / 2f;
            foreach (var h in b.hexes) { h.x -= cx; h.y -= cy; }

            // --- köşeler ve kenarlar ---
            var vmap = new Dictionary<long, int>();
            var emap = new Dictionary<long, int>();
            var vHex = new List<List<int>>();
            var eHex = new List<List<int>>();
            for (int hi = 0; hi < b.hexes.Count; hi++)
            {
                var h = b.hexes[hi];
                for (int k = 0; k < 6; k++)
                {
                    double ang = Math.PI / 180.0 * (30 + 60 * k);
                    double vx = h.x + Math.Cos(ang), vy = h.y + Math.Sin(ang);
                    long key = Key(vx, vy);
                    if (!vmap.TryGetValue(key, out int vi))
                    {
                        vi = b.verts.Count;
                        vmap[key] = vi;
                        b.verts.Add(new Vertex { x = (float)vx, y = (float)vy });
                        vHex.Add(new List<int>());
                    }
                    h.verts[k] = vi;
                    vHex[vi].Add(hi);
                }
                for (int k = 0; k < 6; k++)
                {
                    int va = h.verts[k], vb = h.verts[(k + 1) % 6];
                    int lo = Math.Min(va, vb), hiV = Math.Max(va, vb);
                    long key = (long)lo * 100000L + hiV;
                    if (!emap.TryGetValue(key, out int ei))
                    {
                        ei = b.edges.Count;
                        emap[key] = ei;
                        b.edges.Add(new Edge
                        {
                            a = lo, b = hiV,
                            x = (b.verts[lo].x + b.verts[hiV].x) / 2f,
                            y = (b.verts[lo].y + b.verts[hiV].y) / 2f
                        });
                        eHex.Add(new List<int>());
                    }
                    h.edges[k] = ei;
                    eHex[ei].Add(hi);
                }
            }
            var vEdges = new List<int>[b.verts.Count];
            var vVerts = new List<int>[b.verts.Count];
            for (int i = 0; i < b.verts.Count; i++) { vEdges[i] = new List<int>(); vVerts[i] = new List<int>(); }
            for (int ei = 0; ei < b.edges.Count; ei++)
            {
                var e = b.edges[ei];
                e.hexes = eHex[ei].ToArray();
                vEdges[e.a].Add(ei); vEdges[e.b].Add(ei);
                vVerts[e.a].Add(e.b); vVerts[e.b].Add(e.a);
            }
            for (int i = 0; i < b.verts.Count; i++)
            {
                b.verts[i].hexes = vHex[i].ToArray();
                b.verts[i].edges = vEdges[i].ToArray();
                b.verts[i].verts = vVerts[i].ToArray();
            }

            // --- komşu karolar ve deniz halkası ---
            var landKeys = new HashSet<long>(b.hexes.Select(h => Key(h.x, h.y)));
            var seaKeys = new HashSet<long>();
            for (int i = 0; i < b.hexes.Count; i++)
            {
                var n = new List<int>();
                for (int j = 0; j < b.hexes.Count; j++)
                {
                    if (i == j) continue;
                    float dx = b.hexes[i].x - b.hexes[j].x, dy = b.hexes[i].y - b.hexes[j].y;
                    if (dx * dx + dy * dy < W * W * 1.1f) n.Add(j);
                }
                b.hexes[i].nbrs = n.ToArray();
                for (int k = 0; k < 6; k++)
                {
                    double ang = Math.PI / 180.0 * (60 * k);
                    double sx = b.hexes[i].x + W * Math.Cos(ang), sy = b.hexes[i].y + W * Math.Sin(ang);
                    long key = Key(sx, sy);
                    if (landKeys.Contains(key) || seaKeys.Contains(key)) continue;
                    seaKeys.Add(key);
                    b.sea.Add(new SeaHex { x = (float)sx, y = (float)sy });
                }
            }

            AssignTerrain(b, c, rng);
            AssignNumbers(b, c, rng);
            if (c.harborsEnabled) AssignHarbors(b, c, rng);
            return b;
        }

        static void AssignTerrain(BoardData b, GameConfig c, Random rng)
        {
            var list = new List<Terrain>();
            for (int t = 0; t < 6; t++)
                for (int i = 0; i < c.terrainCounts[t]; i++) list.Add((Terrain)t);
            Util.Shuffle(list, rng);
            while (list.Count < b.hexes.Count) list.Add((Terrain)(1 + rng.Next(5)));
            if (list.Count > b.hexes.Count) list.RemoveRange(b.hexes.Count, list.Count - b.hexes.Count);
            Util.Shuffle(list, rng);
            for (int i = 0; i < b.hexes.Count; i++) b.hexes[i].terrain = list[i];
        }

        /// <summary>Üretim karolarının sayılarını gizler (-1 = "?"). Pullar sonra AssignNumbers ile dağıtılır.</summary>
        public static void HideNumbers(BoardData b)
        {
            foreach (var h in b.hexes) h.number = h.terrain == Terrain.Volkan ? 0 : -1;
        }

        public static void AssignNumbers(BoardData b, GameConfig c, Random rng)
        {
            var producers = Enumerable.Range(0, b.hexes.Count).Where(i => b.hexes[i].terrain != Terrain.Volkan).ToList();
            var tokens = Util.ParseInts(c.numberTokens).Where(t => t >= 2 && t <= 12).ToList();
            if (tokens.Count == 0) tokens = DefaultTokens.ToList();
            var baseTokens = tokens.ToList();
            int cycle = 0;
            while (tokens.Count < producers.Count)
            {
                if (cycle % baseTokens.Count == 0) Util.Shuffle(baseTokens, rng);
                tokens.Add(baseTokens[cycle % baseTokens.Count]);
                cycle++;
            }
            if (c.shuffleNumbers) Util.Shuffle(tokens, rng);
            if (tokens.Count > producers.Count) tokens.RemoveRange(producers.Count, tokens.Count - producers.Count);

            foreach (var h in b.hexes) h.number = 0;
            if (c.shuffleNumbers && c.separateRedNumbers)
            {
                int bestBad = int.MaxValue;
                List<int> best = tokens.ToList();
                for (int attempt = 0; attempt < 2000 && bestBad > 0; attempt++)
                {
                    Util.Shuffle(tokens, rng);
                    for (int i = 0; i < producers.Count; i++) b.hexes[producers[i]].number = tokens[i];
                    int bad = CountRedPairs(b);
                    if (bad < bestBad) { bestBad = bad; best = tokens.ToList(); }
                }
                tokens = best;
            }
            for (int i = 0; i < producers.Count; i++) b.hexes[producers[i]].number = tokens[i];
        }

        static int CountRedPairs(BoardData b)
        {
            int bad = 0;
            for (int i = 0; i < b.hexes.Count; i++)
            {
                int n = b.hexes[i].number;
                if (n != 6 && n != 8) continue;
                foreach (var j in b.hexes[i].nbrs)
                    if (j > i && (b.hexes[j].number == 6 || b.hexes[j].number == 8)) bad++;
            }
            return bad;
        }

        static void AssignHarbors(BoardData b, GameConfig c, Random rng)
        {
            var types = new List<int>();
            for (int i = 0; i < c.genericHarbors; i++) types.Add(T.GenericHarbor);
            for (int r = 0; r < T.ResCount; r++)
                for (int i = 0; i < c.specificHarborsPerResource; i++) types.Add(r);
            if (types.Count == 0) return;
            Util.Shuffle(types, rng);

            var coast = Enumerable.Range(0, b.edges.Count).Where(e => b.edges[e].hexes.Length == 1)
                .OrderBy(e => Math.Atan2(b.edges[e].y, b.edges[e].x)).ToList();
            int n = coast.Count;
            int count = Math.Min(types.Count, n / 2);
            if (count == 0) return;

            var usedVerts = new HashSet<int>();
            double step = n / (double)count;
            int offset = rng.Next(n);
            for (int i = 0; i < count; i++)
            {
                int idx = (offset + (int)Math.Round(i * step)) % n;
                int tries = 0;
                while (tries < n)
                {
                    var e = b.edges[coast[idx]];
                    if (!usedVerts.Contains(e.a) && !usedVerts.Contains(e.b)) break;
                    idx = (idx + 1) % n;
                    tries++;
                }
                if (tries >= n) break;
                int ei = coast[idx];
                var edge = b.edges[ei];
                usedVerts.Add(edge.a); usedVerts.Add(edge.b);
                foreach (var nv in b.verts[edge.a].verts) usedVerts.Add(nv);
                foreach (var nv in b.verts[edge.b].verts) usedVerts.Add(nv);
                var land = b.hexes[edge.hexes[0]];
                float dx = edge.x - land.x, dy = edge.y - land.y;
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                var hb = new Harbor { edge = ei, type = types[i], x = edge.x + dx / len * 0.62f, y = edge.y + dy / len * 0.62f };
                b.harbors.Add(hb);
                b.verts[edge.a].harbor = hb.type;
                b.verts[edge.b].harbor = hb.type;
            }
        }
    }
}
