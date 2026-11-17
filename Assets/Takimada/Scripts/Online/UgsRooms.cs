using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Takimada
{
    /// <summary>
    /// Unity Relay üzerinden oda kur / kodla katıl. Port açmaya gerek yoktur.
    /// Bu dosya yalnızca com.unity.services.multiplayer paketi kuruluyken derlenir.
    /// </summary>
    public class UgsRooms : IOnlineRooms
    {
        ISession session;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => OnlineRooms.Provider = new UgsRooms();

        static async Task Ensure()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var opts = new InitializationOptions();
                // Aynı bilgisayarda iki kopya açılabilsin diye her oturum ayrı anonim profil kullanır.
                opts.SetProfile("p" + Random.Range(0, 1000000));
                await UnityServices.InitializeAsync(opts);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public async Task<string> CreateRoom(int maxPlayers)
        {
            await Ensure();
            var options = new SessionOptions { MaxPlayers = maxPlayers, IsPrivate = true }.WithRelayNetwork();
            session = await MultiplayerService.Instance.CreateSessionAsync(options);
            return session.Code;
        }

        public async Task JoinRoom(string code)
        {
            await Ensure();
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
        }

        public async Task Leave()
        {
            if (session == null) return;
            var s = session;
            session = null;
            try { await s.LeaveAsync(); } catch { }
        }
    }
}
