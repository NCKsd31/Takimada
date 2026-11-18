using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Takimada
{
    public enum WindowMode { Fullscreen, Borderless, Windowed }

    /// <summary>
    /// Oyuncunun ses, görüntü ve ekran tercihleri. PlayerPrefs'te saklanır; sürgüler anında, ekran ayarları seçilince uygulanır.
    /// </summary>
    public class Settings
    {
        public float master = 0.8f, music = 0.5f, brightness = 1f;
        public bool muted, vsync = true;
        public WindowMode mode = WindowMode.Borderless;
        public int width, height;     // 0 = ekranın kendi çözünürlüğü
        public bool hasDisplay;       // kullanıcı ekran ayarını hiç değiştirdi mi

        public const float MinBright = 0.6f, MaxBright = 1.4f;

        public static Settings Load()
        {
            var s = new Settings
            {
                master = PlayerPrefs.GetFloat("tk.vol", 0.8f),
                music = PlayerPrefs.GetFloat("tk.music", 0.5f),
                muted = PlayerPrefs.GetInt("tk.mute", 0) == 1,
                brightness = Mathf.Clamp(PlayerPrefs.GetFloat("tk.bright", 1f), MinBright, MaxBright),
                vsync = PlayerPrefs.GetInt("tk.vsync", 1) == 1,
                hasDisplay = PlayerPrefs.HasKey("tk.mode"),
                mode = (WindowMode)Mathf.Clamp(PlayerPrefs.GetInt("tk.mode", (int)WindowMode.Borderless), 0, 2),
                width = PlayerPrefs.GetInt("tk.w", 0),
                height = PlayerPrefs.GetInt("tk.h", 0)
            };
            return s;
        }

        public void Save()
        {
            PlayerPrefs.SetFloat("tk.vol", master);
            PlayerPrefs.SetFloat("tk.music", music);
            PlayerPrefs.SetInt("tk.mute", muted ? 1 : 0);
            PlayerPrefs.SetFloat("tk.bright", brightness);
            PlayerPrefs.SetInt("tk.vsync", vsync ? 1 : 0);
            if (hasDisplay)
            {
                PlayerPrefs.SetInt("tk.mode", (int)mode);
                PlayerPrefs.SetInt("tk.w", width);
                PlayerPrefs.SetInt("tk.h", height);
            }
            PlayerPrefs.Save();
        }

        public void ResetDefaults()
        {
            var d = new Settings();
            master = d.master; music = d.music; muted = d.muted; brightness = d.brightness; vsync = d.vsync;
        }

        // ---------------- ses ----------------

        public void ApplyAudio(AudioSource musicSource)
        {
            AudioListener.volume = muted ? 0f : master;
            if (musicSource != null) musicSource.volume = music;
        }

        // ---------------- parlaklık ----------------

        /// <summary>3B sahnenin parlaklığı: shader'lar son rengi (1 + _TkBright) ile çarpar; gökyüzü rengi de aynı oranda.</summary>
        public void ApplyBrightness(Camera cam)
        {
            Shader.SetGlobalFloat("_TkBright", brightness - 1f);
            if (cam != null)
            {
                var c = Pal.Haze * brightness;
                c.a = 1f;
                cam.backgroundColor = c;
            }
        }

        // ---------------- ekran ----------------

        /// <summary>Ekranın desteklediği çözünürlükler (yenileme hızları birleştirilmiş), büyükten küçüğe.</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = Screen.resolutions
                .Select(r => new Vector2Int(r.width, r.height))
                .Where(v => v.x >= 800 && v.y >= 600)
                .Distinct()
                .OrderByDescending(v => v.x * v.y).ThenByDescending(v => v.x)
                .ToList();
            var native = Native();
            if (!list.Contains(native)) list.Insert(0, native);
            return list;
        }

        public static Vector2Int Native()
        {
            var r = Screen.currentResolution;
            return new Vector2Int(Mathf.Max(800, r.width), Mathf.Max(600, r.height));
        }

        /// <summary>Seçili çözünürlük; kaydedilmemişse pencerede şu anki boyut, tam ekranda ekranın kendi çözünürlüğü.</summary>
        public Vector2Int Current()
        {
            if (width > 0 && height > 0) return new Vector2Int(width, height);
            return mode == WindowMode.Windowed ? new Vector2Int(Screen.width, Screen.height) : Native();
        }

        static FullScreenMode ToUnity(WindowMode m)
        {
            switch (m)
            {
                case WindowMode.Fullscreen:
                    return Application.platform == RuntimePlatform.WindowsPlayer ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.FullScreenWindow;
                case WindowMode.Windowed: return FullScreenMode.Windowed;
                default: return FullScreenMode.FullScreenWindow;
            }
        }

        public static WindowMode FromUnity(FullScreenMode m) =>
            m == FullScreenMode.Windowed ? WindowMode.Windowed :
            m == FullScreenMode.ExclusiveFullScreen ? WindowMode.Fullscreen : WindowMode.Borderless;

        public void ApplyVsync() => QualitySettings.vSyncCount = vsync ? 1 : 0;

        public void ApplyDisplay()
        {
            ApplyVsync();
            if (Application.isEditor) return;
            var r = Current();
            Screen.SetResolution(r.x, r.y, ToUnity(mode));
        }
    }
}
