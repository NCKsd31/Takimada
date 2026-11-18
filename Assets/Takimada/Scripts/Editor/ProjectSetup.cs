using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Takimada
{
    /// <summary>
    /// Proje ilk açıldığında boş bir sahne oluşturup build listesine ekler ve oyuncu ayarlarını yapar.
    /// Oyunun kendisi sahneye bağlı değildir; App kendini çalışma anında kurar.
    /// </summary>
    [InitializeOnLoad]
    static class ProjectSetup
    {
        const string ScenePath = "Assets/Takimada/Scenes/Main.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += Run;
        }

        [MenuItem("Satan/Windows build al (zip)")]
        static void BuildWindowsZip()
        {
            if (string.IsNullOrEmpty(CloudProjectSettings.projectId))
                Debug.LogWarning("Satan: proje Unity Cloud'a bağlı değil; bu build'de 'Oda kur' çalışmaz (IP ile bağlanma çalışır).");

            const string dir = "Builds/Satan", zip = "Builds/Satan.zip";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = dir + "/Satan.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError("Satan: build başarısız (" + report.summary.result + "). Console'daki hatalara bak.");
                return;
            }
            // Burst hata ayıklama klasörü oyuncuya gönderilmez
            foreach (var d in Directory.GetDirectories(dir, "*DoNotShip*")) Directory.Delete(d, true);
            if (File.Exists(zip)) File.Delete(zip);
            System.IO.Compression.ZipFile.CreateFromDirectory(dir, zip);
            var full = Path.GetFullPath(zip);
            Debug.Log("Satan: build hazır → " + full + " (" + (new FileInfo(full).Length / (1024 * 1024)) + " MB)");
            EditorUtility.RevealInFinder(full);
        }

        [MenuItem("Satan/Mac build al (zip)")]
        static void BuildMacZip()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
            {
                Debug.LogError("Satan: Mac Build Support modülü kurulu değil. Unity Hub > Installs > bu Unity sürümü > Add modules > " +
                               "'Mac Build Support (Mono)' kur, editörü yeniden başlat.");
                return;
            }
            if (string.IsNullOrEmpty(CloudProjectSettings.projectId))
                Debug.LogWarning("Satan: proje Unity Cloud'a bağlı değil; bu build'de 'Oda kur' çalışmaz (IP ile bağlanma çalışır).");

            // hem Intel hem Apple Silicon Mac'lerde çalışsın (tek dosyada iki mimari)
            EditorUserBuildSettings.SetPlatformSettings("OSXUniversal", "Architecture", "x64ARM64");

            const string dir = "Builds/Satan-Mac", zip = "Builds/Satan-Mac.zip";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = dir + "/Satan.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError("Satan: Mac build başarısız (" + report.summary.result + "). Console'daki hatalara bak.");
                return;
            }
            foreach (var d in Directory.GetDirectories(dir, "*DoNotShip*")) Directory.Delete(d, true);

            // Windows'ta alınan Mac build'i imzasızdır ve karantinaya alınır; Apple Silicon'da imzasız uygulama açılmaz.
            // Bu betik Mac'te bir kez çalıştırılınca (sağ tık > Aç) karantinayı kaldırır, yerel imza atar ve oyunu açar.
            File.WriteAllText(dir + "/ilk-acilis.command",
                "#!/bin/bash\n" +
                "cd \"$(dirname \"$0\")\"\n" +
                "xattr -cr Satan.app\n" +
                "chmod +x Satan.app/Contents/MacOS/*\n" +
                "codesign --force --deep -s - Satan.app\n" +
                "open Satan.app\n");
            File.WriteAllText(dir + "/OKU-BENI.txt",
                "Mac'te ilk açılış:\n" +
                "1) Zip'i aç.\n" +
                "2) 'ilk-acilis.command' dosyasına sağ tıkla > Aç > Aç. (Bir kez yeterli.)\n" +
                "   Oyun açılır. Sonraki seferlerde doğrudan Satan.app'e çift tıkla.\n" +
                "Olmazsa Terminal'de bu klasörde: xattr -cr Satan.app && codesign --force --deep -s - Satan.app\n");

            ZipWithUnixModes(dir, zip);
            var full = Path.GetFullPath(zip);
            Debug.Log("Satan: Mac build hazır → " + full + " (" + (new FileInfo(full).Length / (1024 * 1024)) + " MB)");
            EditorUtility.RevealInFinder(full);
        }

        /// <summary>
        /// Klasörü zip'ler ve her girdiye Unix izinleri yazar. Windows'ta yapılan normal zip'te izin bilgisi olmaz ve
        /// Mac'te oyunun çalıştırılabilir dosyası "çalıştırılamaz" açılır. Satan.app/Contents/MacOS altındakiler ve
        /// .command dosyaları 755, diğerleri 644 olur. Klasör adı zip'in kökünde durur.
        /// </summary>
        static void ZipWithUnixModes(string srcDir, string zipPath)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            string root = Path.GetFullPath(srcDir).TrimEnd('\\', '/');
            string baseName = Path.GetFileName(root);
            using (var fs = new FileStream(zipPath, FileMode.CreateNew))
            using (var za = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
            {
                foreach (var f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    string rel = baseName + "/" + f.Substring(root.Length + 1).Replace('\\', '/');
                    var entry = za.CreateEntry(rel, System.IO.Compression.CompressionLevel.Optimal);
                    entry.LastWriteTime = File.GetLastWriteTime(f);
                    using (var src = File.OpenRead(f))
                    using (var dst = entry.Open()) src.CopyTo(dst);
                }
            }

            // Merkezi dizindeki her girdi: "oluşturan sistem" = Unix (3), dış öznitelikler = izinler << 16.
            // (ZIP biçimi, APPNOTE 4.3.12; Mac'in arşiv aracı izinleri ancak oluşturan sistem Unix ise okur.)
            var b = File.ReadAllBytes(zipPath);
            int eocd = -1;
            for (int i = b.Length - 22; i >= System.Math.Max(0, b.Length - 65557); i--)
                if (b[i] == 0x50 && b[i + 1] == 0x4b && b[i + 2] == 0x05 && b[i + 3] == 0x06) { eocd = i; break; }
            if (eocd < 0) throw new IOException("Zip sonu bulunamadı: " + zipPath);
            int count = System.BitConverter.ToUInt16(b, eocd + 10);
            int off = (int)System.BitConverter.ToUInt32(b, eocd + 16);
            for (int k = 0; k < count; k++)
            {
                if (System.BitConverter.ToUInt32(b, off) != 0x02014b50) throw new IOException("Zip merkezi dizini beklenmedik: " + zipPath);
                int nameLen = System.BitConverter.ToUInt16(b, off + 28);
                int extraLen = System.BitConverter.ToUInt16(b, off + 30);
                int commentLen = System.BitConverter.ToUInt16(b, off + 32);
                string name = System.Text.Encoding.UTF8.GetString(b, off + 46, nameLen);
                bool exec = name.Contains(".app/Contents/MacOS/") || name.EndsWith(".command");
                uint mode = exec ? 0x81EDu : 0x81A4u; // 0100755 : 0100644
                b[off + 5] = 3;
                System.BitConverter.GetBytes(mode << 16).CopyTo(b, off + 38);
                off += 46 + nameLen + extraLen + commentLen;
            }
            File.WriteAllBytes(zipPath, b);
        }

        [MenuItem("Satan/Unity Cloud bağlantısını kontrol et")]
        static void CheckCloud()
        {
            string V(string s) => string.IsNullOrEmpty(s) ? "(BOŞ)" : s;
            Debug.Log("Satan bulut durumu:" +
                "\n  kullanıcı: " + V(CloudProjectSettings.userName) +
                "\n  projectId: " + V(CloudProjectSettings.projectId) +
                "\n  proje adı: " + V(CloudProjectSettings.projectName) +
                "\n  organizasyon: " + V(CloudProjectSettings.organizationName) + " / " + V(CloudProjectSettings.organizationId));
        }

        /// <summary>
        /// Build penceresi: kenarlıksız tam ekran, ekranın kendi çözünürlüğünde (bulanık olmasın);
        /// F11 / Alt+Enter ile boyutu değiştirilebilir pencereye geçilir. Her açılışta zorlanır.
        /// </summary>
        static void ApplyWindowSettings()
        {
            bool changed = false;
            void Set<TV>(TV cur, TV want, System.Action<TV> set) { if (!Equals(cur, want)) { set(want); changed = true; } }
            Set(PlayerSettings.runInBackground, true, v => PlayerSettings.runInBackground = v);
            Set(PlayerSettings.fullScreenMode, FullScreenMode.FullScreenWindow, v => PlayerSettings.fullScreenMode = v);
            Set(PlayerSettings.defaultIsNativeResolution, true, v => PlayerSettings.defaultIsNativeResolution = v);
            Set(PlayerSettings.resizableWindow, true, v => PlayerSettings.resizableWindow = v);
            Set(PlayerSettings.defaultScreenWidth, 1600, v => PlayerSettings.defaultScreenWidth = v);
            Set(PlayerSettings.defaultScreenHeight, 900, v => PlayerSettings.defaultScreenHeight = v);
            Set(PlayerSettings.allowFullscreenSwitch, true, v => PlayerSettings.allowFullscreenSwitch = v);
            if (changed) Debug.Log("Satan: pencere ayarları uygulandı (tam ekran pencere, ekran çözünürlüğü, F11 ile pencere).");
        }

        [MenuItem("Satan/Proje ayarlarını yeniden uygula")]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
                Debug.Log("Satan: ana sahne oluşturuldu → " + ScenePath);
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("Satan: sahne build listesine eklendi.");
            }

            if (PlayerSettings.productName != "Satan")
            {
                PlayerSettings.productName = "Satan";
                PlayerSettings.companyName = "Satan";
            }
            ApplyWindowSettings();

            var active = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
