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
        // Jump는 상승/체공 프레임이 머리카락/망토까지 크게 퍼진 포즈라 원본 픽셀 자체가 커서,
        // 걷기와 같은 240을 써도 눈에 띄게 커 보인다. 전용으로 더 높은 값을 써서 걷기 키와 맞춘다.
        private static readonly (string subfolder, float pixelsPerUnit)[] PixelsPerUnitOverrides =
        {
            ("Art/Necrosia/Jump/", 340f),
            ("Art/Necrosia/Attack/", 150f), ("Art/Necrosia/ChainSkill/", 150f),
            ("Art/Necrosia/WSkill/", 150f), ("Art/Necrosia/SSkill/", 150f),
            ("Art/Necrosia/ZSkill/", 150f),
            ("Art/Effects/ScytheSpin/", 150f), ("Art/Effects/ChainBurst/", 150f),
            ("Art/Effects/DarkFist/", 150f), ("Art/Effects/DeathVortex/", 150f),
            ("Art/Monsters/BossKnight/Walk/", 150f), ("Art/Monsters/BossKnight/Attack/", 150f),
            // 노아 대기 프레임도 Jump처럼 캐릭터가 프레임을 거의 꽉 채워서, 150을 쓰면 실제로 거대해진다.
            ("Art/NPC/Noa/Idle/", 400f),
        };

        private void OnPreprocessTexture()
        {
            string normalizedPath = assetPath.Replace('\\', '/');
            if (!normalizedPath.Contains(WatchedFolder)) return;

            float pixelsPerUnit = DefaultPixelsPerUnit;
            foreach (var (subfolder, ppu) in PixelsPerUnitOverrides)
            {
                if (normalizedPath.Contains(subfolder))
                {
                    pixelsPerUnit = ppu;
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
