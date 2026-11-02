using UnityEditor;
using UnityEngine;

namespace Aethoria.EditorTools
{
    // 배경 음악(Resources/Audio/Bgm)은 몇 분짜리라 통째로 풀어 메모리에 올리지 않고 재생하면서 읽어 오게(Streaming) 한다.
    public class BgmAudioPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("Resources/Audio/Bgm/")) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
        }
    }
}
