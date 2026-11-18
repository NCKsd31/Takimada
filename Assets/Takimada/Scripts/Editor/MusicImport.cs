using UnityEditor;
using UnityEngine;

namespace Takimada
{
    /// <summary>
    /// Resources/Music altındaki parçalar akış (streaming) olarak içe aktarılır: birkaç dakikalık şarkı
    /// açılışta belleğe tamamen açılmaz, arka planda yüklenir.
    /// </summary>
    class MusicImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Takimada/Resources/Music/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            ai.defaultSampleSettings = s;
            ai.loadInBackground = true;
        }
    }
}
