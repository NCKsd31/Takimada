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

    /// <summary>
    /// Uygulamanın beyni: menü/lobi/oyun akışı, sunucu tarafı oda yönetimi ve hamlelerin yönlendirilmesi.
    /// Sahneye hiçbir şey koymaya gerek yok; oyun açılınca kendini kurar.
    /// </summary>
    public class App : MonoBehaviour
    {
        public const int Protocol = 2;
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

        public NetSession net;
        public BoardView board;
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
            Application.targetFrameRate = 60;
            playerName = PlayerPrefs.GetString("tk.name", "Kaptan" + UnityEngine.Random.Range(10, 99));
            ip = PlayerPrefs.GetString("tk.ip", "127.0.0.1");
            port = PlayerPrefs.GetString("tk.port", "7777");
            lobby.cfg = LoadConfigOrDefault();

            board = new BoardView();
            board.Init(transform);
            board.SetVisible(false);
            hud = new Hud(this);

            net = new NetSession();
            net.Init(transform);
            net.ServerStarted += OnServerStarted;
            net.ConnectedToServer += OnConnectedToServer;
            net.Disconnected += OnDisconnected;
            net.ClientLeft += OnClientLeft;
            net.Message += OnMessage;
        }

        void Update()
        {
            if (isHost && !isLocal && lobbyDirty)
            {
                lobbySendTimer -= Time.unscaledDeltaTime;
                if (lobbySendTimer <= 0) { BroadcastLobby(); }
            }
            hud.Tick();
        }

        void OnGUI() => hud.Draw();

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

        public async void HostOnline()
        {
            if (OnlineRooms.Provider == null) { status = "Online paket yüklü değil (com.unity.services.multiplayer)."; return; }
            SavePrefs();
            isLocal = false; isOnlineRoom = true; busy = true;
            status = "Unity Relay odası kuruluyor...";
            try
            {
                string code = await OnlineRooms.Provider.CreateRoom(Mathf.Clamp(lobby.cfg.maxPlayers, 2, 8));
                lobby.code = code;
                status = "";
                MarkLobbyDirty();
            }
            catch (Exception e)
            {
                status = "Oda kurulamadı: " + e.Message + "\n(Project Settings > Services ile projeyi Unity Cloud'a bağladın mı?)";
                net.Shutdown();
                view = View.Menu;
            }
            busy = false;
        }

        public async void JoinOnline()
        {
            if (OnlineRooms.Provider == null) { status = "Online paket yüklü değil (com.unity.services.multiplayer)."; return; }
            if (string.IsNullOrWhiteSpace(joinCode)) { status = "Oda kodunu yaz."; return; }
            SavePrefs();
            isLocal = false; isHost = false; isOnlineRoom = true; busy = true;
            status = "Odaya katılınıyor...";
            try { await OnlineRooms.Provider.JoinRoom(joinCode); }
            catch (Exception e)
            {
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
            if (isLocal) { SetState(engine.S.Clone()); return; }
            SetState(engine.S.ForViewer(PlayerIndexOfSeat(mySeatId)));
            foreach (var kv in clientSeat)
            {
                int idx = PlayerIndexOfSeat(kv.Value);
                net.Send(kv.Key, "state", JsonUtility.ToJson(engine.S.ForViewer(idx)));
            }
        }

        void SetState(GameState s)
        {
            var prev = state;
            state = s;
            view = View.Game;
            board.SetVisible(true);
            if (board.EnsureBoard(s.board)) hud.FitCamera();
            board.UpdatePieces(s);
            hud.OnState(prev, s);
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
