using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Takimada
{
    /// <summary>Online oda sağlayıcısı (Unity Relay). Paket kuruluysa Takimada.Online derlemesi kendini buraya kaydeder.</summary>
    public interface IOnlineRooms
    {
        Task<string> CreateRoom(int maxPlayers);
        Task JoinRoom(string code);
        Task Leave();
    }

    public static class OnlineRooms
    {
        public static IOnlineRooms Provider;
    }

    /// <summary>
    /// Netcode for GameObjects üzerinde ince bir katman. Ağ nesnesi/prefab kullanmaz;
    /// her şey "tk" adlı mesajla, sıkıştırılmış JSON olarak gider.
    /// </summary>
    public class NetSession
    {
        const string Channel = "tk";

        public NetworkManager nm;
        UnityTransport utp;
        bool handlerRegistered;

        public event Action ServerStarted;
        public event Action ConnectedToServer;
        public event Action<string> Disconnected;
        public event Action<ulong> ClientLeft;
        public event Action<ulong, string, string> Message; // gönderen, tür, json

        public bool IsServer => nm != null && nm.IsServer;
        public bool IsRunning => nm != null && (nm.IsServer || nm.IsClient);

        public void Init()
        {
            if (nm != null) return;
            // NetworkManager kök nesne olmak zorunda: başka bir nesnenin altına konursa NGO
            // "nested NetworkManager" hatası verip StartHost/StartClient'ı yarıda keser.
            var go = new GameObject("NetworkManager");
            go.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(go);
            utp = go.AddComponent<UnityTransport>();
            nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = utp,
                ConnectionApproval = false,
                EnableSceneManagement = false,
                ClientConnectionBufferTimeout = 15
            };
            go.SetActive(true);

            nm.OnServerStarted += () =>
            {
                RegisterHandler();
                ServerStarted?.Invoke();
            };
            nm.OnClientConnectedCallback += id =>
            {
                if (nm.IsServer) return;
                if (id != nm.LocalClientId) return;
                RegisterHandler();
                ConnectedToServer?.Invoke();
            };
            nm.OnClientDisconnectCallback += id =>
            {
                if (nm.IsServer)
                {
                    if (id != NetworkManager.ServerClientId) ClientLeft?.Invoke(id);
                    return;
                }
                string reason = string.IsNullOrEmpty(nm.DisconnectReason) ? "Sunucuyla bağlantı kesildi." : nm.DisconnectReason;
                Disconnected?.Invoke(reason);
            };
        }

        void RegisterHandler()
        {
            if (handlerRegistered) return;
            nm.CustomMessagingManager.RegisterNamedMessageHandler(Channel, OnNamedMessage);
            handlerRegistered = true;
        }

        public bool StartHost(ushort port)
        {
            utp.SetConnectionData("0.0.0.0", port, "0.0.0.0");
            return nm.StartHost();
        }

        public bool StartClient(string ip, ushort port)
        {
            utp.SetConnectionData(ip, port);
            return nm.StartClient();
        }

        public void Shutdown()
        {
            if (nm == null) return;
            if (handlerRegistered && nm.CustomMessagingManager != null)
            {
                try { nm.CustomMessagingManager.UnregisterNamedMessageHandler(Channel); } catch { }
            }
            handlerRegistered = false;
            if (nm.IsServer || nm.IsClient) nm.Shutdown();
        }

        public void Kick(ulong client, string reason)
        {
            if (IsServer) nm.DisconnectClient(client, reason);
        }

        // ---------------- Gönderme / alma ----------------

        public void Send(ulong client, string kind, string json)
        {
            if (!IsRunning) return;
            var data = Pack(kind, json);
            using (var w = new FastBufferWriter(data.Length + 16, Allocator.Temp))
            {
                w.WriteValueSafe(data);
                nm.CustomMessagingManager.SendNamedMessage(Channel, client, w, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        public void SendToServer(string kind, string json) => Send(NetworkManager.ServerClientId, kind, json);

        void OnNamedMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte[] data);
            string kind, json;
            try { Unpack(data, out kind, out json); }
            catch (Exception e) { Debug.LogWarning("Satan: bozuk mesaj " + e.Message); return; }
            Message?.Invoke(sender, kind, json);
        }

        static byte[] Pack(string kind, string json)
        {
            var raw = Encoding.UTF8.GetBytes(kind + "\n" + json);
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionMode.Compress, true)) gz.Write(raw, 0, raw.Length);
                return ms.ToArray();
            }
        }

        static void Unpack(byte[] data, out string kind, out string json)
        {
            using (var ms = new MemoryStream(data))
            using (var gz = new GZipStream(ms, CompressionMode.Decompress))
            using (var outMs = new MemoryStream())
            {
                gz.CopyTo(outMs);
                var s = Encoding.UTF8.GetString(outMs.ToArray());
                int nl = s.IndexOf('\n');
                kind = nl < 0 ? s : s.Substring(0, nl);
                json = nl < 0 ? "" : s.Substring(nl + 1);
            }
        }
    }
}
