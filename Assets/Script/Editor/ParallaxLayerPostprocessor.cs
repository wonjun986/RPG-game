#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Aethoria.EditorTools
{
    // 시차 배경 층(Resources/Backgrounds/Parallax/지역/*.png)을 자동으로 설정한다.
    //  - 1칸 = 135px(1080p 화면에서 원본 크기 그대로 보이는 배율), 아래 가운데 피벗(층을 바닥 기준으로 세운다).
    //  - 하늘(2400px)이 기본 최대 크기(2048)에 걸려 줄어들지 않게 최대 크기를 4096으로 올린다.
    //  - 하늘 그라데이션에 띠가 생기지 않게 고품질 압축, 배경이라 밉맵 없이 부드럽게(Bilinear) 그린다.
    //  - 이어 붙이는 층이라 가장자리 픽셀이 반대편을 끌어오지 않게 Clamp.
    public class ParallaxLayerPostprocessor : AssetPostprocessor
    {
        public const float PixelsPerUnit = 135f;
        private const string WatchedFolder = "Resources/Backgrounds/Parallax/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains(WatchedFolder)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(0.5f, 0f);
            settings.spriteMeshType = SpriteMeshType.FullRect; // 이어 붙일 때 투명 가장자리를 잘라 내지 않게
            importer.SetTextureSettings(settings);
        }
    }
}
#endif
