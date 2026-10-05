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
            // Jump는 위 설명대로 원본 캔버스가 커서 걷기와 같은 240을 쓰면 실제보다 커 보였는데,
            // 처음 올렸던 340은 지나치게 높아서 오히려 작아 보였고, 그다음 470px 실루엣 기준으로 맞춘
            // 270은 이번엔 던전에서 너무 커 보인다는 피드백을 받았다. 두 값 사이로 다시 낮췄다.
            ("Art/Necrosia/Jump/", 300f),
            // 더블 점프(낫 플립) 프레임은 전용 보정값이 없어 기본값(240)을 그대로 썼는데, 캔버스 대비
            // 캐릭터 실루엣이 작게 그려져 있어서(약 320px) 걷기보다 작아 보였다. 걷기 키에 맞춘 값.
            ("Art/Necrosia/DoubleJump/", 185f),
            ("Art/Necrosia/Attack/", 150f), ("Art/Necrosia/ChainSkill/", 150f),
            ("Art/Necrosia/WSkill/", 150f), ("Art/Necrosia/SSkill/", 150f),
            ("Art/Necrosia/ZSkill/", 150f),
            ("Art/Effects/ScytheSpin/", 150f), ("Art/Effects/ChainBurst/", 150f),
            ("Art/Effects/DarkFist/", 150f), ("Art/Effects/DeathVortex/", 150f),
            ("Art/Effects/ChainThrow/", 150f),
            // E스킬 앉는 동작: 첫 프레임(선 자세) 키 560px가 기준 키 1.5유닛이 되도록 한다. 프레임마다 크기를
            // 보정하지 않으므로(앉으면 키가 줄어야 해서) 폴더 값만으로 크기를 맞춘다.
            ("Art/Necrosia/ESkill/", 373f),
            // E스킬 사슬 폭풍: 회오리(Loop) 키 약 646px가 약 2.6유닛(캐릭터 키의 1.7배)이 되게 한다.
            ("Art/Effects/ChainStormStart/", 250f), ("Art/Effects/ChainStormLoop/", 250f),
            // 보스 시트들은 시트마다 기사를 그린 크기가 달라서(걷기 키 약 489px, 기본 공격 약 355px,
            // 점프 공격 약 321px), 모두 같은 키(약 2.55유닛)로 보이도록 시트별로 환산한 값을 쓴다.
            ("Art/Monsters/BossKnight/Walk/", 192f), ("Art/Monsters/BossKnight/Attack/", 140f),
            ("Art/Monsters/BossKnight/JumpAttack/", 126f),
            // 평야 발판 조각(Yard_scattfolding에서 잘라낸 것)은 폭 210~325px인데, 캐릭터가 여유 있게 올라설 수 있도록
            // 2~3유닛 폭이 되게 한다.
            ("Art/Props/Plains/", 100f),
            // 보스 슬라임은 폭 약 3.5유닛(플레이어 키의 2배 이상)으로 크게 보이게 한다. 공격 시트는 잘라낼 때
            // 왕관 폭 기준으로 이동 시트와 같은 크기로 맞춰 두었으므로 같은 값을 쓴다. 물대포 투사체도 같은 시트에서 나왔다.
            ("Art/Monsters/BossSlime/", 100f), ("Art/Effects/SlimeWaterBall/", 100f),
            // 노아 대기 프레임도 Jump처럼 캐릭터가 프레임을 거의 꽉 채워서, 150을 쓰면 실제로 거대해진다.
            ("Art/NPC/Noa/Idle/", 400f),
            // 이시스(연습장 NPC)는 갑옷 기사라 플레이어(1.5)보다 조금 큰 약 1.8유닛(프레임 키 약 704px).
            ("Art/NPC/Isis/Idle/", 390f),
            // 대장장이 엘리도 이시스와 비슷한 키(약 1.8유닛, 프레임 키 724px).
            ("Art/NPC/Elly/Idle/", 400f),
            // 연습장 허수아비: 배경에 그려진 허수아비보다 작아 보이지 않게 약 2.4유닛(프레임 키 약 739px).
            ("Art/Props/Dummy/", 308f),
            // 맵 끝 포탈: 프레임 키 678px가 약 3.4유닛(캐릭터보다 조금 크게)이 되게 한다.
            ("Art/Props/Portal/", 200f),
            // W스킬 악마의 손: 포탈에서 손끝까지 약 376px가 약 2.2유닛 사거리가 되게 한다.
            ("Art/Effects/DemonHand/", 170f),
        };

        // 대부분의 캐릭터/이펙트 스프라이트는 바닥에 서 있는 기준(BottomCenter)이 맞지만,
        // 전방으로 뻗어나가는 사슬(ChainThrow)은 캐릭터 손 위치에 왼쪽 끝이 고정된 채
        // 오른쪽으로 길이가 늘어나야 해서 왼쪽 중앙(LeftCenter)을 기준점으로 써야 한다.
        private static readonly string[] LeftCenterPivotFolders =
        {
            "Art/Effects/ChainThrow/",
            // 악마의 손도 포탈(왼쪽 끝)이 고정된 채 손이 앞쪽으로 뻗어나간다. 세로 가운데가 포탈 중심.
            "Art/Effects/DemonHand/",
        };

        // 그 밖에 기준점을 따로 지정해야 하는 폴더.
        // 포탈: 돌 받침대 위 룬 원판 높이(아래에서 약 20%)가 바닥에 닿게 한다.
        // 회전 낫: 프레임을 같은 크기 캔버스 가운데에 맞춰 잘랐으므로, 예전 프레임과 비행 높이가 같도록 약간 올린다.
        private static readonly (string subfolder, Vector2 pivot)[] CustomPivotFolders =
        {
            ("Art/Props/Portal/", new Vector2(0.5f, 0.2f)),
            ("Art/Effects/ScytheSpin/", new Vector2(0.5f, 0.1f)),
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

            bool useLeftCenterPivot = false;
            foreach (var subfolder in LeftCenterPivotFolders)
            {
                if (normalizedPath.Contains(subfolder))
                {
                    useLeftCenterPivot = true;
                    break;
                }
            }

            Vector2? customPivot = null;
            foreach (var (subfolder, pivot) in CustomPivotFolders)
            {
                if (normalizedPath.Contains(subfolder))
                {
                    customPivot = pivot;
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
            if (customPivot.HasValue)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = customPivot.Value;
            }
            else if (useLeftCenterPivot)
            {
                settings.spriteAlignment = (int)SpriteAlignment.LeftCenter;
                settings.spritePivot = new Vector2(0f, 0.5f);
            }
            else
            {
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spritePivot = new Vector2(0.5f, 0f);
            }
            importer.SetTextureSettings(settings);
        }
    }
}
#endif
