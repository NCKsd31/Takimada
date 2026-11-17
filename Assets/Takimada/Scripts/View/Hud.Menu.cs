using System.Linq;
using UnityEngine;

namespace Takimada
{
    public partial class Hud
    {
        // =====================================================================
        // Ana menü

        void DrawMenu()
        {
            var r = new Rect(28, 28, 460, sh - 56);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 22, r.y + 18, r.width - 44, r.height - 36));

            GUILayout.Label("SATAN", Sty.title, GUILayout.Height(56));
            GUILayout.Label("Adalarda köy kur, ticaret yap, yolları birleştir. Bütün kurallar lobide ayarlanabilir.", Sty.dim);
            GUILayout.Space(14);

            GUILayout.Label("Kaptan adın", Sty.h2);
            app.playerName = GUILayout.TextField(app.playerName ?? "", 16, Sty.field, GUILayout.Height(34));
            GUILayout.Space(16);

            bool hasOnline = OnlineRooms.Provider != null;
            GUILayout.Label("Online oda (Unity Relay, port açmaya gerek yok)", Sty.h2);
            if (!hasOnline) GUILayout.Label("Online paket derlenmedi. README'deki kurulum adımlarına bak.", Sty.dim);
            GUILayout.BeginHorizontal();
            if (LBtn("Oda kur", hasOnline && !app.busy, Sty.btnAccent, GUILayout.Height(40), GUILayout.Width(150))) app.HostOnline();
            GUILayout.Space(6);
            app.joinCode = GUILayout.TextField(app.joinCode ?? "", 12, Sty.field, GUILayout.Height(40));
            if (LBtn("Kodla katıl", hasOnline && !app.busy, Sty.btn, GUILayout.Height(40), GUILayout.Width(120))) app.JoinOnline();
            GUILayout.EndHorizontal();
            GUILayout.Space(16);

            GUILayout.Label("Doğrudan IP (LAN, Hamachi/Radmin VPN veya port yönlendirme)", Sty.h2);
            GUILayout.BeginHorizontal();
            GUILayout.Label("IP", Sty.label, GUILayout.Width(26));
            app.ip = GUILayout.TextField(app.ip ?? "", 40, Sty.field, GUILayout.Height(34));
            GUILayout.Label("Port", Sty.label, GUILayout.Width(36));
            app.port = GUILayout.TextField(app.port ?? "", 5, Sty.field, GUILayout.Width(70), GUILayout.Height(34));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (LBtn("Sunucu kur", !app.busy && !app.net.IsRunning, Sty.btn, GUILayout.Height(38))) app.HostDirect();
            if (LBtn("Bağlan", !app.busy && !app.net.IsRunning, Sty.btn, GUILayout.Height(38))) app.JoinDirect();
            GUILayout.EndHorizontal();
            if (app.net.IsRunning && !app.net.IsServer)
                if (LBtn("Bağlantıyı iptal et", true, Sty.btnSmall, GUILayout.Height(28))) app.Leave();
            GUILayout.Space(16);

            GUILayout.Label("Aynı bilgisayarda", Sty.h2);
            if (LBtn("Sırayla oyna (tek ekran)", !app.busy, Sty.btn, GUILayout.Height(38))) app.StartLocal();
            GUILayout.Space(14);

            if (!string.IsNullOrEmpty(app.status))
            {
                GUILayout.Label(app.status, Sty.wrap);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("Fare tekerleği: yakınlaş · Sürükle: kaydır · Sağ tık: inşa modundan çık", Sty.dim);
            GUILayout.EndArea();
        }

        // =====================================================================
        // Lobi

        bool confirmLeave;

        void DrawLobby()
        {
            var l = app.lobby;
            var left = new Rect(16, 16, 320, sh - 32);
            Panel(left);
            GUILayout.BeginArea(new Rect(left.x + 16, left.y + 14, left.width - 32, left.height - 28));
            GUILayout.Label(app.isLocal ? "Tek ekran lobisi" : "Lobi", Sty.h1);

            if (app.isOnlineRoom)
            {
                GUILayout.Space(4);
                GUILayout.Label("Oda kodu", Sty.dim);
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.IsNullOrEmpty(l.code) ? "..." : l.code, Sty.Colored(Sty.title, Sty.Brass), GUILayout.Height(52));
                if (!string.IsNullOrEmpty(l.code) && LBtn("Kopyala", true, Sty.btnSmall, GUILayout.Width(80), GUILayout.Height(30)))
                {
                    GUIUtility.systemCopyBuffer = l.code;
                    Toast("Kod panoya kopyalandı.");
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("Arkadaşların menüde bu kodu yazıp 'Kodla katıl'a bassın.", Sty.dim);
            }
            else if (!app.isLocal && app.isHost)
            {
                GUILayout.Label(app.status, Sty.dim);
            }

            GUILayout.Space(10);
            GUILayout.Label("Oyuncular (" + l.seats.Count + "/" + l.cfg.maxPlayers + ")", Sty.h2);
            foreach (var seat in l.seats.ToList())
            {
                GUILayout.BeginHorizontal(GUILayout.Height(34));
                var col = Pal.PlayerColor(seat.color);
                bool mine = seat.id == app.mySeatId || app.isLocal;
                var swatch = GUILayoutUtility.GetRect(28, 28, GUILayout.Width(28), GUILayout.Height(28));
                Sty.Fill(swatch, Pal.Ink);
                Sty.Fill(new Rect(swatch.x + 2, swatch.y + 2, swatch.width - 4, swatch.height - 4), col);
                if (mine && GUI.Button(swatch, GUIContent.none, GUIStyle.none)) app.CycleColor(seat.id);
                GUILayout.Space(6);
                if (app.isLocal)
                    seat.name = GUILayout.TextField(seat.name ?? "", 16, Sty.field, GUILayout.Height(30));
                else
                    GUILayout.Label(seat.name + (seat.id == app.mySeatId ? "  (sen)" : "") + (seat.id == 0 ? "  · kurucu" : ""), Sty.label, GUILayout.Height(30));
                if (app.isHost && seat.id != app.mySeatId)
                    if (LBtn("Çıkar", true, Sty.btnSmall, GUILayout.Width(56), GUILayout.Height(28))) app.RemoveSeat(seat.id);
                GUILayout.EndHorizontal();
            }
            if (app.isLocal)
                if (LBtn("+ Oyuncu ekle", l.seats.Count < l.cfg.maxPlayers, Sty.btnSmall, GUILayout.Height(30))) app.AddLocalSeat();
            GUILayout.Label("Rengini değiştirmek için renk kutusuna tıkla.", Sty.dim);

            GUILayout.FlexibleSpace();
            if (app.isHost)
            {
                bool can = l.seats.Count >= 2 || app.isLocal && l.seats.Count >= 1;
                if (LBtn(can ? "Oyunu başlat" : "En az 2 oyuncu gerekli", can, Sty.btnAccent, GUILayout.Height(46))) app.StartGame();
            }
            else GUILayout.Label("Kurucunun oyunu başlatması bekleniyor...", Sty.wrap);
            GUILayout.Space(6);
            if (!confirmLeave) { if (LBtn("Lobiden ayrıl", true, Sty.btnSmall, GUILayout.Height(30))) confirmLeave = true; }
            else
            {
                GUILayout.BeginHorizontal();
                if (LBtn("Evet, ayrıl", true, Sty.btnSmall, GUILayout.Height(30))) { confirmLeave = false; app.Leave(); }
                if (LBtn("Vazgeç", true, Sty.btnSmall, GUILayout.Height(30))) confirmLeave = false;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();

            var right = new Rect(sw - 600, 16, 584, sh - 32);
            DrawRulesEditor(right, app.isHost && !l.started);
        }
    }
}
