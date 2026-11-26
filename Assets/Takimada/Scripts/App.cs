using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Takimada
{
    public enum View { Menu, Lobby, Game }

    [Serializable]
    public class Seat
    {
        public int id;
        public string name;
        public int color;
        public bool connected = true;
    }

    [Serializable]
    public class LobbyInfo
    {
        public List<Seat> seats = new List<Seat>();
        public GameConfig cfg = new GameConfig();
        public bool started;
        public string code = "";
    }

    [Serializable] class HelloMsg { public string name; public int protocol; }
    [Serializable] class SeatMsg { public int seat; }
    [Serializable] class TextMsg { public string text; }
    [Serializable] public class ChatLine { public string name; public int color; public string text; }

    /// <summary>
    /// Uygulamanın beyni: menü/lobi/oyun akışı, sunucu tarafı oda yönetimi ve hamlelerin yönlendirilmesi.
    /// Sahneye hiçbir şey koymaya gerek yok; oyun açılınca kendini kurar.
    /// </summary>
    public class App : MonoBehaviour
    {
        public const int Protocol = 4;
        public static App I;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("Takimada");
            DontDestroyOnLoad(go);
            I = go.AddComponent<App>();
        }

        // --- kullanıcı tercihleri ---
        public string playerName = "Kaptan";
        public string ip = "127.0.0.1";
        public string port = "7777";
        public string joinCode = "";

        // --- akış ---
        public View view = View.Menu;
        public bool isHost, isLocal, isOnlineRoom;
        public string status = "";
        public bool busy;
        public LobbyInfo lobby = new LobbyInfo();
        public int mySeatId = -1;
        public GameState state;
        public readonly List<ChatLine> chat = new List<ChatLine>();
        public int chatVersion; // her yeni mesajda artar (HUD kaydırmayı buna göre yapar)

        public NetSession net;
        public BoardView board;
        public Settings settings;
        AudioSource music;
        public Sfx sfx;
        Hud hud;

        // --- yalnız sunucu ---
        Engine engine;
        readonly Dictionary<ulong, int> clientSeat = new Dictionary<ulong, int>();
        int nextSeatId;
        bool lobbyDirty;
        float lobbySendTimer;

        void Awake()
        {
            I = this;
            Application.runInBackground = true;
            // FPS sınırı ekranın yenileme hızı (144 Hz ekranda 144); bilinemezse 144.
            // VSync açıksa (ayarlardan) build'de zaten yenileme hızına kilitlenir, bu değer editörde ve VSync kapalıyken işler.
            double hz = Screen.currentResolution.refreshRateRatio.value;
            Application.targetFrameRate = hz >= 30 ? Mathf.RoundToInt((float)hz) : 144;
            settings = Settings.Load();
            if (settings.hasDisplay) settings.ApplyDisplay();
            else { settings.ApplyVsync(); FixResolution(); }
            playerName = PlayerPrefs.GetString("tk.name", "Kaptan" + UnityEngine.Random.Range(10, 99));
            ip = PlayerPrefs.GetString("tk.ip", "127.0.0.1");
            port = PlayerPrefs.GetString("tk.port", "7777");
            lobby.cfg = LoadConfigOrDefault();

            board = new BoardView();
            board.Init(transform);
            board.SetVisible(false);
            hud = new Hud(this);

            // arka plan müziği (Resources/Music), döngüde
            music = gameObject.AddComponent<AudioSource>();
            music.clip = Resources.Load<AudioClip>("Music/PastoralStrategy");
            music.loop = true;
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.priority = 0;
            ApplySettings();
            if (music.clip != null) music.Play();
            else Debug.LogWarning("Satan: Resources/Music/PastoralStrategy bulunamadı, müzik çalmayacak.");
            sfx = new Sfx(gameObject);

            net = new NetSession();
            net.Init();
            net.ServerStarted += OnServerStarted;
            net.ConnectedToServer += OnConnectedToServer;
            net.Disconnected += OnDisconnected;
            net.ClientLeft += OnClientLeft;
            net.Message += OnMessage;
        }

        /// <summary>Ses ve parlaklığı uygular (ayarlar sürgüleri her değişimde çağırır).</summary>
        public void ApplySettings()
        {
            settings.ApplyAudio(music);
            settings.ApplyBrightness(board?.cam);
        }

        /// <summary>F11 ya da Alt+Enter: pencere ↔ kenarlıksız tam ekran (ekranın kendi çözünürlüğü).</summary>
        void ToggleFullscreen()
        {
            if (Application.isEditor) return;
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (!Input.GetKeyDown(KeyCode.F11) && !(alt && enter)) return;
            var r = Screen.currentResolution;
            settings.hasDisplay = true;
            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                settings.mode = WindowMode.Borderless;
                settings.width = r.width; settings.height = r.height;
            }
            else
            {
                settings.mode = WindowMode.Windowed;
                settings.width = Mathf.Min(1600, r.width * 4 / 5); settings.height = Mathf.Min(900, r.height * 4 / 5);
            }
            settings.ApplyDisplay();
            settings.Save();
        }

        /// <summary>
        /// Tam ekran ekranın kendi çözünürlüğünden düşük açılırsa görüntü büyütülüp bulanıklaşır.
        /// Öyleyse ekranın kendi çözünürlüğüne geç.
        /// </summary>
        static void FixResolution()
        {
            if (Application.isEditor || Screen.fullScreenMode == FullScreenMode.Windowed) return;
            var r = Screen.currentResolution;
            if (r.width > 0 && (Screen.width != r.width || Screen.height != r.height))
                Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
        }

        void Update()
        {
            if (hud == null) return; // Play açıkken script derlendi; Play'i durdurup yeniden başlat
            ToggleFullscreen();
            // süre dolduysa motor sıradaki hamleyi kendisi yapar (yalnızca sunucuda)
            if (isHost && engine != null && engine.Tick()) BroadcastState();
            if (isHost && !isLocal && lobbyDirty)
            {
                lobbySendTimer -= Time.unscaledDeltaTime;
                if (lobbySendTimer <= 0) { BroadcastLobby(); }
            }
            hud.Tick();
        }

        void OnGUI() => hud?.Draw();

        void OnApplicationQuit()
        {
            SavePrefs();
            net?.Shutdown();
        }

        public void SavePrefs()
        {
            PlayerPrefs.SetString("tk.name", playerName);
            PlayerPrefs.SetString("tk.ip", ip);
            PlayerPrefs.SetString("tk.port", port);
            settings?.Save();
            PlayerPrefs.Save();
        }

        ushort Port => ushort.TryParse(port, out var p) ? p : (ushort)7777;

        string CleanName()
        {
            var n = (playerName ?? "").Trim();
            if (n.Length == 0) n = "Kaptan";
            if (n.Length > 16) n = n.Substring(0, 16);
            return n;
        }

        // =====================================================================
        // Menü işlemleri

        public void StartLocal()
        {
            SavePrefs();
            isLocal = true; isHost = true; isOnlineRoom = false;
            lobby = new LobbyInfo { cfg = lobby.cfg };
            lobby.seats.Add(new Seat { id = 0, name = CleanName(), color = 0 });
            lobby.seats.Add(new Seat { id = 1, name = "Oyuncu 2", color = 1 });
            lobby.seats.Add(new Seat { id = 2, name = "Oyuncu 3", color = 2 });
            nextSeatId = 3;
            mySeatId = 0;
            view = View.Lobby;
        }

        public void HostDirect()
        {
            SavePrefs();
            isLocal = false; isOnlineRoom = false;
            status = "Sunucu başlatılıyor...";
            if (!net.StartHost(Port)) status = "Sunucu başlatılamadı. Port kullanımda olabilir.";
        }

        public void JoinDirect()
        {
            SavePrefs();
            isLocal = false; isHost = false; isOnlineRoom = false;
            status = ip + ":" + Port + " adresine bağlanılıyor...";
            if (!net.StartClient(ip.Trim(), Port)) status = "Bağlantı başlatılamadı.";
        }

        const string NotLinkedMsg = "Proje Unity Cloud'a bağlı değil. Editörde Edit > Project Settings > Services'ten projeyi bağla, sonra yeni build al.";

        public async void HostOnline()
        {
            if (OnlineRooms.Provider == null) { status = "Online paket yüklü değil (com.unity.services.multiplayer)."; return; }
            if (string.IsNullOrEmpty(Application.cloudProjectId)) { status = NotLinkedMsg; Debug.LogWarning("Satan: " + NotLinkedMsg); return; }
            SavePrefs();
            isLocal = false; isOnlineRoom = true; busy = true;
            status = "Unity Relay odası kuruluyor...";
            try
            {
                string code = await OnlineRooms.Provider.CreateRoom(8);
                lobby.code = code;
                status = "";
                MarkLobbyDirty();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                status = "Oda kurulamadı: " + e.Message + "\n(Unity Cloud panelinde Relay açık mı? Ayrıntı Console'da.)";
                net.Shutdown();
                view = View.Menu;
            }
            busy = false;
        }

        public async void JoinOnline()
        {
            if (OnlineRooms.Provider == null) { status = "Online paket yüklü değil (com.unity.services.multiplayer)."; return; }
            if (string.IsNullOrWhiteSpace(joinCode)) { status = "Oda kodunu yaz."; return; }
            if (string.IsNullOrEmpty(Application.cloudProjectId)) { status = NotLinkedMsg; Debug.LogWarning("Satan: " + NotLinkedMsg); return; }
            SavePrefs();
            isLocal = false; isHost = false; isOnlineRoom = true; busy = true;
            status = "Odaya katılınıyor...";
            try { await OnlineRooms.Provider.JoinRoom(joinCode); }
            catch (Exception e)
            {
                Debug.LogException(e);
                status = "Katılınamadı: " + e.Message;
                net.Shutdown();
            }
            busy = false;
        }

        public async void Leave()
        {
            var wasOnline = isOnlineRoom;
            net.Shutdown();
            if (wasOnline && OnlineRooms.Provider != null) await OnlineRooms.Provider.Leave();
            engine = null;
            state = null;
            clientSeat.Clear();
            chat.Clear();
            isHost = false; isLocal = false; isOnlineRoom = false;
            mySeatId = -1;
            lobby = new LobbyInfo { cfg = lobby.cfg };
            board.SetVisible(false);
            view = View.Menu;
        }

        // =====================================================================
        // Ağ olayları

        void OnServerStarted()
        {
            isHost = true; isLocal = false;
            clientSeat.Clear();
            chat.Clear();
            lobby = new LobbyInfo { cfg = lobby.cfg, code = lobby.code };
            lobby.seats.Add(new Seat { id = 0, name = CleanName(), color = 0 });
            nextSeatId = 1;
            mySeatId = 0;
            status = isOnlineRoom ? "" : "Sunucu hazır. Arkadaşların IP adresinle bağlanabilir (port " + Port + ").";
            view = View.Lobby;
        }

        void OnConnectedToServer()
        {
            status = "Bağlandı, lobiye giriliyor...";
            chat.Clear();
            net.SendToServer("hello", JsonUtility.ToJson(new HelloMsg { name = CleanName(), protocol = Protocol }));
        }

        void OnDisconnected(string reason)
        {
            bool wasOnline = isOnlineRoom;
            engine = null; state = null;
            isHost = false; isOnlineRoom = false;
            board.SetVisible(false);
            view = View.Menu;
            status = reason;
            net.Shutdown();
            if (wasOnline && OnlineRooms.Provider != null) _ = OnlineRooms.Provider.Leave();
        }

        void OnClientLeft(ulong client)
        {
            if (!clientSeat.TryGetValue(client, out int seatId)) return;
            clientSeat.Remove(client);
            var seat = lobby.seats.Find(s => s.id == seatId);
            if (seat == null) return;
            if (!lobby.started) lobby.seats.Remove(seat);
            else
            {
                seat.connected = false;
                if (engine != null) { engine.SetConnected(PlayerIndexOfSeat(seatId), false); BroadcastState(); }
            }
            hud.Toast(seat.name + " ayrıldı.");
            MarkLobbyDirty();
        }

        void OnMessage(ulong sender, string kind, string json)
        {
            try
            {
                if (net.IsServer) HostMessage(sender, kind, json);
                else ClientMessage(kind, json);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void HostMessage(ulong sender, string kind, string json)
        {
            switch (kind)
            {
                case "hello":
                {
                    var h = JsonUtility.FromJson<HelloMsg>(json);
                    if (h.protocol != Protocol) { net.Kick(sender, "Oyun sürümleri farklı. Herkes aynı build'i kullanmalı."); return; }
                    string name = string.IsNullOrWhiteSpace(h.name) ? "Kaptan" : h.name.Trim();
                    if (lobby.started)
                    {
                        var back = lobby.seats.Find(s => !s.connected && s.name == name);
                        if (back == null) { net.Kick(sender, "Oyun zaten başladı."); return; }
                        back.connected = true;
                        clientSeat[sender] = back.id;
                        net.Send(sender, "seat", JsonUtility.ToJson(new SeatMsg { seat = back.id }));
                        engine?.SetConnected(PlayerIndexOfSeat(back.id), true);
                        BroadcastLobby();
                        BroadcastState();
                        return;
                    }
                    if (lobby.seats.Count >= lobby.cfg.maxPlayers) { net.Kick(sender, "Oda dolu."); return; }
                    string unique = name; int k = 2;
                    while (lobby.seats.Any(s => s.name == unique)) unique = name + " " + k++;
                    var seat = new Seat { id = nextSeatId++, name = unique, color = FreeColor(-1) };
                    lobby.seats.Add(seat);
                    clientSeat[sender] = seat.id;
                    net.Send(sender, "seat", JsonUtility.ToJson(new SeatMsg { seat = seat.id }));
                    hud.Toast(unique + " katıldı.");
                    BroadcastLobby();
                    break;
                }
                case "chat":
                    if (clientSeat.TryGetValue(sender, out int chatSeat)) HostChat(chatSeat, JsonUtility.FromJson<TextMsg>(json).text);
                    break;
                case "color":
                    if (clientSeat.TryGetValue(sender, out int cs)) CycleColor(cs);
                    break;
                case "act":
                {
                    if (engine == null || !clientSeat.TryGetValue(sender, out int seatId)) return;
                    var a = JsonUtility.FromJson<GameAction>(json);
                    a.player = PlayerIndexOfSeat(seatId);
                    if (a.player < 0) return;
                    var err = engine.Apply(a);
                    if (err != null) net.Send(sender, "err", JsonUtility.ToJson(new TextMsg { text = err }));
                    else BroadcastState();
                    break;
                }
            }
        }

        void ClientMessage(string kind, string json)
        {
            switch (kind)
            {
                case "seat":
                    mySeatId = JsonUtility.FromJson<SeatMsg>(json).seat;
                    break;
                case "lobby":
                    lobby = JsonUtility.FromJson<LobbyInfo>(json);
                    status = "";
                    if (!lobby.started) { state = null; board.SetVisible(false); view = View.Lobby; }
                    break;
                case "state":
                    SetState(JsonUtility.FromJson<GameState>(json));
                    break;
                case "chat":
                    AddChat(JsonUtility.FromJson<ChatLine>(json));
                    break;
                case "err":
                    hud.Toast(JsonUtility.FromJson<TextMsg>(json).text, true);
                    break;
            }
        }

        // =====================================================================
        // Lobi (sunucu)

        public void MarkLobbyDirty()
        {
            lobbyDirty = true;
            if (lobbySendTimer <= 0) lobbySendTimer = 0.25f;
        }

        void BroadcastLobby()
        {
            lobbyDirty = false;
            lobbySendTimer = 0;
            if (isLocal || !net.IsServer) return;
            var json = JsonUtility.ToJson(lobby);
            foreach (var c in clientSeat.Keys.ToList()) net.Send(c, "lobby", json);
        }

        int FreeColor(int preferAfter)
        {
            for (int i = 1; i <= Pal.Player.Length; i++)
            {
                int c = (preferAfter + i + Pal.Player.Length) % Pal.Player.Length;
                if (!lobby.seats.Any(s => s.color == c)) return c;
            }
            return 0;
        }

        public void CycleColor(int seatId)
        {
            var seat = lobby.seats.Find(s => s.id == seatId);
            if (seat == null || lobby.started) return;
            if (!isHost) { net.SendToServer("color", "{}"); return; }
            seat.color = FreeColor(seat.color);
            MarkLobbyDirty();
        }

        public void AddLocalSeat()
        {
            if (!isLocal || lobby.seats.Count >= lobby.cfg.maxPlayers) return;
            lobby.seats.Add(new Seat { id = nextSeatId++, name = "Oyuncu " + (lobby.seats.Count + 1), color = FreeColor(-1) });
        }

        public void RemoveSeat(int seatId)
        {
            if (!isHost || seatId == mySeatId) return;
            var seat = lobby.seats.Find(s => s.id == seatId);
            if (seat == null) return;
            if (!isLocal)
            {
                foreach (var kv in clientSeat.Where(kv => kv.Value == seatId).ToList())
                {
                    clientSeat.Remove(kv.Key);
                    net.Kick(kv.Key, "Sunucu seni odadan çıkardı.");
                }
            }
            lobby.seats.Remove(seat);
            MarkLobbyDirty();
        }

        public void SetConfig(GameConfig c)
        {
            lobby.cfg = c;
            MarkLobbyDirty();
        }

        // =====================================================================
        // Oyun (sunucu)

        public void StartGame()
        {
            if (!isHost) return;
            var seats = lobby.seats.Select(s => new SeatInfo { name = s.name, color = s.color, seat = s.id }).ToList();
            engine = Engine.Create(lobby.cfg, seats);
            engine.Clock = () => Time.realtimeSinceStartupAsDouble;
            engine.StartClock();
            lobby.started = true;
            BroadcastLobby();
            BroadcastState();
        }

        public void BackToLobby()
        {
            if (!isHost) return;
            engine = null;
            state = null;
            lobby.started = false;
            lobby.seats.RemoveAll(s => !s.connected);
            board.SetVisible(false);
            view = View.Lobby;
            BroadcastLobby();
        }

        int PlayerIndexOfSeat(int seatId) => engine == null ? -1 : engine.S.players.FindIndex(p => p.seat == seatId);

        void BroadcastState()
        {
            if (engine == null) return;
            if (isLocal) { SetState(WithTimer(engine.S.Clone())); return; }
            SetState(WithTimer(engine.S.ForViewer(PlayerIndexOfSeat(mySeatId))));
            foreach (var kv in clientSeat)
            {
                int idx = PlayerIndexOfSeat(kv.Value);
                net.Send(kv.Key, "state", JsonUtility.ToJson(WithTimer(engine.S.ForViewer(idx))));
            }
        }

        GameState WithTimer(GameState s)
        {
            s.timerLeft = engine.TimeLeft;
            s.timerTotal = engine.TimeTotal;
            return s;
        }

        void SetState(GameState s)
        {
            var prev = state;
            state = s;
            view = View.Game;
            board.SetVisible(true);
            if (board.EnsureBoard(s.board)) hud.FitCamera();
            board.UpdatePieces(s);
            if (PiecePlaced(prev, s)) sfx.Place();
            hud.OnState(prev, s);
        }

        /// <summary>Aynı oyunda yeni bir köy/kasaba/yol konduysa ya da korsan taşındıysa.</summary>
        static bool PiecePlaced(GameState a, GameState b)
        {
            if (a == null || b == null || b.turn < a.turn) return false; // ilk durum ya da yeni oyun
            if (a.vOwner == null || b.vOwner == null || a.vOwner.Length != b.vOwner.Length) return false;
            if (a.vLevel == null || b.vLevel == null || a.vLevel.Length != b.vLevel.Length) return false;
            if (a.eOwner == null || b.eOwner == null || a.eOwner.Length != b.eOwner.Length) return false;
            for (int v = 0; v < b.vOwner.Length; v++)
                if (b.vOwner[v] >= 0 && (a.vOwner[v] != b.vOwner[v] || a.vLevel[v] < b.vLevel[v])) return true;
            for (int e = 0; e < b.eOwner.Length; e++)
                if (b.eOwner[e] >= 0 && a.eOwner[e] != b.eOwner[e]) return true;
            return a.robber >= 0 && b.robber >= 0 && a.robber != b.robber;
        }

        // =====================================================================
        // Oyuncunun hamleleri

        /// <summary>Bu ekranı kullanan oyuncunun indeksi. Tek bilgisayar modunda sırası gelen oyuncudur.</summary>
        public int Me
        {
            get
            {
                if (state == null) return -1;
                if (isLocal)
                {
                    if (state.phase == Phase.Discard)
                        for (int p = 0; p < state.players.Count; p++) if (state.discardPending[p] > 0) return p;
                    return Rules.Actor(state);
                }
                return state.players.FindIndex(p => p.seat == mySeatId);
            }
        }

        // =====================================================================
        // Sohbet: oyun durumundan bağımsız; istemci sunucuya yollar, sunucu adı ve rengi ekleyip herkese dağıtır.

        public const int ChatMax = 140;

        public void SendChat(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || isLocal) return;
            if (text.Length > ChatMax) text = text.Substring(0, ChatMax);
            if (isHost) HostChat(mySeatId, text);
            else net.SendToServer("chat", JsonUtility.ToJson(new TextMsg { text = text }));
        }

        void HostChat(int seatId, string text)
        {
            var seat = lobby.seats.Find(s => s.id == seatId);
            if (seat == null || string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();
            if (text.Length > ChatMax) text = text.Substring(0, ChatMax);
            var line = new ChatLine { name = seat.name, color = seat.color, text = text };
            AddChat(line);
            var json = JsonUtility.ToJson(line);
            foreach (var c in clientSeat.Keys.ToList()) net.Send(c, "chat", json);
        }

        void AddChat(ChatLine line)
        {
            chat.Add(line);
            if (chat.Count > 200) chat.RemoveRange(0, chat.Count - 200);
            chatVersion++;
        }

        public void Send(GameAction a, int asPlayer = -1)
        {
            a.player = asPlayer >= 0 && isLocal ? asPlayer : Me;
            if (isHost)
            {
                if (engine == null) return;
                var err = engine.Apply(a);
                if (err != null) hud.Toast(err, true);
                else BroadcastState();
            }
            else net.SendToServer("act", JsonUtility.ToJson(a));
        }

        // =====================================================================
        // Kural dosyaları

        static string ConfigPath => Path.Combine(Application.persistentDataPath, "kurallar.json");

        public static GameConfig LoadConfigOrDefault()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var c = JsonUtility.FromJson<GameConfig>(File.ReadAllText(ConfigPath));
                    c.Sanitize();
                    return c;
                }
            }
            catch (Exception e) { Debug.LogWarning(e.Message); }
            return GameConfig.Standard();
        }

        public string SaveConfig()
        {
            try
            {
                File.WriteAllText(ConfigPath, JsonUtility.ToJson(lobby.cfg, true));
                return "Kurallar kaydedildi: " + ConfigPath;
            }
            catch (Exception e) { return "Kaydedilemedi: " + e.Message; }
        }

        public string LoadConfig()
        {
            if (!File.Exists(ConfigPath)) return "Kayıtlı kural dosyası yok.";
            SetConfig(LoadConfigOrDefault());
            return "Kurallar yüklendi.";
        }

        public static string ConfigFile => ConfigPath;
    }
}
