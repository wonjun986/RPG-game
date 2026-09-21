#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Aethoria.EditorTools
{
    // Assets/Resources/Art/... 아래 들어오는 캐릭터 아트를 픽셀아트용 스프라이트로 자동 설정한다.
    public class CharacterArtPostprocessor : AssetPostprocessor
    {
        private const string WatchedFolder = "Assets/Resources/Art/";
        private const float DefaultPixelsPerUnit = 240f;

        // Attack/ChainSkill은 원본 참고 시트 해상도가 Walk보다 훨씬 커서(캐릭터가 같은 실제 크기라도
        // 픽셀 수가 더 많음), 같은 240을 쓰면 캐릭터가 눈에 띄게 작아 보인다. 걷기 스프라이트 기준
        // 실제 캐릭터 키(약 1.5유닛)에 맞춰 환산한 값(150)을 대신 사용한다.
        // BossKnight는 해상도 보정이 아니라 의도적으로 기본 기사보다 크게 보이도록 같은 값을 재사용한다.
        private static readonly string[] HighResSubfolders =
        {
            "Art/Necrosia/Attack/", "Art/Necrosia/ChainSkill/",
            "Art/Necrosia/WSkill/", "Art/Necrosia/SSkill/",
            "Art/Effects/ScytheSpin/", "Art/Effects/ChainBurst/",
            "Art/Effects/DarkFist/", "Art/Effects/DeathVortex/",
            "Art/Monsters/BossKnight/Walk/", "Art/Monsters/BossKnight/Attack/"
        };
        private const float HighResPixelsPerUnit = 150f;

        private void OnPreprocessTexture()
        {
            string normalizedPath = assetPath.Replace('\\', '/');
            if (!normalizedPath.Contains(WatchedFolder)) return;

            float pixelsPerUnit = DefaultPixelsPerUnit;
            foreach (var subfolder in HighResSubfolders)
            {
                if (normalizedPath.Contains(subfolder))
                {
                    pixelsPerUnit = HighResPixelsPerUnit;
                    break;
                }
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
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
