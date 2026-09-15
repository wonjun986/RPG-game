#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Aethoria.EditorTools
{
    // Assets/Resources/Art/... 아래 들어오는 캐릭터 아트를 픽셀아트용 스프라이트로 자동 설정한다.
    public class CharacterArtPostprocessor : AssetPostprocessor
    {
        private const string WatchedFolder = "Assets/Resources/Art/";
        private const float PixelsPerUnit = 240f;

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains(WatchedFolder)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(settings);
        }
    }
}
#endif
