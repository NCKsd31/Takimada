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
                PlayerSettings.runInBackground = true;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth = 1600;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.resizableWindow = true;
                Debug.Log("Satan: oyuncu ayarları uygulandı (pencereli 1600x900, arka planda çalış).");
            }

            var active = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
