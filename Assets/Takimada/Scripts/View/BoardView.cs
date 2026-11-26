using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Takimada
{
    public static class Pal
    {
        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public static readonly Color Haze = Hex("#5E93A6");
        public static readonly Color Ink = Hex("#1B1712");
        public static readonly Color Token = Hex("#F2E6CC");
        public static readonly Color TokenEdge = Hex("#6B5A3E");
        public static readonly Color Wood = Hex("#7A5532");
        public static readonly Color Fog = Hex("#A79C7E");
        public static readonly Color FogEdge = Hex("#756C55");

        // Volkan, Orman, Kil Ocağı, Otlak, Tarla, Dağ
        public static readonly Color[] TerrainFill =
        {
            Hex("#4A3B36"), Hex("#3E7A3C"), Hex("#B86A45"), Hex("#8FC25A"), Hex("#C9AE4C"), Hex("#8B9096")
        };
        public static readonly Color[] TerrainEdge =
        {
            Hex("#2E2420"), Hex("#2B5A2B"), Hex("#8A4A2C"), Hex("#6B9940"), Hex("#9C8635"), Hex("#676C72")
        };
        // Odun, Tuğla, Koyun, Buğday, Taş
        public static readonly Color[] Res =
        {
            Hex("#2F6B34"), Hex("#E2553A"), Hex("#B8E58C"), Hex("#F5C531"), Hex("#8EA6C2")
        };

        /// <summary>Kaynak rengi koyu mu (odun: koyu yeşil)? Koyu zeminde simge ve yazı açık renk çizilir.</summary>
        public static bool DarkRes(int r)
        {
            var c = Res[Mathf.Clamp(r, 0, Res.Length - 1)];
            return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b < 0.42f;
        }

        /// <summary>Kaynak renkli zeminin üstündeki simge/yazı rengi.</summary>
        public static Color ResInk(int r) => DarkRes(r) ? Token : Ink;

        /// <summary>Koyu panel üstünde kaynak adını yazmak için okunur renk.</summary>
        public static Color ResText(int r) => DarkRes(r) ? Color.Lerp(Res[r], Color.white, 0.45f) : Res[r];

        public static readonly string[] PlayerNames = { "Kırmızı", "Mavi", "Turuncu", "Beyaz", "Mor", "Pembe", "Turkuaz", "Kahve" };
        public static readonly Color[] Player =
        {
            Hex("#E27D7D"), Hex("#7EA4E0"), Hex("#EFA968"), Hex("#F5F2EA"),
            Hex("#AE90DB"), Hex("#EC9CC0"), Hex("#6FC6BA"), Hex("#A9825F")
        };
        public static Color PlayerColor(int i) => Player[((i % Player.Length) + Player.Length) % Player.Length];

        // haritadaki taşların rengi: aynı tonlar ama doygun ve parlak, ki ada renklerinden ayrılsın
        public static readonly Color[] Piece =
        {
            Hex("#F0262B"), Hex("#2366FF"), Hex("#FF8A00"), Hex("#FFFFFF"),
            Hex("#9B30FF"), Hex("#FF3DA8"), Hex("#00E0D0"), Hex("#7A4518")
        };
        public static Color PieceColor(int i) => Piece[((i % Piece.Length) + Piece.Length) % Piece.Length];

        /// <summary>Pastel ton: doygunluğu azaltır, rengi beyaza doğru açar. Alfa (ışıma) korunur.</summary>
        public static Color Soft(Color c, float sat = 0.6f, float lift = 0.18f)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            var o = Color.HSVToRGB(h, s * sat, Mathf.Lerp(v, 1f, lift));
            o.a = c.a;
            return o;
        }
    }

    /// <summary>Düz gölgeli (low-poly) 3B mesh oluşturucu. Her üçgen kendi normaline sahiptir.</summary>
    public class Mesh3
    {
        readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        readonly List<Color32> c = new List<Color32>();
        readonly List<int> t = new List<int>();
        static readonly Vector3 Up = Vector3.up;

        // 1 ve 0 dışındaysa her üçgenin rengi Pal.Soft ile pastelleştirilir
        public float Sat = 1f, Lift = 0f;

        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Vector4> an = new List<Vector4>();

        /// <summary>
        /// Bundan sonra eklenen üçgenlerin shader'da nasıl oynayacağı (TEXCOORD1):
        /// x,y = dönme/sallanma merkezi (dünya x,z), z = faz (0-1), w = tür (0 sabit, 1 koyun gövdesi, 2 koyun başı, 3 gemi,
        /// 4 yunus, 5 balina, 6 balina fıskiyesi, 7 duman). 4 ve üstünde z'nin tam kısmı ek bilgi taşır (bkz. Packed).
        /// </summary>
        public Vector4 Anim;

        public const float AnimSheep = 1, AnimSheepHead = 2, AnimShip = 3, AnimDolphin = 4, AnimWhale = 5, AnimSpout = 6, AnimSmoke = 7;

        /// <summary>z bileşeni: tam kısım = param (yön derecesi ya da yükseklik×1000), kesirli kısım = faz.</summary>
        public static float Packed(float param, float phase) => Mathf.Floor(Mathf.Max(0f, param)) + Mathf.Repeat(phase, 1f) * 0.999f;

        public void Clear() { v.Clear(); n.Clear(); c.Clear(); t.Clear(); uv.Clear(); an.Clear(); Anim = Vector4.zero; }

        public void Tri(Vector3 a, Vector3 b, Vector3 d, Color col) => TriUV(a, b, d, Vector2.zero, Vector2.zero, Vector2.zero, col);

        public void TriUV(Vector3 a, Vector3 b, Vector3 d, Vector2 ua, Vector2 ub, Vector2 ud, Color col)
        {
            var nn = Vector3.Cross(b - a, d - a);
            if (nn.sqrMagnitude < 1e-14f) return;
            nn.Normalize();
            if (Sat < 1f || Lift > 0f) col = Pal.Soft(col, Sat, Lift);
            int s = v.Count;
            v.Add(a); v.Add(b); v.Add(d);
            n.Add(nn); n.Add(nn); n.Add(nn);
            uv.Add(ua); uv.Add(ub); uv.Add(ud);
            an.Add(Anim); an.Add(Anim); an.Add(Anim);
            Color32 cc = col;
            c.Add(cc); c.Add(cc); c.Add(cc);
            t.Add(s); t.Add(s + 1); t.Add(s + 2);
        }

        /// <summary>Yatay düz daire (pul üstü noktalar için).</summary>
        public void Disc(Vector3 center, float r, int sides, Color col)
        {
            for (int i = 0; i < sides; i++)
                Tri(center, Ring(center, r, 360f / sides * (i + 1)), Ring(center, r, 360f / sides * i), col);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color col) { Tri(a, b, cc, col); Tri(a, cc, d, col); }

        public static Vector3 Ring(Vector3 center, float r, float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
        }

        /// <summary>Kesik koni: r1 = r0 ise prizma, r1 = 0 ise koni. b taban merkezidir.</summary>
        public void Frustum(Vector3 b, float r0, float r1, float h, int sides, float rotDeg, Color top, Color side, bool cap = true)
        {
            var tc = b + Up * h;
            for (int i = 0; i < sides; i++)
            {
                float a0 = rotDeg + 360f / sides * i, a1 = rotDeg + 360f / sides * (i + 1);
                var p0 = Ring(b, r0, a0); var p1 = Ring(b, r0, a1);
                if (r1 > 0.0001f)
                {
                    var q0 = Ring(tc, r1, a0); var q1 = Ring(tc, r1, a1);
                    Quad(p0, q0, q1, p1, side);
                    if (cap) Tri(tc, q1, q0, top);
                }
                else Tri(p0, tc, p1, side);
            }
        }

        public void Prism(Vector3 b, float r, float h, int sides, float rotDeg, Color top, Color side) => Frustum(b, r, r, h, sides, rotDeg, top, side);

        /// <summary>Taban merkezli kutu; size = (genişlik, yükseklik, derinlik), yaw derece.</summary>
        public void Box(Vector3 b, Vector3 size, float yaw, Color col, Color? topCol = null)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            float x = size.x / 2, z = size.z / 2, y = size.y;
            Vector3 P(float px, float py, float pz) => b + q * new Vector3(px, py, pz);
            Quad(P(-x, y, -z), P(-x, y, z), P(x, y, z), P(x, y, -z), topCol ?? col);
            Quad(P(-x, 0, -z), P(-x, y, -z), P(x, y, -z), P(x, 0, -z), col);
            Quad(P(x, 0, z), P(x, y, z), P(-x, y, z), P(-x, 0, z), col);
            Quad(P(-x, 0, z), P(-x, y, z), P(-x, y, -z), P(-x, 0, -z), col);
            Quad(P(x, 0, -z), P(x, y, -z), P(x, y, z), P(x, 0, z), col);
            Quad(P(-x, 0, -z), P(x, 0, -z), P(x, 0, z), P(-x, 0, z), col);
        }

        /// <summary>Beşik çatı: b saçak yüksekliğindeki merkez, mahya yerel z ekseni boyunca. Yüzler dışa bakar.</summary>
        public void Roof(Vector3 b, float w, float d, float h, float yaw, Color roof, Color gable, float overhang)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            float x = w / 2 + overhang, z = d / 2 + overhang, gx = w / 2, gz = d / 2;
            Vector3 P(float px, float py, float pz) => b + q * new Vector3(px, py, pz);
            var r0 = P(0, h, -z); var r1 = P(0, h, z);
            Quad(P(-x, -overhang * 0.6f, z), r1, r0, P(-x, -overhang * 0.6f, -z), roof);
            Quad(r0, r1, P(x, -overhang * 0.6f, z), P(x, -overhang * 0.6f, -z), roof);
            Tri(P(-gx, 0, -gz), P(0, h, -gz), P(gx, 0, -gz), gable);
            Tri(P(-gx, 0, gz), P(gx, 0, gz), P(0, h, gz), gable);
        }

        /// <summary>
        /// Roof'un dış hat kabuğu: çatıyı t kadar dışarıdan saran kapalı üçgen prizma (altı da kapalı).
        /// Ön yüzleri kırpılarak (Cull Front) çizilince çatının çevresinde ince koyu bir çizgi kalır.
        /// </summary>
        public void RoofShell(Vector3 b, float w, float d, float h, float yaw, float overhang, float t, Color col)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float px, float py, float pz) => b + q * new Vector3(px, py, pz);
            // eğik yüzü normali boyunca t kadar kaydır; mahya ve saçak (t kadar aşağıda) bu doğru üstünde
            float xe = w / 2 + overhang, ye = -overhang * 0.6f, rise = h - ye;
            float len = Mathf.Sqrt(xe * xe + rise * rise);
            float ry = h + t * len / xe;
            float ey = ye - t;
            float X = xe + t * (len + xe) / rise;
            float Z = d / 2 + overhang + t;
            var L0 = P(-X, ey, -Z); var L1 = P(-X, ey, Z); var R0 = P(X, ey, -Z); var R1 = P(X, ey, Z);
            var T0 = P(0, ry, -Z); var T1 = P(0, ry, Z);
            Quad(L1, T1, T0, L0, col);
            Quad(T0, T1, R1, R0, col);
            Tri(L0, T0, R0, col);
            Tri(L1, R1, T1, col);
            Quad(L0, R0, R1, L1, col);
        }

        /// <summary>Altı kapalı koni (dış hat kabuğu için): b taban merkezi.</summary>
        public void ClosedCone(Vector3 b, float r, float h, int sides, float rotDeg, Color col)
        {
            Frustum(b, r, 0f, h, sides, rotDeg, col, col);
            for (int i = 0; i < sides; i++)
                Tri(b, Ring(b, r, rotDeg + 360f / sides * i), Ring(b, r, rotDeg + 360f / sides * (i + 1)), col);
        }

        public void Cylinder(Vector3 a, Vector3 b, float r, int sides, Color col)
        {
            var dir = (b - a).normalized;
            var u = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var w = Vector3.Cross(dir, u);
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.PI * 2 * i / sides, a1 = Mathf.PI * 2 * (i + 1) / sides;
                var o0 = (u * Mathf.Cos(a0) + w * Mathf.Sin(a0)) * r;
                var o1 = (u * Mathf.Cos(a1) + w * Mathf.Sin(a1)) * r;
                Quad(a + o0, b + o0, b + o1, a + o1, col);
                Tri(b, b + o0, b + o1, col);
                Tri(a, a + o1, a + o0, col);
            }
        }

        public void Ellipsoid(Vector3 center, Vector3 rad, int lon, int lat, float yaw, Color col)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 P(int i, int j)
            {
                float th = Mathf.PI * j / lat, ph = 2 * Mathf.PI * i / lon;
                return center + q * new Vector3(Mathf.Sin(th) * Mathf.Cos(ph) * rad.x, Mathf.Cos(th) * rad.y, Mathf.Sin(th) * Mathf.Sin(ph) * rad.z);
            }
            for (int j = 0; j < lat; j++)
                for (int i = 0; i < lon; i++)
                {
                    var a = P(i, j); var b = P(i + 1, j); var cc = P(i + 1, j + 1); var d = P(i, j + 1);
                    if (j == 0) Tri(a, cc, d, col);
                    else if (j == lat - 1) Tri(a, b, cc, col);
                    else Quad(a, b, cc, d, col);
                }
        }

        /// <summary>Altıgen halka (vurgular için): iç ve dış yarıçap, yükseklik.</summary>
        public void HexRing(Vector3 center, float r0, float r1, float h, Color col)
        {
            for (int k = 0; k < 6; k++)
            {
                float a0 = 30 + 60 * k, a1 = 30 + 60 * (k + 1);
                var i0 = Ring(center, r0, a0); var i1 = Ring(center, r0, a1);
                var o0 = Ring(center, r1, a0); var o1 = Ring(center, r1, a1);
                var up = Up * h;
                Quad(i0 + up, o0 + up, o1 + up, i1 + up, col);
                Quad(o0, o0 + up, o1 + up, o1, col);
                Quad(i0, i1, i1 + up, i0 + up, col);
            }
        }

        public void Apply(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetColors(c);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, an);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            // hareket eden parçalar (gemi, koyunlar, deniz canlıları, yükselen duman) sınır kutusundan taşmasın
            var bb = mesh.bounds;
            bb.SetMinMax(bb.min - new Vector3(0.8f, 1.1f, 0.8f), bb.max + new Vector3(0.8f, 1.2f, 0.8f));
            mesh.bounds = bb;
        }
    }

    /// <summary>
    /// 3B tahta: deniz, ada (karolar ve süslemeler), taşlar ve vurgular ayrı mesh'ler.
    /// Tahta koordinatı (x, y) dünyada (x, yükseklik, y) olur. Kamera eğik bakar, döner ve yakınlaşır.
    /// </summary>
    public class BoardView
    {
        public Camera cam;
        Light sun;
        readonly Mesh staticMesh = new Mesh(), pieceMesh = new Mesh(), outlineMesh = new Mesh(), overlayMesh = new Mesh(), waterMesh = new Mesh(), labelMesh = new Mesh(), iconMesh = new Mesh();
        readonly Mesh3 m = new Mesh3();
        readonly Mesh3 ol = new Mesh3(); // taşların dış hat kabukları (UpdatePieces)
        GameObject root;
        string boardKey = "";
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(10, 1, 10));
        readonly List<Vector3> fitPoints = new List<Vector3>();
        float[] hexTop = new float[0];
        Vector3[] tokenAt = new Vector3[0], harborAt = new Vector3[0];
        Terrain[] hexTerrain = new Terrain[0];

        Vector3 target = new Vector3(0, GroundY, 0);
        float dist = 18, yaw = 0, pitch = 55;
        Vector2 pivotGui;
        const float MinDist = 4f, MaxDist = 60f, GroundY = 0.22f, DefaultPitch = 55f, MinPitch = 25f, MaxPitch = 85f;

        static readonly Vector3 Up = Vector3.up, Down = Vector3.down;
        static Color C(string h) => Pal.Hex(h);

        // karo yükseklikleri: Volkan, Orman, Kil Ocağı, Otlak, Tarla, Dağ
        static readonly float[] Top = { 0.30f, 0.27f, 0.24f, 0.22f, 0.20f, 0.30f };

        // ada renk filtresi (hafif yumuşatılmış ama canlı) ve pul renkleri
        const float IslandSat = 0.88f, IslandLift = 0.03f;
        static readonly Color TokenTop = C("#E6D8BA"), LabelInk = C("#2B241D"), LabelRed = C("#B0473F");
        BoardData built;
        float labelYaw = float.NaN;

        static readonly Color Sand = C("#D9C48F"), SandSide = C("#B49A62"), Cliff = C("#5C4A36");
        static readonly Color Stone = C("#9C9890"), StoneDark = C("#6E6A63");
        static readonly Color Trunk = C("#5A3D24"), PineDark = C("#1F5A2E"), PineMid = C("#2C7240"), Leaf = C("#4F8F3A"), Log = C("#8A5A32");
        static readonly Color ClayPit = C("#7A3E25"), ClayRim = C("#9C5434"), Brick = C("#B24A2C"), BrickDark = C("#3A2A22");
        static readonly Color Wool = C("#F2EEE4"), SheepDark = C("#2F2A26"), GrassLight = C("#A6D46A"), FenceWood = C("#8A6A44");
        static readonly Color WheatA = C("#E2BE4A"), WheatB = C("#CFA43A"), Straw = C("#D9B85C"), StrawLight = C("#E8CC78");
        static readonly Color Plaster = C("#EDE6D6"), RoofRed = C("#8A3B2A"), Sail = C("#F4EEDD");
        static readonly Color Rock = C("#7D838A"), RockLight = C("#A3A8AD"), Snow = C("#F4F6F8"), CutStone = C("#BFC2C4");
        static readonly Color VolcRock = C("#3E302B"), CraterDark = C("#231A17"), Lava = C("#FF6A2A");
        static readonly Color Plank = C("#7A5532"), PlankTop = C("#9A7148"), Post = C("#4E3622");
        static readonly Color RobberRed = C("#D8352A");
        static readonly Color FlagBlack = C("#16130F"), Bone = C("#EDE7D8"), Chest = C("#5B3A1E"), Gold = C("#E0B53A");

        public void Init(Transform parent)
        {
            root = new GameObject("Board");
            root.transform.SetParent(parent);
            var lit = Resources.Load<Shader>("TakimadaLit");
            if (lit == null) lit = Shader.Find("Takimada/Lit");
            if (lit == null) lit = Shader.Find("Sprites/Default");
            var water = Resources.Load<Shader>("TakimadaWater");
            if (water == null) water = Shader.Find("Takimada/Water");
            if (water == null) water = lit;
            var mat = new Material(lit);
            if (mat.HasProperty("_Glow")) mat.SetFloat("_Glow", 1.0f);
            var wmat = new Material(water);
            Layer("Deniz", waterMesh, wmat, true, true);
            Layer("Ada", staticMesh, mat, true, true);
            Layer("Taslar", pieceMesh, mat, true, true);
            // köy, kasaba ve yolların koyu dış hattı: biraz büyük kabuk, yalnızca arka yüzleri çizilir
            var olMat = new Material(lit);
            if (olMat.HasProperty("_Cull")) olMat.SetFloat("_Cull", (float)CullMode.Front);
            Layer("Hatlar", outlineMesh, olMat, false, false);
            // pul yazıları ve liman sembolleri pulların üst yüzeyine basılır
            var iconMat = new Material(lit);
            if (iconMat.HasProperty("_MainTex")) iconMat.mainTexture = Icons.Atlas(Pal.Soft(TokenTop, IslandSat, IslandLift));
            Layer("Etiketler", labelMesh, mat, false, true);
            Layer("Semboller", iconMesh, iconMat, false, true);
            Layer("Vurgu", overlayMesh, mat, false, false);

            var camGo = new GameObject("Kamera");
            camGo.transform.SetParent(parent);
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Pal.Haze;
            cam.allowMSAA = true;
            camGo.AddComponent<AudioListener>();

            var sunGo = new GameObject("Gunes");
            sunGo.transform.SetParent(root.transform);
            sunGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = C("#F5E9D6");
            sun.intensity = 0.88f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.42f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.25f;
            RenderSettings.sun = sun;

            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 90f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.antiAliasing = 4;
            Shader.SetGlobalColor("_TkSky", C("#C3D6DE") * 0.55f);
            Shader.SetGlobalColor("_TkGround", C("#9A8B78") * 0.42f);
            Shader.SetGlobalColor("_TkFogColor", Pal.Haze);
            Shader.SetGlobalFloat("_TkWaveAmp", 1f);
            Shader.SetGlobalFloat("_TkRipple", 1f);
            ApplyCamera();
        }

        void Layer(string name, Mesh mesh, Material mat, bool cast, bool receive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = receive;
            mesh.MarkDynamic();
        }

        public void SetVisible(bool on) { if (root != null) root.SetActive(on); }

        public static string KeyOf(BoardData b)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var h in b.hexes) sb.Append((int)h.terrain).Append(h.number).Append(',');
            sb.Append(b.harbors.Count);
            sb.Append(b.fog ? "f" : "");
            return sb.ToString();
        }

        /// <summary>Yeni harita geldiyse adayı yeniden kurar. true = yeniden kuruldu.</summary>
        public bool EnsureBoard(BoardData b)
        {
            string key = KeyOf(b);
            if (key == boardKey) return false;
            boardKey = key;
            BuildStatic(b);
            return true;
        }

        /// <summary>Etiket konumları bu haritaya mı ait?</summary>
        public bool Matches(BoardData b) => b != null && hexTop.Length == b.hexes.Count && harborAt.Length == b.harbors.Count;

        public Vector3 TokenAnchor(int i) => i >= 0 && i < tokenAt.Length ? tokenAt[i] : Vector3.zero;
        public Vector3 HarborAnchor(int i) => i >= 0 && i < harborAt.Length ? harborAt[i] : Vector3.zero;

        static Vector3 W(float x, float y, float h = 0) => new Vector3(x, h, y);

        // ===================================================================== ada

        void BuildStatic(BoardData b)
        {
            m.Sat = IslandSat; m.Lift = IslandLift;   // ada rengi biraz yumuşatılır
            int n = b.hexes.Count;
            hexTop = new float[n];
            hexTerrain = new Terrain[n];
            tokenAt = new Vector3[n];
            fitPoints.Clear();
            m.Clear();

            for (int i = 0; i < n; i++)
            {
                hexTerrain[i] = b.hexes[i].terrain;
                hexTop[i] = b.fog ? 0.22f : Top[(int)b.hexes[i].terrain];
            }

            // kumsal: her karonun altında biraz daha geniş bir taban; kıyıda plaj olarak görünür
            foreach (var h in b.hexes)
                m.Prism(W(h.x, h.y, -0.22f), 1.13f, 0.28f, 6, 30, Sand, SandSide);

            for (int i = 0; i < n; i++)
            {
                var h = b.hexes[i];
                var c = W(h.x, h.y);
                float top = hexTop[i];
                var rnd = new System.Random(i * 7919 + 31 + (int)h.terrain * 13 + (int)(h.x * 100) * 7);
                int t = (int)h.terrain;
                if (b.fog) Tile(c, top, Pal.Fog, Pal.FogEdge, Color.Lerp(Pal.FogEdge, Cliff, 0.5f), rnd);
                else
                {
                    Tile(c, top, Pal.TerrainFill[t], Pal.TerrainEdge[t], Color.Lerp(Pal.TerrainEdge[t], Cliff, 0.55f), rnd);
                    Decorate(h, c, top, rnd);
                }
                float ty = top + 0.025f;
                if (b.fog || h.number != 0)
                    m.Frustum(c + Up * (ty - 0.01f), 0.34f, 0.33f, 0.06f, 20, 0, TokenTop, Pal.TokenEdge);
                tokenAt[i] = c + Up * (ty + 0.05f);
                for (int k = 0; k < 6; k++) fitPoints.Add(Mesh3.Ring(c + Up * top, 1.05f, 30 + 60 * k));
            }

            harborAt = new Vector3[b.harbors.Count];
            harborAnim = new Vector4[b.harbors.Count];
            for (int i = 0; i < b.harbors.Count; i++)
            {
                var hb = b.harbors[i];
                Harbor(b, hb, i, b.fog);
                harborAt[i] = ShipLabelPoint(b, hb);
                fitPoints.Add(W(hb.x, hb.y, 0.1f));
            }
            SeaLife(b);
            m.Apply(staticMesh);
            m.Sat = 1f; m.Lift = 0f;

            if (fitPoints.Count > 0)
            {
                bounds = new Bounds(fitPoints[0], Vector3.zero);
                foreach (var p in fitPoints) bounds.Encapsulate(p);
            }
            BuildWater(b);
            built = b;
            labelYaw = float.NaN;
            BuildLabels();
        }

        void Tile(Vector3 c, float top, Color fill, Color edge, Color side, System.Random rnd)
        {
            var center = c + Up * (top + 0.025f);
            for (int k = 0; k < 6; k++)
            {
                float a0 = 30 + 60 * k, a1 = 30 + 60 * (k + 1);
                var p0 = Mesh3.Ring(c + Up * top, 0.92f, a0); var p1 = Mesh3.Ring(c + Up * top, 0.92f, a1);
                var b0 = Mesh3.Ring(c + Up * (top - 0.035f), 0.965f, a0); var b1 = Mesh3.Ring(c + Up * (top - 0.035f), 0.965f, a1);
                var s0 = Mesh3.Ring(c, 0.965f, a0); var s1 = Mesh3.Ring(c, 0.965f, a1);
                m.Tri(center, p1, p0, Jitter(fill, rnd, 0.045f));
                m.Quad(p0, p1, b1, b0, edge);
                m.Quad(b0, b1, s1, s0, side);
            }
        }

        static Color Jitter(Color col, System.Random rnd, float amt)
        {
            float f = 1f + ((float)rnd.NextDouble() - 0.5f) * 2f * amt;
            return new Color(col.r * f, col.g * f, col.b * f, 1f);
        }

        static Vector3 Ground(Vector3 c, float top, Vector2 off) =>
            new Vector3(c.x + off.x, top + 0.025f * (1f - Mathf.Clamp01(off.magnitude / 0.92f)), c.z + off.y);

        static List<Vector2> Spots(System.Random rnd, int count, float rMin, float rMax, float sep, int tries = 80)
        {
            var list = new List<Vector2>();
            for (int k = 0; k < tries && list.Count < count; k++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Lerp(rMin, rMax, Mathf.Sqrt((float)rnd.NextDouble()));
                var p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                bool ok = true;
                foreach (var q in list) if ((q - p).sqrMagnitude < sep * sep) { ok = false; break; }
                if (ok) list.Add(p);
            }
            return list;
        }

        void Decorate(Hex h, Vector3 c, float top, System.Random rnd)
        {
            float R() => (float)rnd.NextDouble();
            Vector3 G(Vector2 o) => Ground(c, top, o);
            switch (h.terrain)
            {
                case Terrain.Orman:
                {
                    var spots = Spots(rnd, 12, 0.42f, 0.78f, 0.16f);
                    for (int k = 0; k < spots.Count; k++)
                    {
                        if (k == 0) { LogPile(G(spots[k]), R() * 180f); continue; }
                        float s = 0.8f + R() * 0.5f;
                        if (R() < 0.72f) Pine(G(spots[k]), s, R() * 60f, rnd);
                        else Broadleaf(G(spots[k]), s, rnd);
                    }
                    break;
                }
                case Terrain.KilOcagi:
                {
                    var spots = Spots(rnd, 6, 0.44f, 0.74f, 0.25f);
                    for (int k = 0; k < spots.Count; k++)
                    {
                        var g = G(spots[k]);
                        if (k == 0) Kiln(g, rnd);
                        else if (k < 3) m.Frustum(g, 0.15f, 0.13f, 0.012f, 9, R() * 40f, ClayPit, ClayRim);
                        else BrickStack(g, R() * 90f, rnd);
                    }
                    break;
                }
                case Terrain.Otlak:
                {
                    var spots = Spots(rnd, 14, 0.42f, 0.78f, 0.14f);
                    for (int k = 0; k < spots.Count; k++)
                    {
                        var g = G(spots[k]);
                        if (k < 4) Sheep(g, R() * 360f, rnd);
                        else if (k == 4) Fence(g, R() * 180f);
                        else if (k == 5 && R() < 0.6f) Broadleaf(g, 0.8f, rnd);
                        else Tuft(g, rnd);
                    }
                    break;
                }
                case Terrain.Tarla:
                {
                    Field(c, top, rnd);
                    var spots = Spots(rnd, 3, 0.5f, 0.72f, 0.32f);
                    for (int k = 0; k < spots.Count; k++)
                    {
                        var g = G(spots[k]) + Up * 0.03f;
                        if (k == 0 && R() < 0.65f) Windmill(g, rnd);
                        else Haystack(g, rnd);
                    }
                    break;
                }
                case Terrain.Maden:
                {
                    var peaks = Spots(rnd, 3, 0.58f, 0.66f, 0.42f);
                    foreach (var o in peaks) Mountain(G(o), 0.2f + R() * 0.06f, 0.36f + R() * 0.2f, rnd);
                    foreach (var o in Spots(rnd, 6, 0.4f, 0.78f, 0.12f))
                    {
                        bool inside = false;
                        foreach (var p in peaks) if ((p - o).magnitude < 0.25f) inside = true;
                        if (inside) continue;
                        var g = G(o);
                        if (R() < 0.6f) { float a = 0.035f + R() * 0.035f; m.Ellipsoid(g + Up * 0.015f, new Vector3(a, a * 0.7f, a * 0.85f), 5, 3, R() * 360f, Jitter(RockLight, rnd, 0.08f)); }
                        else m.Box(g, new Vector3(0.08f, 0.05f, 0.055f), R() * 90f, CutStone);
                    }
                    break;
                }
                case Terrain.Volkan:
                    Volcano(c, top, rnd);
                    break;
            }
        }

        void Pine(Vector3 g, float s, float rot, System.Random rnd)
        {
            var dark = Jitter(PineDark, rnd, 0.1f);
            var mid = Jitter(PineMid, rnd, 0.1f);
            m.Cylinder(g, g + Up * 0.07f * s, 0.02f * s, 5, Trunk);
            m.Frustum(g + Up * 0.05f * s, 0.12f * s, 0f, 0.17f * s, 6, rot, dark, dark);
            m.Frustum(g + Up * 0.13f * s, 0.095f * s, 0f, 0.15f * s, 6, rot + 30, mid, mid);
            m.Frustum(g + Up * 0.21f * s, 0.06f * s, 0f, 0.11f * s, 6, rot, mid, mid);
        }

        void Broadleaf(Vector3 g, float s, System.Random rnd)
        {
            m.Cylinder(g, g + Up * 0.12f * s, 0.022f * s, 5, Trunk);
            m.Ellipsoid(g + Up * 0.18f * s, new Vector3(0.1f, 0.085f, 0.1f) * s, 6, 4, (float)rnd.NextDouble() * 360f, Jitter(Leaf, rnd, 0.1f));
        }

        void LogPile(Vector3 g, float yaw)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            for (int i = -1; i <= 1; i++)
                m.Cylinder(g + q * new Vector3(-0.09f, 0.022f, i * 0.043f), g + q * new Vector3(0.09f, 0.022f, i * 0.043f), 0.021f, 6, Log);
            for (int i = 0; i < 2; i++)
                m.Cylinder(g + q * new Vector3(-0.08f, 0.06f, (i - 0.5f) * 0.043f), g + q * new Vector3(0.08f, 0.06f, (i - 0.5f) * 0.043f), 0.021f, 6, Log);
        }

        void Kiln(Vector3 g, System.Random rnd)
        {
            m.Frustum(g, 0.12f, 0.085f, 0.15f, 8, 0, BrickDark, Brick);
            m.Cylinder(g + new Vector3(0.05f, 0.08f, 0.03f), g + new Vector3(0.05f, 0.24f, 0.03f), 0.022f, 6, Brick);
            float R() => (float)rnd.NextDouble();
            var smoke = g + new Vector3(0.05f, 0.3f, 0.03f);
            m.Ellipsoid(smoke, Vector3.one * 0.04f, 6, 4, 0, C("#8E8A86"));
            m.Ellipsoid(smoke + new Vector3(0.03f + R() * 0.02f, 0.09f, 0.01f), Vector3.one * 0.055f, 6, 4, 0, C("#A29E9A"));
            m.Ellipsoid(smoke + new Vector3(0.07f + R() * 0.03f, 0.2f, 0.02f), Vector3.one * 0.07f, 6, 4, 0, C("#B8B4B0"));
        }

        void BrickStack(Vector3 g, float yaw, System.Random rnd)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            for (int layer = 0; layer < 3; layer++)
                for (int i = -1; i <= 1; i++)
                {
                    bool alongZ = layer % 2 == 0;
                    var off = alongZ ? new Vector3(i * 0.045f, layer * 0.028f, 0) : new Vector3(0, layer * 0.028f, i * 0.045f);
                    var size = alongZ ? new Vector3(0.04f, 0.026f, 0.12f) : new Vector3(0.12f, 0.026f, 0.04f);
                    m.Box(g + q * off, size, yaw, Jitter(Brick, rnd, 0.12f));
                }
        }

        void Sheep(Vector3 g, float yaw, System.Random rnd)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            // shader koyunu bu nokta etrafında gezdirir/döndürür, başını otlamak için eğer
            float phase = (float)rnd.NextDouble();
            m.Anim = new Vector4(g.x, g.z, phase, Mesh3.AnimSheep);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    m.Cylinder(g + q * new Vector3(sx * 0.045f, 0, sz * 0.03f), g + q * new Vector3(sx * 0.045f, 0.065f, sz * 0.03f), 0.012f, 4, SheepDark);
            m.Ellipsoid(g + Up * 0.095f, new Vector3(0.095f, 0.065f, 0.065f), 7, 4, yaw, Jitter(Wool, rnd, 0.04f));
            m.Anim.w = Mesh3.AnimSheepHead;
            m.Ellipsoid(g + q * new Vector3(0.1f, 0.11f, 0), new Vector3(0.04f, 0.035f, 0.032f), 5, 3, yaw, SheepDark);
            m.Anim = Vector4.zero;
        }

        void Fence(Vector3 g, float yaw)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            for (int i = 0; i < 4; i++)
            {
                var p = g + q * new Vector3(-0.12f + i * 0.08f, 0, 0);
                m.Cylinder(p, p + Up * 0.075f, 0.01f, 4, FenceWood);
            }
            foreach (var y in new[] { 0.035f, 0.062f })
                m.Cylinder(g + q * new Vector3(-0.12f, y, 0), g + q * new Vector3(0.12f, y, 0), 0.007f, 4, FenceWood);
        }

        void Tuft(Vector3 g, System.Random rnd)
        {
            float R() => (float)rnd.NextDouble();
            m.Frustum(g, 0.03f, 0f, 0.05f + R() * 0.03f, 4, R() * 90f, GrassLight, GrassLight);
            m.Frustum(g + new Vector3(0.03f, 0, 0.02f), 0.022f, 0f, 0.04f + R() * 0.02f, 4, R() * 90f, GrassLight, GrassLight);
        }

        void Field(Vector3 c, float top, System.Random rnd)
        {
            float th = 60f * rnd.Next(3);
            var q = Quaternion.Euler(0, th, 0);
            const float Rad = 0.86f;
            float ri = Rad * 0.866f, hole = 0.4f;
            int idx = 0;
            void Strip(float x0, float x1, float zz, Color col)
            {
                if (x1 - x0 < 0.05f) return;
                var mid = c + q * new Vector3((x0 + x1) / 2f, 0, zz) + Up * (top + 0.005f);
                var cc = Jitter(col, rnd, 0.05f);
                m.Box(mid, new Vector3(x1 - x0, 0.045f, 0.1f), th, cc * 0.85f, cc);
            }
            for (float z = -0.72f; z <= 0.73f; z += 0.13f, idx++)
            {
                float az = Mathf.Abs(z);
                float hw = (az <= Rad / 2 ? ri : ri * (Rad - az) / (Rad / 2)) - 0.04f;
                if (hw < 0.06f) continue;
                var col = idx % 2 == 0 ? WheatA : WheatB;
                if (az < hole)
                {
                    float cut = Mathf.Sqrt(hole * hole - z * z);
                    Strip(-hw, -cut, z, col);
                    Strip(cut, hw, z, col);
                }
                else Strip(-hw, hw, z, col);
            }
        }

        void Haystack(Vector3 g, System.Random rnd)
        {
            float r = (float)rnd.NextDouble() * 45f;
            m.Frustum(g, 0.07f, 0.065f, 0.05f, 8, r, Straw, Straw);
            m.Frustum(g + Up * 0.05f, 0.065f, 0f, 0.07f, 8, r, StrawLight, StrawLight);
        }

        void Windmill(Vector3 g, System.Random rnd)
        {
            m.Frustum(g, 0.075f, 0.05f, 0.24f, 6, 0, Plaster, Plaster);
            m.Frustum(g + Up * 0.24f, 0.065f, 0f, 0.08f, 6, 0, RoofRed, RoofRed);
            var hub = g + Up * 0.21f + Vector3.back * 0.065f;
            float a0 = (float)rnd.NextDouble() * 90f;
            for (int k = 0; k < 4; k++)
            {
                float a = (a0 + 90 * k) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                var pp = new Vector3(-d.y, d.x, 0);
                m.Quad(hub + d * 0.02f - pp * 0.012f, hub + d * 0.2f - pp * 0.035f, hub + d * 0.2f + pp * 0.035f, hub + d * 0.02f + pp * 0.012f, Sail);
                m.Cylinder(hub, hub + d * 0.2f, 0.006f, 4, Post);
            }
            m.Ellipsoid(hub, Vector3.one * 0.02f, 5, 3, 0, Post);
        }

        void Mountain(Vector3 g, float r, float h, System.Random rnd)
        {
            float rot = (float)rnd.NextDouble() * 72f;
            var rock = Jitter(Rock, rnd, 0.07f);
            m.Frustum(g + Down * 0.01f, r, 0f, h, 5, rot, rock, rock);
            const float k = 0.62f;
            m.Frustum(g + Up * (h * k - 0.005f), r * (1 - k) * 1.08f, 0f, h * (1 - k) + 0.012f, 5, rot, Snow, Snow);
            // yanında küçük bir tepe
            var side = g + Quaternion.Euler(0, rot * 3f, 0) * new Vector3(r * 0.7f, 0, 0);
            m.Frustum(side + Down * 0.01f, r * 0.55f, 0f, h * 0.5f, 5, rot + 20, Jitter(Rock, rnd, 0.07f), Jitter(Rock, rnd, 0.07f));
        }

        const float VolR0 = 0.8f, VolR1 = 0.2f, VolH = 0.5f;

        static float VolcanoHeight(float r) => r < VolR1 ? VolH : r > VolR0 ? 0 : VolH * (VolR0 - r) / (VolR0 - VolR1);

        void Volcano(Vector3 c, float top, System.Random rnd)
        {
            float R() => (float)rnd.NextDouble();
            float rot = R() * 40f;
            var b = c + Up * (top - 0.01f);
            m.Frustum(b, VolR0, VolR1, VolH, 9, rot, CraterDark, VolcRock, false);
            m.Frustum(b + Up * (VolH - 0.09f), VolR1 * 0.95f, VolR1 * 0.95f, 0.05f, 9, rot, Glow(Lava, 1f), Glow(Lava, 0.6f));
            m.Frustum(b + Up * (VolH - 0.045f), VolR1 * 0.85f, VolR1, 0.045f, 9, rot, CraterDark, CraterDark, false);
            for (int s = 0; s < 3; s++)
            {
                float a = (rot + 40 + s * 120 + R() * 30) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var perp = new Vector3(-dir.z, 0, dir.x);
                for (int seg = 0; seg < 4; seg++)
                {
                    float r0 = VolR1 + seg * 0.12f, r1 = r0 + 0.12f;
                    var p0 = b + dir * r0 + Up * (VolcanoHeight(r0) + 0.012f);
                    var p1 = b + dir * r1 + Up * (VolcanoHeight(r1) + 0.012f);
                    float w = 0.03f + seg * 0.008f;
                    m.Quad(p0 - perp * w, p1 - perp * w, p1 + perp * w, p0 + perp * w, Glow(Lava, 0.9f - seg * 0.2f));
                }
            }
            // duman: kraterden sırayla çıkan topaklar; shader yükseltir, büyütür, rüzgârla savurur ve söndürür
            var smoke = b + Up * (VolH + 0.03f);
            const int Puffs = 6;
            float ph0 = R();
            for (int k = 0; k < Puffs; k++)
            {
                m.Anim = new Vector4(smoke.x, smoke.z, Mesh3.Packed(smoke.y * 1000f, ph0 + (k + R() * 0.3f) / Puffs), Mesh3.AnimSmoke);
                m.Ellipsoid(smoke, Vector3.one * (0.075f + R() * 0.02f), 7, 4, R() * 360f, C("#4E4744"));
            }
            m.Anim = Vector4.zero;
        }

        // ===================================================================== deniz canlıları

        static readonly Color DolphinBack = C("#5D7385"), DolphinBelly = C("#C7D2D8");
        static readonly Color WhaleBack = C("#3D5263"), WhaleBelly = C("#8EA2AE"), Spray = C("#EEF6F7");

        // yunus sıçrayış yayı (TakimadaLit.shader ile aynı olmalı): eksen y = DolphinAxisY, yarıçap DolphinArc
        const float DolphinAxisY = -0.34f, DolphinArc = 0.52f;

        /// <summary>Adanın açıklarına birkaç yunus sürüsü ve balina serpiştirir. Hareketi shader verir.</summary>
        void SeaLife(BoardData b)
        {
            if (b.hexes.Count == 0) return;
            int seed = b.hexes.Count * 7919 + b.harbors.Count * 131;
            foreach (var h in b.hexes) seed = seed * 31 + (int)h.terrain + Mathf.RoundToInt(h.x * 10) * 3 + Mathf.RoundToInt(h.y * 10);
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();

            bool big = b.hexes.Count > 24;
            int pods = big ? 3 : 2, whales = big ? 2 : 1;
            var taken = new List<Vector2>();
            foreach (var hb in b.harbors) taken.Add(new Vector2(hb.x, hb.y));

            // kıyıdan biraz açıkta, limanlardan ve birbirinden uzak bir nokta
            bool Spot(float dMin, float dMax, float sep, out Vector2 p)
            {
                float rad = 0;
                foreach (var h in b.hexes) rad = Mathf.Max(rad, new Vector2(h.x, h.y).magnitude);
                for (int tries = 0; tries < 300; tries++)
                {
                    float a = R() * Mathf.PI * 2f, r = rad + dMin + R() * (dMax - dMin) * 1.2f;
                    p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                    float d = float.MaxValue;
                    foreach (var h in b.hexes) d = Mathf.Min(d, Vector2.Distance(p, new Vector2(h.x, h.y)));
                    if (d < dMin || d > dMax) continue;
                    bool ok = true;
                    foreach (var q in taken) if (Vector2.Distance(p, q) < sep) { ok = false; break; }
                    if (!ok) continue;
                    taken.Add(p);
                    return true;
                }
                p = Vector2.zero;
                return false;
            }

            for (int i = 0; i < whales; i++)
                if (Spot(2.6f, 3.6f, 2.4f, out var p)) Whale(W(p.x, p.y), R() * 360f, R());
            for (int i = 0; i < pods; i++)
            {
                if (!Spot(2.2f, 3.2f, 2.2f, out var p)) continue;
                float yaw = R() * 360f, ph = R();
                var q = Quaternion.Euler(0, yaw, 0);
                int count = 2 + (R() < 0.5f ? 1 : 0);
                for (int k = 0; k < count; k++)
                {
                    var off = q * new Vector3((k - (count - 1) / 2f) * 0.2f, 0, -Mathf.Abs(k - (count - 1) / 2f) * 0.16f);
                    Dolphin(W(p.x, p.y) + off, yaw, ph + k * 0.03f + R() * 0.01f, 0.9f + R() * 0.2f);
                }
            }
            m.Anim = Vector4.zero;
        }

        /// <summary>Yunus, sıçrayışın en tepesindeki duruşuyla kurulur; shader onu yay üstünde döndürür.</summary>
        void Dolphin(Vector3 at, float yawDeg, float phase, float s)
        {
            var q = Quaternion.Euler(0, yawDeg, 0);
            var f = q * Vector3.forward; var side = q * Vector3.right;
            var c = new Vector3(at.x, DolphinAxisY + DolphinArc, at.z);
            m.Anim = new Vector4(at.x, at.z, Mesh3.Packed(Mathf.Repeat(yawDeg, 360f), phase), Mesh3.AnimDolphin);
            m.Ellipsoid(c, new Vector3(0.042f, 0.038f, 0.15f) * s, 8, 5, yawDeg, DolphinBack);
            m.Ellipsoid(c + Down * 0.012f * s + f * 0.01f * s, new Vector3(0.034f, 0.03f, 0.125f) * s, 8, 4, yawDeg, DolphinBelly);
            m.Ellipsoid(c + (f * 0.155f + Down * 0.01f) * s, new Vector3(0.014f, 0.012f, 0.035f) * s, 5, 3, yawDeg, DolphinBack);
            // sırt yüzgeci
            m.Tri(c + (Up * 0.034f + f * 0.03f) * s, c + (Up * 0.078f - f * 0.045f) * s, c + (Up * 0.034f - f * 0.045f) * s, DolphinBack);
            // yan yüzgeçler
            for (int k = -1; k <= 1; k += 2)
                m.Tri(c + (f * 0.06f + side * k * 0.03f + Down * 0.02f) * s, c + (f * 0.01f + side * k * 0.075f + Down * 0.04f) * s, c + (f * 0.02f + side * k * 0.03f + Down * 0.02f) * s, DolphinBack);
            // kuyruk
            var t0 = c - f * 0.14f * s;
            m.Tri(t0, t0 + (-f * 0.06f + side * 0.058f) * s, t0 - f * 0.035f * s, DolphinBack);
            m.Tri(t0, t0 - f * 0.035f * s, t0 + (-f * 0.06f - side * 0.058f) * s, DolphinBack);
        }

        // balina fıskiyesinin tabanı (TakimadaLit.shader ile aynı olmalı)
        const float SpoutY = 0.07f;

        /// <summary>Balina su yüzündeki duruşuyla kurulur (at = püskürtme deliği). Shader onu yüzdürür, daldırır.</summary>
        void Whale(Vector3 at, float yawDeg, float phase)
        {
            var q = Quaternion.Euler(0, yawDeg, 0);
            var f = q * Vector3.forward; var side = q * Vector3.right;
            var anim = new Vector4(at.x, at.z, Mesh3.Packed(Mathf.Repeat(yawDeg, 360f), phase), Mesh3.AnimWhale);
            m.Anim = anim;
            var body = at - f * 0.12f + Down * 0.02f;
            m.Ellipsoid(body, new Vector3(0.13f, 0.085f, 0.38f), 10, 6, yawDeg, WhaleBack);
            m.Ellipsoid(body + Down * 0.03f + f * 0.04f, new Vector3(0.11f, 0.06f, 0.32f), 10, 4, yawDeg, WhaleBelly);
            m.Ellipsoid(body - f * 0.4f + Up * 0.005f, new Vector3(0.055f, 0.045f, 0.14f), 7, 4, yawDeg, WhaleBack);
            // yan yüzgeçler
            for (int k = -1; k <= 1; k += 2)
                m.Tri(body + f * 0.12f + side * k * 0.1f + Down * 0.04f, body + f * 0.0f + side * k * 0.22f + Down * 0.06f, body + f * 0.04f + side * k * 0.1f + Down * 0.04f, WhaleBack);
            // kuyruk yüzgeci
            var t0 = body - f * 0.52f + Up * 0.01f;
            m.Tri(t0, t0 - f * 0.1f + side * 0.17f, t0 - f * 0.05f, WhaleBack);
            m.Tri(t0, t0 - f * 0.05f, t0 - f * 0.1f - side * 0.17f, WhaleBack);
            m.Tri(t0 - f * 0.05f, t0 - f * 0.1f + side * 0.17f, t0 - f * 0.13f + side * 0.1f, WhaleBack);
            m.Tri(t0 - f * 0.05f, t0 - f * 0.13f - side * 0.1f, t0 - f * 0.1f - side * 0.17f, WhaleBack);
            // fıskiye: shader yalnızca su yüzündeyken açar
            anim.w = Mesh3.AnimSpout;
            m.Anim = anim;
            var sp = new Vector3(at.x, SpoutY, at.z);
            var mist = Glow(Spray, 0.15f);
            m.Ellipsoid(sp + Up * 0.05f, new Vector3(0.025f, 0.05f, 0.025f), 6, 3, 0, mist);
            m.Ellipsoid(sp + Up * 0.15f, new Vector3(0.045f, 0.06f, 0.045f), 6, 3, 30, mist);
            m.Ellipsoid(sp + Up * 0.24f + side * 0.05f, new Vector3(0.05f, 0.04f, 0.05f), 6, 3, 10, mist);
            m.Ellipsoid(sp + Up * 0.24f - side * 0.05f, new Vector3(0.05f, 0.04f, 0.05f), 6, 3, 50, mist);
            m.Anim = Vector4.zero;
        }

        // korsan gemisi ölçüleri (yerel: x yan, y yukarı, z baş yönü)
        const float ShipL = 0.72f, ShipB = 0.32f, ShipDeck = 0.13f, ShipKeel = -0.07f, ShipLabelZ = -0.08f;
        const float ShipScale = 1.3f; // geminin ve üstündeki etiketin büyüklüğü
        static readonly Color Hull = C("#4A2F1C"), HullRail = C("#6B4428"), Deck = C("#D2B98F"), Cannon = C("#1A1714"), SailDark = C("#D8CFBA");

        Vector4[] harborAnim = new Vector4[0];

        void Harbor(BoardData b, Harbor hb, int index, bool fog)
        {
            var e = b.edges[hb.edge];
            var hp = W(hb.x, hb.y);
            var va = W(b.verts[e.a].x, b.verts[e.a].y);
            var vb = W(b.verts[e.b].x, b.verts[e.b].y);
            var mid = Vector3.Lerp(va, vb, 0.5f);
            var toShore = (mid - hp); toShore.y = 0; toShore.Normalize();

            // iskele: gemiden kıyıdaki iki köşeye uzanan iki tahta yol (sabit, gemiyle sallanmaz)
            var gang = hp + toShore * (ShipB * 0.5f * ShipScale - 0.02f);
            foreach (var vp in new[] { va, vb })
            {
                var d = vp - gang;
                float yawDeg = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                m.Box(Vector3.Lerp(gang, vp, 0.5f) + Up * 0.07f, new Vector3(0.1f, 0.03f, d.magnitude), yawDeg, Plank, PlankTop);
                foreach (var f in new[] { 0.3f, 0.65f })
                {
                    var pt = Vector3.Lerp(gang, vp, f);
                    m.Cylinder(pt + Down * 0.2f, pt + Up * 0.07f, 0.016f, 5, Post);
                }
            }

            var ax = vb - va; ax.y = 0; ax.Normalize();
            float phase = Mathf.Repeat(hb.x * 0.37f + hb.y * 0.71f, 1f);
            var anim = new Vector4(hp.x, hp.z, phase, Mesh3.AnimShip);
            harborAnim[index] = anim;
            m.Anim = anim;
            var sail = fog || hb.type == T.GenericHarbor ? SailDark : Color.Lerp(SailDark, Pal.Res[hb.type], 0.7f);
            PirateShip(hp, ax, sail);
            m.Anim = Vector4.zero;
        }

        /// <summary>Gövde, güverte, kıç kasarası, top delikleri, direk, şişkin yelken ve kurukafalı bayrak.</summary>
        void PirateShip(Vector3 c, Vector3 fwd, Color sail)
        {
            const float K = ShipScale;
            var side = Vector3.Cross(Up, fwd);
            float yawDeg = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            // ölçüler ShipScale ile büyütülür; aşağıdaki sayılar ölçeksiz gemiye göre
            Vector3 L(float x, float y, float z) => c + (side * x + Up * y + fwd * z) * K;

            float hb = ShipB / 2, hl = ShipL / 2;
            // güverte ve omurga dış hatları: kıç-sol, orta-sol, baş, orta-sağ, kıç-sağ
            var top = new[] { L(-hb, ShipDeck, -hl), L(-hb, ShipDeck, ShipL * 0.12f), L(0, ShipDeck, hl + 0.05f), L(hb, ShipDeck, ShipL * 0.12f), L(hb, ShipDeck, -hl) };
            var bot = new[] { L(-hb * 0.45f, ShipKeel, -hl * 0.85f), L(-hb * 0.45f, ShipKeel, ShipL * 0.08f), L(0, ShipKeel, hl * 0.8f), L(hb * 0.45f, ShipKeel, ShipL * 0.08f), L(hb * 0.45f, ShipKeel, -hl * 0.85f) };
            for (int i = 0; i < top.Length; i++)
            {
                int j = (i + 1) % top.Length;
                m.Quad(top[i], top[j], bot[j], bot[i], Hull);
            }
            var deckC = L(0, ShipDeck, 0);
            for (int i = 0; i < top.Length; i++) m.Tri(deckC, top[(i + 1) % top.Length], top[i], Deck);
            // küpeşte
            for (int i = 0; i < top.Length; i++)
            {
                int j = (i + 1) % top.Length;
                m.Cylinder(top[i] + Up * 0.02f * K, top[j] + Up * 0.02f * K, 0.012f * K, 4, HullRail);
            }
            // gövdede açık renk şerit ve top delikleri
            for (int s = -1; s <= 1; s += 2)
            {
                m.Box(L(s * (hb + 0.002f), ShipDeck - 0.05f, -0.02f), new Vector3(0.006f, 0.02f, ShipL * 0.7f) * K, yawDeg, HullRail);
                for (int k = -1; k <= 1; k++)
                    m.Box(L(s * (hb + 0.004f), ShipDeck - 0.045f, k * 0.14f - 0.02f), new Vector3(0.012f, 0.03f, 0.04f) * K, yawDeg, Cannon);
            }
            // kıç kasarası
            m.Box(L(0, ShipDeck, -hl + 0.07f), new Vector3(ShipB, 0.07f, 0.14f) * K, yawDeg, Hull, HullRail);
            // cıvadra
            m.Cylinder(L(0, ShipDeck + 0.02f, hl + 0.03f), L(0, ShipDeck + 0.09f, hl + 0.2f), 0.01f * K, 4, Post);

            // direk, seren ve yelken (etiketleri kapatmasın diye başa yakın ve yüksekte)
            float mz = 0.24f, y0 = ShipDeck + 0.26f, y1 = ShipDeck + 0.46f, sw = 0.17f;
            m.Cylinder(L(0, ShipDeck, mz), L(0, ShipDeck + 0.58f, mz), 0.014f * K, 5, Post);
            m.Cylinder(L(-sw - 0.02f, y1, mz + 0.01f), L(sw + 0.02f, y1, mz + 0.01f), 0.008f * K, 4, Post);
            m.Cylinder(L(-sw - 0.02f, y0, mz + 0.01f), L(sw + 0.02f, y0, mz + 0.01f), 0.008f * K, 4, Post);
            const float Belly = 0.05f;
            float ym = (y0 + y1) / 2;
            var a0 = L(-sw, y1, mz + 0.015f); var a1 = L(sw, y1, mz + 0.015f);
            var b0 = L(-sw * 0.95f, ym, mz + 0.015f + Belly); var b1 = L(sw * 0.95f, ym, mz + 0.015f + Belly);
            var c0 = L(-sw, y0, mz + 0.015f); var c1 = L(sw, y0, mz + 0.015f);
            m.Quad(a0, a1, b1, b0, sail);
            m.Quad(b0, b1, c1, c0, Darker(sail, 0.92f));
            // kurukafalı bayrak
            var ft = L(0, ShipDeck + 0.58f, mz);
            var f1 = ft + (-fwd * 0.17f + Up * 0.01f) * K;
            var f2 = ft + (-fwd * 0.16f - Up * 0.1f) * K;
            var f3 = ft - Up * 0.1f * K;
            m.Quad(ft, f1, f2, f3, FlagBlack);
            var skull = Vector3.Lerp(ft, f2, 0.5f);
            m.Ellipsoid(skull + side * 0.004f * K, new Vector3(0.004f, 0.022f, 0.022f) * K, 6, 3, yawDeg, Bone);
            m.Ellipsoid(skull - side * 0.004f * K, new Vector3(0.004f, 0.022f, 0.022f) * K, 6, 3, yawDeg, Bone);
        }

        /// <summary>Liman etiketinin basılacağı güverte noktası.</summary>
        static Vector3 ShipLabelPoint(BoardData b, Harbor hb)
        {
            var e = b.edges[hb.edge];
            var ax = W(b.verts[e.b].x, b.verts[e.b].y) - W(b.verts[e.a].x, b.verts[e.a].y);
            ax.y = 0; ax.Normalize();
            return W(hb.x, hb.y, ShipDeck * ShipScale + 0.002f) + ax * ShipLabelZ * ShipScale;
        }

        // su köşe rengi alfası: kıyıya uzaklık / ShoreRange (TakimadaWater.shader ile aynı olmalı)
        const float ShoreRange = 3f;

        void BuildWater(BoardData b)
        {
            float rad = 2f;
            foreach (var h in b.hexes) rad = Mathf.Max(rad, new Vector2(h.x, h.y).magnitude + 1f);
            float half = rad + 30f;
            int cells = 150;
            float step = half * 2f / cells;
            int row = cells + 1;
            var verts = new Vector3[row * row];
            var cols = new Color32[row * row];
            var norms = new Vector3[row * row];
            var shallow = C("#3DA5AE"); var mid = C("#1E7493"); var deep = C("#14506C"); var foam = C("#A9CFCB");
            for (int j = 0; j < row; j++)
                for (int i = 0; i < row; i++)
                {
                    float x = -half + i * step, z = -half + j * step;
                    float d = 99f;
                    foreach (var h in b.hexes)
                    {
                        float dx = x - h.x, dz = z - h.y;
                        d = Mathf.Min(d, Mathf.Sqrt(dx * dx + dz * dz) - 0.97f);
                    }
                    var col0 = Color.Lerp(shallow, mid, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1.8f, d)));
                    col0 = Color.Lerp(col0, deep, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.5f, 7f, d)));
                    if (d < 0.36f) col0 = Color.Lerp(foam, col0, Mathf.InverseLerp(0.16f, 0.36f, d));
                    int idx = j * row + i;
                    verts[idx] = new Vector3(x, 0, z);
                    var wc = Pal.Soft(col0, 0.92f, 0.02f);
                    wc.a = Mathf.Clamp01(d / ShoreRange); // kıyıya uzaklık: shader dalgaları bununla çizer
                    cols[idx] = wc;
                    norms[idx] = Vector3.up;
                }
            var tris = new int[cells * cells * 6];
            int t = 0;
            for (int j = 0; j < cells; j++)
                for (int i = 0; i < cells; i++)
                {
                    int a = j * row + i, bb = a + 1, cc = a + row, dd = cc + 1;
                    tris[t++] = a; tris[t++] = cc; tris[t++] = bb;
                    tris[t++] = bb; tris[t++] = cc; tris[t++] = dd;
                }
            waterMesh.Clear();
            waterMesh.indexFormat = IndexFormat.UInt32;
            waterMesh.vertices = verts;
            waterMesh.normals = norms;
            waterMesh.colors32 = cols;
            waterMesh.triangles = tris;
            waterMesh.RecalculateBounds();
            var bnd = waterMesh.bounds;
            bnd.Expand(new Vector3(0, 0.2f, 0));
            waterMesh.bounds = bnd;
        }

        // ===================================================================== pul yazıları

        // Çizgi font: her karakter yüksekliği 1 olan bir hücrede çoklu çizgilerden oluşur (tek noktalı çizgi = nokta).
        static readonly Dictionary<char, (float w, Vector2[][] strokes)> Glyphs = BuildGlyphs();

        static Vector2[] Oval(float cx, float cy, float rx, float ry, int n = 14)
        {
            var p = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.PI * 2 * i / n + Mathf.PI / 2;
                p[i] = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
            }
            return p;
        }

        static Vector2[] P(params float[] xy)
        {
            var p = new Vector2[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return p;
        }

        static Dictionary<char, (float, Vector2[][])> BuildGlyphs()
        {
            var six = P(0.52f, 0.93f, 0.32f, 1f, 0.13f, 0.88f, 0.04f, 0.55f, 0.05f, 0.22f, 0.18f, 0.03f, 0.38f, 0f, 0.54f, 0.12f, 0.58f, 0.32f, 0.48f, 0.52f, 0.28f, 0.57f, 0.08f, 0.45f);
            var nine = new Vector2[six.Length];
            for (int i = 0; i < six.Length; i++) nine[i] = new Vector2(0.6f - six[i].x, 1f - six[i].y);
            return new Dictionary<char, (float, Vector2[][])>
            {
                ['0'] = (0.6f, new[] { Oval(0.3f, 0.5f, 0.27f, 0.48f) }),
                ['1'] = (0.42f, new[] { P(0.08f, 0.78f, 0.3f, 1f, 0.3f, 0f) }),
                ['2'] = (0.6f, new[] { P(0.04f, 0.76f, 0.12f, 0.93f, 0.3f, 1f, 0.48f, 0.93f, 0.56f, 0.76f, 0.5f, 0.58f, 0.04f, 0f, 0.6f, 0f) }),
                ['3'] = (0.6f, new[] { P(0.05f, 0.9f, 0.22f, 1f, 0.42f, 0.98f, 0.55f, 0.84f, 0.52f, 0.64f, 0.3f, 0.54f), P(0.3f, 0.54f, 0.52f, 0.45f, 0.59f, 0.26f, 0.5f, 0.07f, 0.3f, 0f, 0.05f, 0.08f) }),
                ['4'] = (0.6f, new[] { P(0.46f, 0f, 0.46f, 1f, 0.02f, 0.32f, 0.6f, 0.32f) }),
                ['5'] = (0.6f, new[] { P(0.56f, 1f, 0.1f, 1f, 0.06f, 0.56f, 0.28f, 0.63f, 0.48f, 0.57f, 0.59f, 0.34f, 0.52f, 0.1f, 0.32f, 0f, 0.05f, 0.08f) }),
                ['6'] = (0.6f, new[] { six }),
                ['8'] = (0.6f, new[] { Oval(0.3f, 0.76f, 0.21f, 0.24f, 12), Oval(0.3f, 0.27f, 0.27f, 0.27f, 12) }),
                ['9'] = (0.6f, new[] { nine }),
                ['?'] = (0.6f, new[] { P(0.06f, 0.78f, 0.15f, 0.95f, 0.32f, 1f, 0.48f, 0.94f, 0.55f, 0.78f, 0.47f, 0.6f, 0.3f, 0.48f, 0.3f, 0.28f), P(0.3f, 0.04f) }),
                [':'] = (0.24f, new[] { P(0.12f, 0.22f), P(0.12f, 0.72f) }),
            };
        }

        /// <summary>Yazıyı yatay bir yüzeye basar. c: ortası, right/fwd: yüzeydeki ekran sağı ve yukarısı, h: yükseklik.</summary>
        void FlatText(string s, Vector3 c, Vector3 right, Vector3 fwd, float h, Color col)
        {
            float gap = 0.16f * h, total = 0;
            foreach (var ch in s) if (Glyphs.TryGetValue(ch, out var g)) total += g.w * h + gap;
            total -= gap;
            float x = -total / 2f, w = 0.17f * h;
            foreach (var ch in s)
            {
                if (!Glyphs.TryGetValue(ch, out var g)) continue;
                foreach (var stroke in g.strokes)
                {
                    Vector3 At(Vector2 q) => c + right * (x + q.x * h) + fwd * ((q.y - 0.5f) * h);
                    if (stroke.Length == 1) { m.Disc(At(stroke[0]), w * 0.8f, 10, col); continue; }
                    for (int i = 0; i + 1 < stroke.Length; i++)
                    {
                        var a = At(stroke[i]); var b = At(stroke[i + 1]);
                        var side = Vector3.Cross(Vector3.up, (b - a).normalized) * (w / 2f);
                        m.Quad(a - side, a + side, b + side, b - side, col);
                        m.Disc(b, w / 2f, 8, col);
                    }
                    m.Disc(At(stroke[0]), w / 2f, 8, col);
                }
                x += g.w * h + gap;
            }
        }

        /// <summary>Pul sayılarını, olasılık noktalarını ve liman oranlarını kameranın yönüne göre yeniden basar.</summary>
        void BuildLabels()
        {
            labelYaw = yaw;
            var b = built;
            if (b == null) return;
            var right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            const float Raise = 0.003f;
            m.Clear();
            m.Sat = IslandSat; m.Lift = IslandLift;
            for (int i = 0; i < b.hexes.Count && i < tokenAt.Length; i++)
            {
                var h = b.hexes[i];
                var top = tokenAt[i] + Vector3.up * Raise;
                if (b.fog || h.number < 0) { FlatText("?", top, right, fwd, 0.26f, LabelInk); continue; }
                if (h.number == 0) continue;
                var col = h.number == 6 || h.number == 8 ? LabelRed : LabelInk;
                string txt = h.number.ToString();
                FlatText(txt, top + fwd * 0.05f, right, fwd, txt.Length > 1 ? 0.22f : 0.26f, col);
                int pips = 6 - Mathf.Abs(7 - h.number);
                for (int k = 0; k < pips; k++)
                    m.Disc(top + fwd * -0.185f + right * ((k - (pips - 1) / 2f) * 0.06f), 0.024f, 10, col);
            }
            var icons = new Mesh3 { Sat = IslandSat, Lift = IslandLift };
            for (int i = 0; i < b.harbors.Count && i < harborAt.Length; i++)
            {
                var hb = b.harbors[i];
                var top = harborAt[i] + Vector3.up * Raise;
                // etiket güverteye basılı: gemiyle aynı şekilde sallanır
                var anim = i < harborAnim.Length ? harborAnim[i] : Vector4.zero;
                m.Anim = anim; icons.Anim = anim;
                if (b.fog) { FlatText("?", top, right, fwd, 0.21f, LabelInk); continue; }
                if (hb.type == T.GenericHarbor) { FlatText(GenericRatio + ":1", top, right, fwd, 0.17f, LabelInk); continue; }
                FlatText(SpecificRatio + ":1", top + fwd * -0.16f, right, fwd, 0.12f, LabelInk);
                // sembol: atlastaki kendi bölümü, pulun üstünde kare
                var ic = top + fwd * 0.075f;
                float s = 0.12f, u0 = hb.type / (float)T.ResCount, u1 = (hb.type + 1) / (float)T.ResCount;
                var bl = ic - right * s - fwd * s; var br = ic + right * s - fwd * s;
                var tr = ic + right * s + fwd * s; var tl = ic - right * s + fwd * s;
                icons.TriUV(bl, tl, tr, new Vector2(u0, 0), new Vector2(u0, 1), new Vector2(u1, 1), Color.white);
                icons.TriUV(bl, tr, br, new Vector2(u0, 0), new Vector2(u1, 1), new Vector2(u1, 0), Color.white);
            }
            m.Anim = Vector4.zero;
            m.Apply(labelMesh);
            m.Sat = 1f; m.Lift = 0f;
            icons.Apply(iconMesh);
        }

        // liman oranları kurallardan gelir; App her durum/lobi değişiminde günceller
        public int GenericRatio = 3, SpecificRatio = 2;

        public void SetHarborRatios(int generic, int specific)
        {
            if (generic == GenericRatio && specific == SpecificRatio) return;
            GenericRatio = generic; SpecificRatio = specific;
            if (built != null) BuildLabels();
        }

        // ===================================================================== taşlar

        float EdgeTop(BoardData b, int e)
        {
            float y = 0.1f;
            foreach (var h in b.edges[e].hexes) if (h < hexTop.Length) y = Mathf.Max(y, hexTop[h]);
            return y;
        }

        float VertTop(BoardData b, int v)
        {
            float y = 0.1f;
            foreach (var h in b.verts[v].hexes) if (h < hexTop.Length) y = Mathf.Max(y, hexTop[h]);
            return y;
        }

        static Color Darker(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        static Color Glow(Color c, float amount) { c.a = 1f - Mathf.Clamp01(amount); return c; }

        // taş boyutları ve dış hat kalınlığı
        const float HouseK = 1.3f, CityK = 1.65f, RoadW = 0.15f, RoadH = 0.09f, RoadLen = 0.72f, Edge = 0.018f;
        static readonly Color EdgeInk = C("#15110D");

        /// <summary>Hayalet yol (yerleştirme önizlemesi): düz kutu, uçları köşeden kısa.</summary>
        void Road(Vector3 a, Vector3 b, float y, Color col)
        {
            var d = b - a;
            float yawDeg = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            var c = Vector3.Lerp(a, b, 0.5f) + Up * (y - 0.005f);
            m.Box(c, new Vector3(RoadW, RoadH, d.magnitude * RoadLen), yawDeg, Darker(col, 0.85f), col);
        }

        /// <summary>Bir yol ucunun bilgisi. joined: aynı oyuncunun başka yolu bu köşede birleşiyor.</summary>
        struct RoadEnd
        {
            public bool joined;
            public float y;        // bu uçtaki yol yüksekliği (birleşen yollar köşede aynı yüksekliğe gelir)
            public float gapL, gapR; // sol/sağ komşu yola açı (derece); u'ya göre sol = +90°
        }

        // xz düzleminde döndürme (x, z bir 2B vektör gibi)
        static Vector3 RotXZ(Vector3 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            return new Vector3(v.x * cs - v.z * sn, 0, v.x * sn + v.z * cs);
        }

        /// <summary>
        /// Uç köşe noktaları (u: köşeden yolun içine doğru birim yön). Birleşmeyen uç köşeden kısa ve düz kesilir;
        /// birleşen uç, komşu yollarla aradaki açının ortasından gönye gibi kesilir, böylece yollar üst üste binmeden
        /// tam oturur. Sırayla: sol, (köşe), sağ.
        /// </summary>
        static void EndCorners(Vector3 v, Vector3 u, RoadEnd end, float w, float gap, List<Vector3> pts)
        {
            var n = RotXZ(u, 90);
            if (!end.joined)
            {
                pts.Add(v + u * gap + n * w);
                pts.Add(v + u * gap - n * w);
                return;
            }
            pts.Add(v + RotXZ(u, end.gapL / 2) * (w / Mathf.Sin(end.gapL * 0.5f * Mathf.Deg2Rad)));
            pts.Add(v);
            pts.Add(v + RotXZ(u, -end.gapR / 2) * (w / Mathf.Sin(end.gapR * 0.5f * Mathf.Deg2Rad)));
        }

        readonly List<Vector3> roadPts = new List<Vector3>();

        /// <summary>Yol gövdesi: taban izi dışbükey çokgen olan prizma. Üst yüzey iki uç yüksekliği arasında eğimli.</summary>
        void RoadPrism(Mesh3 mesh, Vector3 a, Vector3 b, RoadEnd ea, RoadEnd eb, float w, float gap, float lift, Color top, Color side, bool bottom)
        {
            var d = b - a; d.y = 0;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            var dir = d / len;
            roadPts.Clear();
            EndCorners(a, dir, ea, w, gap, roadPts);
            int startB = roadPts.Count;
            EndCorners(b, -dir, eb, w, gap, roadPts);
            // A ucu: sol(+n) ... sağ(-n); B ucu u=-dir olduğundan sol(-n) ... sağ(+n). Çevre: A sırası, sonra B sırası.
            int count = roadPts.Count;
            // saat yönünde (yukarıdan, x sağ z ileri) sırala: Unity'de ön yüz saat yönü
            float area = 0;
            for (int i = 0; i < count; i++) { var p = roadPts[i]; var q = roadPts[(i + 1) % count]; area += p.x * q.z - q.x * p.z; }
            if (area > 0) roadPts.Reverse();

            float yBot = Mathf.Min(ea.y, eb.y) - 0.005f - lift;
            Vector3 Top(Vector3 p)
            {
                float t = Mathf.Clamp01(Vector3.Dot(p - a, dir) / len);
                return new Vector3(p.x, Mathf.Lerp(ea.y, eb.y, t) - 0.005f + RoadH + lift, p.z);
            }
            Vector3 Bot(Vector3 p) => new Vector3(p.x, yBot, p.z);

            var t0 = Top(roadPts[0]);
            for (int i = 1; i + 1 < count; i++) mesh.Tri(t0, Top(roadPts[i]), Top(roadPts[i + 1]), top);
            for (int i = 0; i < count; i++)
            {
                var p = roadPts[i]; var q = roadPts[(i + 1) % count];
                if ((q - p).sqrMagnitude < 1e-8f) continue;
                mesh.Quad(Bot(q), Top(q), Top(p), Bot(p), side);
            }
            if (!bottom) return;
            var b0 = Bot(roadPts[0]);
            for (int i = 1; i + 1 < count; i++) mesh.Tri(b0, Bot(roadPts[i + 1]), Bot(roadPts[i]), side);
        }

        /// <summary>Gerçek yol: gövde ve dış hat kabuğu.</summary>
        void RoadPiece(Vector3 a, Vector3 b, RoadEnd ea, RoadEnd eb, Color col)
        {
            float gap = Vector3.Distance(a, b) * (1f - RoadLen) / 2;
            RoadPrism(m, a, b, ea, eb, RoadW / 2, gap, 0, col, Darker(col, 0.85f), false);
            RoadPrism(ol, a, b, ea, eb, RoadW / 2 + Edge, gap - Edge, Edge, EdgeInk, EdgeInk, true);
        }

        void OBox(Vector3 b, Vector3 size, float yaw) => ol.Box(b + Down * Edge, size + Vector3.one * (2 * Edge), yaw, EdgeInk);

        void House(Vector3 p, float yawDeg, Color wall, Color roof, bool foundation = true, bool outline = false)
        {
            const float K = HouseK;
            var q = Quaternion.Euler(0, yawDeg, 0);
            if (foundation) m.Prism(p + Down * 0.04f, 0.15f * K, 0.05f, 6, 30, Stone, StoneDark);
            p += Up * 0.01f;
            var body = new Vector3(0.2f, 0.12f, 0.16f) * K;
            var chim = p + q * new Vector3(0.05f, 0.14f, 0.03f) * K;
            var chimSize = new Vector3(0.035f, 0.07f, 0.035f) * K;
            m.Box(p, body, yawDeg, wall);
            m.Roof(p + Up * body.y, body.x, body.z, 0.1f * K, yawDeg, roof, wall, 0.025f * K);
            m.Box(chim, chimSize, yawDeg, roof);
            if (!outline) return;
            OBox(p, body, yawDeg);
            ol.RoofShell(p + Up * body.y, body.x, body.z, 0.1f * K, yawDeg, 0.025f * K, Edge, EdgeInk);
            OBox(chim, chimSize, yawDeg);
        }

        void City(Vector3 p, float yawDeg, Color wall, Color roof, bool foundation = true, bool outline = false)
        {
            const float K = CityK;
            var q = Quaternion.Euler(0, yawDeg, 0);
            if (foundation) m.Prism(p + Down * 0.04f, 0.22f * K, 0.05f, 6, 30, Stone, StoneDark);
            p += Up * 0.01f;
            var hall = p + q * new Vector3(0.05f, 0, 0) * K;
            var hallSize = new Vector3(0.24f, 0.14f, 0.2f) * K;
            m.Box(hall, hallSize, yawDeg, wall);
            m.Roof(hall + Up * hallSize.y, hallSize.x, hallSize.z, 0.1f * K, yawDeg, roof, wall, 0.025f * K);
            var tower = p + q * new Vector3(-0.1f, 0, 0) * K;
            var towerSize = new Vector3(0.12f, 0.3f, 0.12f) * K;
            m.Box(tower, towerSize, yawDeg, wall);
            m.Frustum(tower + Up * towerSize.y, 0.1f * K, 0f, 0.13f * K, 4, 45 - yawDeg, roof, roof);
            if (!outline) return;
            OBox(hall, hallSize, yawDeg);
            ol.RoofShell(hall + Up * hallSize.y, hallSize.x, hallSize.z, 0.1f * K, yawDeg, 0.025f * K, Edge, EdgeInk);
            OBox(tower, towerSize, yawDeg);
            ol.ClosedCone(tower + Up * (towerSize.y - Edge), 0.1f * K + Edge * 1.5f, 0.13f * K + Edge * 2.6f, 4, 45 - yawDeg, EdgeInk);
        }

        void Robber(Vector3 p)
        {
            const float K = 1.4f; // korsan uzaktan da seçilsin diye büyük
            m.Ellipsoid(p + Up * 0.01f, new Vector3(0.09f, 0.035f, 0.08f) * K, 6, 3, 0, StoneDark);
            m.Cylinder(p, p + Up * 0.52f * K, 0.016f * K, 5, Post);
            var t0 = p + Up * 0.5f * K;
            m.Quad(t0, t0 + new Vector3(0.25f, 0.01f, 0.03f) * K, t0 + new Vector3(0.22f, -0.18f, 0.02f) * K, t0 + new Vector3(0f, -0.19f, 0f) * K, FlagBlack);
            m.Tri(t0 + new Vector3(0.22f, -0.18f, 0.02f) * K, t0 + new Vector3(0.12f, -0.19f, 0.01f) * K, t0 + new Vector3(0.17f, -0.24f, 0.015f) * K, FlagBlack);
            m.Ellipsoid(t0 + new Vector3(0.11f, -0.09f, 0.012f) * K, new Vector3(0.032f, 0.032f, 0.016f) * K, 6, 3, 0, Bone);
            var chest = p + new Vector3(0.1f, 0, -0.1f) * K;
            m.Box(chest, new Vector3(0.12f, 0.07f, 0.08f) * K, 20f, Chest);
            m.Box(chest + Up * 0.07f * K, new Vector3(0.1f, 0.015f, 0.06f) * K, 20f, Gold);
        }

        static float PieceYaw(int v) => ((v * 7919) % 5) * 12f - 24f;

        public void UpdatePieces(GameState s)
        {
            m.Clear();
            var b = s.board;
            ol.Clear();
            if (!Matches(b)) { m.Apply(pieceMesh); ol.Apply(outlineMesh); return; }
            // köşede bina yoksa ve aynı oyuncunun iki ya da daha çok yolu buluşuyorsa yollar birleşir:
            // her köşe/oyuncu için oradan çıkan yolların yön açıları ve en yüksek yol yüksekliği
            var joinAngles = new Dictionary<long, List<float>>();
            var joinHigh = new Dictionary<long, float>();
            long Key(int v, int o) => (long)v * 64 + o;
            Vector3 VW(int v) => W(b.verts[v].x, b.verts[v].y);
            float Angle(int from, int to) { var d = VW(to) - VW(from); return Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg; }
            for (int e = 0; e < s.eOwner.Length; e++)
            {
                int o = s.eOwner[e];
                if (o < 0) continue;
                float y = EdgeTop(b, e);
                var ed = b.edges[e];
                for (int k = 0; k < 2; k++)
                {
                    int v = k == 0 ? ed.a : ed.b, other = k == 0 ? ed.b : ed.a;
                    if (v < s.vOwner.Length && s.vOwner[v] >= 0) continue;
                    long key = Key(v, o);
                    if (!joinAngles.TryGetValue(key, out var list)) joinAngles[key] = list = new List<float>();
                    list.Add(Angle(v, other));
                    joinHigh[key] = joinHigh.TryGetValue(key, out float hi) ? Mathf.Max(hi, y) : y;
                }
            }
            RoadEnd End(int v, int other, int o, float y)
            {
                var end = new RoadEnd { y = y };
                if (!joinAngles.TryGetValue(Key(v, o), out var list) || list.Count < 2) return end;
                float self = Angle(v, other), minD = 360, maxD = 0;
                bool skipped = false;
                foreach (float a in list)
                {
                    if (!skipped && Mathf.Abs(Mathf.DeltaAngle(a, self)) < 0.5f) { skipped = true; continue; }
                    float diff = Mathf.Repeat(a - self, 360f);
                    if (diff < 1f || diff > 359f) continue;
                    minD = Mathf.Min(minD, diff);
                    maxD = Mathf.Max(maxD, diff);
                }
                if (minD >= 360) return end;
                end.joined = true;
                end.y = Mathf.Max(joinHigh[Key(v, o)], VertTop(b, v)); // köşedeki en yüksek karonun üstünde buluşur
                end.gapL = minD;
                end.gapR = 360f - maxD;
                return end;
            }
            for (int e = 0; e < s.eOwner.Length; e++)
            {
                int o = s.eOwner[e];
                if (o < 0) continue;
                var ed = b.edges[e];
                float y = EdgeTop(b, e);
                RoadPiece(VW(ed.a), VW(ed.b), End(ed.a, ed.b, o, y), End(ed.b, ed.a, o, y), Pal.PieceColor(s.players[o].color));
            }
            for (int v = 0; v < s.vOwner.Length; v++)
            {
                int o = s.vOwner[v];
                if (o < 0) continue;
                var p = W(b.verts[v].x, b.verts[v].y, VertTop(b, v));
                var col = Pal.PieceColor(s.players[o].color);
                if (s.vLevel[v] == 2) City(p, PieceYaw(v), col, Darker(col, 0.62f), true, true);
                else House(p, PieceYaw(v), col, Darker(col, 0.62f), true, true);
            }
            if (s.robber >= 0 && s.robber < b.hexes.Count)
            {
                var h = b.hexes[s.robber];
                var off = new Vector2(-0.56f, -0.3f);
                float y = hexTop[s.robber] + (hexTerrain[s.robber] == Terrain.Volkan ? VolcanoHeight(off.magnitude) : 0.01f);
                Robber(W(h.x + off.x, h.y + off.y, y));
            }
            m.Apply(pieceMesh);
            ol.Apply(outlineMesh);
        }

        // ===================================================================== vurgular

        public enum PickKind { None, Vertex, Edge, Hex }

        /// <summary>vertexLevel: köşe seçiminde fare üstündeyken gösterilecek hayalet (1 köy, 2 kasaba).</summary>
        public void UpdateOverlay(GameState s, PickKind kind, List<int> candidates, int hover, Color accent, float flash, int vertexLevel = 1)
        {
            m.Clear();
            if (s != null && Matches(s.board))
            {
                var b = s.board;
                if (flash > 0.01f && s.lastProduction != null)
                    foreach (var h in s.lastProduction)
                        if (h >= 0 && h < b.hexes.Count)
                            m.HexRing(W(b.hexes[h].x, b.hexes[h].y, hexTop[h] + 0.01f + flash * 0.05f), 0.8f, 0.95f, 0.035f, Glow(C("#FFD36B"), flash));

                // korsanın durduğu karo: sürekli, yavaş nabız atan kırmızı çerçeve
                if (s.robber >= 0 && s.robber < b.hexes.Count && s.robber < hexTop.Length)
                {
                    float rp = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f);
                    var rc = W(b.hexes[s.robber].x, b.hexes[s.robber].y, hexTop[s.robber] + 0.012f);
                    m.HexRing(rc, 0.8f, 0.96f, 0.045f + 0.015f * rp, Glow(RobberRed, 0.3f + 0.4f * rp));
                }

                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
                var soft = Glow(C("#E8E4DA"), 0.2f + 0.3f * pulse);
                if (candidates != null)
                    foreach (var id in candidates)
                    {
                        bool hv = id == hover;
                        switch (kind)
                        {
                            case PickKind.Vertex:
                            {
                                var p = W(b.verts[id].x, b.verts[id].y, VertTop(b, id));
                                if (hv)
                                {
                                    var wall = Glow(accent, 0.5f);
                                    var roof = Glow(Darker(accent, 0.7f), 0.5f);
                                    if (vertexLevel == 2) City(p + Up * 0.005f, PieceYaw(id), wall, roof, false);
                                    else House(p, PieceYaw(id), wall, roof, false);
                                }
                                else
                                {
                                    float bob = Mathf.Sin(Time.time * 3f + id) * 0.02f;
                                    m.Frustum(p + Up * (0.06f + bob), 0.09f, 0.09f, 0.025f, 12, 0, soft, soft);
                                }
                                break;
                            }
                            case PickKind.Edge:
                            {
                                var e = b.edges[id];
                                var va = W(b.verts[e.a].x, b.verts[e.a].y);
                                var vb = W(b.verts[e.b].x, b.verts[e.b].y);
                                float y = EdgeTop(b, id);
                                if (hv) Road(va, vb, y + 0.01f, Glow(accent, 0.5f));
                                else
                                {
                                    var d = vb - va;
                                    m.Box(Vector3.Lerp(va, vb, 0.5f) + Up * (y + 0.03f), new Vector3(0.05f, 0.025f, d.magnitude * 0.4f), Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, soft);
                                }
                                break;
                            }
                            case PickKind.Hex:
                            {
                                var c = W(b.hexes[id].x, b.hexes[id].y, hexTop[id] + 0.015f);
                                if (hv) m.HexRing(c, 0.7f, 0.95f, 0.05f, Glow(C("#F0503C"), 0.8f));
                                else m.HexRing(c, 0.86f, 0.93f, 0.03f, soft);
                                break;
                            }
                        }
                    }
            }
            m.Apply(overlayMesh);
        }

        // ===================================================================== kamera

        void ApplyCamera()
        {
            if (cam == null) return;
            pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
            dist = Mathf.Clamp(dist, MinDist, MaxDist);
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.rotation = rot;
            cam.transform.position = target - rot * Vector3.forward * dist;
            Shader.SetGlobalFloat("_TkFogStart", dist * 1.4f);
            Shader.SetGlobalFloat("_TkFogEnd", dist * 4.5f);
            if (built != null && !(Mathf.Abs(Mathf.DeltaAngle(yaw, labelYaw)) < 0.5f)) BuildLabels();
        }

        Vector3 GroundHit(Vector2 gui)
        {
            var ray = cam.ScreenPointToRay(new Vector3(gui.x, Screen.height - gui.y, 0));
            if (ray.direction.y > -1e-3f) return target;
            float t = Mathf.Min((GroundY - ray.origin.y) / ray.direction.y, 400f);
            return ray.origin + ray.direction * t;
        }

        void ClampTarget()
        {
            var c = bounds.center;
            var e = bounds.extents;
            target.x = Mathf.Clamp(target.x, c.x - e.x - 1.5f, c.x + e.x + 1.5f);
            target.z = Mathf.Clamp(target.z, c.z - e.z - 1.5f, c.z + e.z + 1.5f);
            target.y = GroundY;
        }

        public void Zoom(float factor, Vector2 gui)
        {
            if (cam == null) return;
            orbitPivotValid = false;
            var before = GroundHit(gui);
            dist = Mathf.Clamp(dist * factor, MinDist, MaxDist);
            ApplyCamera();
            var after = GroundHit(gui);
            target += new Vector3(before.x - after.x, 0, before.z - after.z);
            ClampTarget();
            ApplyCamera();
        }

        public void PanDrag(Vector2 fromGui, Vector2 toGui)
        {
            if (cam == null) return;
            orbitPivotValid = false;
            var a = GroundHit(fromGui);
            var b = GroundHit(toGui);
            target += new Vector3(a.x - b.x, 0, a.z - b.z);
            ClampTarget();
            ApplyCamera();
        }

        // Yörünge (sağ tık / Q-E): girdi birikir, TickCamera her karede yumuşakça uygular.
        float orbitYawLeft, orbitPitchLeft;
        Vector3 orbitPivot;
        bool orbitPivotValid;
        const float OrbitSmoothing = 20f; // büyüdükçe kamera girdiye daha çabuk yetişir

        /// <summary>Görünür alanın ortasındaki noktanın etrafında döner/eğer; o nokta ekranda yerinde kalır.</summary>
        public void Orbit(float dYaw, float dPitch)
        {
            if (cam == null) return;
            orbitYawLeft += dYaw;
            // sınırdayken eğme birikip sonradan "geri tepmesin"
            orbitPitchLeft = Mathf.Clamp(pitch + orbitPitchLeft + dPitch, MinPitch, MaxPitch) - pitch;
        }

        public void TickCamera(float dt)
        {
            if (cam == null) return;
            if (orbitYawLeft == 0 && orbitPitchLeft == 0) { orbitPivotValid = false; return; }
            if (!orbitPivotValid)
            {
                orbitPivot = GroundHit(pivotGui == Vector2.zero ? new Vector2(Screen.width / 2f, Screen.height / 2f) : pivotGui);
                orbitPivotValid = true;
            }
            bool settle = Mathf.Abs(orbitYawLeft) < 0.02f && Mathf.Abs(orbitPitchLeft) < 0.02f;
            float k = settle ? 1f : 1f - Mathf.Exp(-OrbitSmoothing * dt);
            float dy = orbitYawLeft * k, dp = orbitPitchLeft * k;
            orbitYawLeft -= dy; orbitPitchLeft -= dp;
            if (settle) { orbitYawLeft = 0; orbitPitchLeft = 0; }
            RotateAbout(orbitPivot, dy, dp);
        }

        /// <summary>Kamerayı yer düzlemindeki bir noktanın etrafında katı biçimde döndürür (yatay + eğim).</summary>
        void RotateAbout(Vector3 pivot, float dYaw, float dPitch)
        {
            float newPitch = Mathf.Clamp(pitch + dPitch, MinPitch, MaxPitch);
            dPitch = newPitch - pitch;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var camPos = target - rot * Vector3.forward * dist;
            var q = Quaternion.AngleAxis(dYaw, Vector3.up) * Quaternion.AngleAxis(dPitch, rot * Vector3.right);
            camPos = pivot + q * (camPos - pivot);
            yaw += dYaw;
            pitch = newPitch;
            var fwd = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
            float t = (GroundY - camPos.y) / fwd.y;
            if (t > 0)
            {
                target = camPos + fwd * t;
                target.y = GroundY;
                dist = t;
            }
            ApplyCamera();
        }

        void CancelOrbit() { orbitYawLeft = 0; orbitPitchLeft = 0; orbitPivotValid = false; }

        public void ResetAngles() { CancelOrbit(); yaw = 0; pitch = DefaultPitch; }

        public Vector2 MouseWorld(Vector2 guiPixel)
        {
            if (cam == null) return Vector2.zero;
            var p = GroundHit(guiPixel);
            return new Vector2(p.x, p.z);
        }

        /// <summary>Dünya noktasını ekran (GUI) pikseline çevirir; ppu o noktada 1 birimin piksel karşılığı.</summary>
        public bool Project(Vector3 world, out Vector2 gui, out float ppu)
        {
            gui = Vector2.zero; ppu = 0;
            if (cam == null) return false;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z < cam.nearClipPlane) return false;
            var sp2 = cam.WorldToScreenPoint(world + cam.transform.right);
            ppu = new Vector2(sp2.x - sp.x, sp2.y - sp.y).magnitude;
            gui = new Vector2(sp.x, Screen.height - sp.y);
            return true;
        }

        public int Nearest(GameState s, PickKind kind, List<int> candidates, Vector2 world)
        {
            if (s == null || candidates == null) return -1;
            int best = -1;
            float bestD = kind == PickKind.Hex ? 0.85f : 0.42f;
            foreach (var id in candidates)
            {
                Vector2 p;
                if (kind == PickKind.Vertex) p = new Vector2(s.board.verts[id].x, s.board.verts[id].y);
                else if (kind == PickKind.Edge) p = new Vector2(s.board.edges[id].x, s.board.edges[id].y);
                else p = new Vector2(s.board.hexes[id].x, s.board.hexes[id].y);
                float d = Vector2.Distance(p, world);
                if (d < bestD) { bestD = d; best = id; }
            }
            return best;
        }

        /// <summary>Adayı ekranın panellerle kapanmayan bölgesine sığdırır (kenar boşlukları piksel).</summary>
        public void Fit(float leftPx, float rightPx, float bottomPx, float topPx)
        {
            if (cam == null || fitPoints.Count == 0) return;
            float x0 = leftPx, x1 = Screen.width - rightPx, y0 = topPx, y1 = Screen.height - bottomPx;
            if (x1 - x0 < 150) { x0 = 0; x1 = Screen.width; }
            if (y1 - y0 < 150) { y0 = 0; y1 = Screen.height; }
            var want = new Vector2((x0 + x1) / 2f, (y0 + y1) / 2f);
            pivotGui = want;
            CancelOrbit();
            target = new Vector3(bounds.center.x, GroundY, bounds.center.z);
            for (int it = 0; it < 8; it++)
            {
                ApplyCamera();
                if (!ScreenBox(out var min, out var max)) break;
                float sx = (max.x - min.x) / (x1 - x0), sy = (max.y - min.y) / (y1 - y0);
                dist = Mathf.Clamp(dist * Mathf.Max(sx, sy) * 1.03f, MinDist, MaxDist);
                ApplyCamera();
                if (!ScreenBox(out min, out max)) break;
                var now = GroundHit((min + max) / 2f);
                var goal = GroundHit(want);
                target += new Vector3(now.x - goal.x, 0, now.z - goal.z);
            }
            ApplyCamera();
        }

        bool ScreenBox(out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in fitPoints)
            {
                if (!Project(p, out var g, out _)) continue;
                min = Vector2.Min(min, g);
                max = Vector2.Max(max, g);
            }
            return min.x <= max.x;
        }
    }
}
