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
            // 보스 시트들은 시트마다 기사를 그린 크기가 달라서(걷기 키 약 489px, 점프 공격 약 321px),
            // 모두 비슷한 키(약 2.55유닛)로 보이도록 시트별로 환산한 값을 쓴다.
            // 대기(키 약 473px)는 서 있는 모습이 작아 보인다는 피드백으로 걷기보다 조금 큰 약 2.65유닛으로 맞췄다.
            // 기본 공격(새 시트, 선 자세 키 약 311px)은 리듬전투에서 대기 모션과 같은 키로 보이도록 대기에 맞춘다.
            ("Art/Monsters/BossKnight/Walk/", 192f), ("Art/Monsters/BossKnight/Idle/", 178f),
            // 필살기(검 들어올리기/기운 모으기) 프레임은 잘라낼 때 대기 모션과 같은 픽셀 크기로 맞춰 두었다.
            ("Art/Monsters/BossKnight/SpecialRaise/", 178f), ("Art/Monsters/BossKnight/SpecialCharge/", 178f),
            ("Art/Monsters/BossKnight/Die/", 178f),
            ("Art/Monsters/BossKnight/Attack/", 117f), ("Art/Monsters/BossKnight/JumpAttack/", 126f),
            // 루나벨 시트들은 시트마다 그린 크기가 제각각이라(2페이즈/순간이동/패배 장면이 서로 커졌다 작아졌다 했다),
            // 의자에 앉은 모습을 뺀 모든 시트를 얼굴 가운데~부츠 끝 키가 1페이즈 대기와 같은 약 2.08유닛이 되도록 환산했다
            // (시트마다 첫 장의 얼굴 위치와 발끝을 재서 맞춘 값). 아래 각 줄의 옛 설명은 처음 값을 정할 때의 기록이다.
            // 절망의 언덕 최종보스(붉은 여왕) 대기: 후광 끝까지 프레임 키 528px가 약 2.9유닛(보스 기사보다 조금 크게)이 되게 한다.
            ("Art/Monsters/FinalBoss/Idle/", 180f),
            // 첫 조우 때 의자에 앉아 있는 모습(First Encounter with Lunabel, 4프레임): 대기와 같은 180(약 3.4유닛)은
            // 주인공보다 너무 커 보인다는 피드백으로, 의자 꼭대기~받침 620px가 약 2.4유닛이 되게 줄였다.
            ("Art/Monsters/FinalBoss/Seated/", 258f),
            // 손 뻗기 공격 시트는 루나벨을 더 작게 그렸다(키 433px, 대기는 516px). 대기와 같은 키로 보이게 환산한 값.
            ("Art/Monsters/FinalBoss/Attack/", 155f),
            // 2페이즈 변신(Lunabel`s_Phase2, 12프레임 중 9프레임) 시트는 키 500px로, 대기와 같은 키로 환산한 값.
            ("Art/Monsters/FinalBoss/Phase2/", 179f),
            // 2페이즈 대기/필살기/차지 시트는 각성한 느낌이 나도록 변신 마지막 프레임보다 1.2배 큰 키(머리~부츠 약 2.74유닛)로
            // 환산한 값. 공중 대기(Lunabel Aerial Motion)는 크게 그려서 머리~부츠 565px, 차지 322px.
            // 필살기 시트는 차지 시트보다 루나벨을 약 1.13배 크게 그렸다(몸을 겹쳐 맞춰 잰 값). 차지와 같은 크기로 보이게 환산한 값.
            ("Art/Monsters/FinalBoss/Phase2Idle/", 254f),
            ("Art/Monsters/FinalBoss/Special/", 151f), ("Art/Monsters/FinalBoss/Charge/", 152f),
            // 구체 던지기(Lunabel's sphere-throwing motion)는 두 팔을 든 첫 프레임 머리~부츠 393px 기준.
            ("Art/Monsters/FinalBoss/Throw/", 196f),
            // 2페이즈 기본 공격(Lunable phase2 normal Attack)은 팔을 든 첫 프레임 머리~부츠 574px가 2페이즈 대기와 같은 키가 되게 한다.
            ("Art/Monsters/FinalBoss/Phase2Attack/", 237f),
            // 1페이즈 혈류 흡수(뱀파이어 엘프의 혈류 흡수 연속 동작)는 1페이즈 대기와 나란히 놓고 키를 맞춘 값.
            ("Art/Monsters/FinalBoss/Absorb/", 151f),
            // 패배 모션(Lunabel_Defeat, 8프레임): 대기와 같은 키(166)로는 클리어 연출에서 너무 커 보인다는 피드백으로 두 번 줄여,
            // 프레임 키(후광 포함) 481px가 약 1.7유닛 — 네크로시아(대기 1.8)와 비슷한 키가 되게 했다.
            ("Art/Monsters/FinalBoss/Defeat/", 205f), // 다른 시트와 같은 키(167)로 맞췄더니 쓰러지는 모션이 커 보여 약 0.8배로 줄였다.
            // 맞는 모션(Lunabel`s_Hit) 시트는 더 크게 그렸다(키 629px).
            ("Art/Monsters/FinalBoss/Hit/", 232f),
            // 루나벨이 날리는 피의 손(Lunabel's hand launch, 6프레임): 그림 폭 638px가 약 2.2유닛이 되게 한다.
            ("Art/Effects/BloodHand/", 290f),
            // 평야 발판 조각(Yard_scattfolding에서 잘라낸 것)은 폭 210~325px인데, 캐릭터가 여유 있게 올라설 수 있도록
            // 2~3유닛 폭이 되게 한다.
            ("Art/Props/Plains/", 100f),
            // 보스 슬라임은 폭 약 3.5유닛(플레이어 키의 2배 이상)으로 크게 보이게 한다. 공격 시트는 잘라낼 때
            // 왕관 폭 기준으로 이동 시트와 같은 크기로 맞춰 두었으므로 같은 값을 쓴다. 물대포 투사체도 같은 시트에서 나왔다.
            ("Art/Monsters/BossSlime/", 100f), ("Art/Effects/SlimeWaterBall/", 100f),
            // 튜토리얼 숲의 정령(Tutorial_Monster_Move): 뿔~발 약 476px가 약 1.8유닛(플레이어보다 조금 크게)이 되게 한다.
            ("Art/Monsters/ForestSpirit/", 265f),
            // 푸른 바다의 심해 정령: 프레임 키 609px(뿔~꼬리)가 약 2유닛(콜로서스와 비슷한 덩치)이 되게 한다.
            ("Art/Monsters/SeaSpirit/", 300f),
            // 잊혀진 폐허 보스 황금 모래룡: 모래 아랫단~뿔 끝 약 390px가 약 2.6유닛(보스 슬라임보다 크고 알리아스와 비슷한 덩치)이 되게 한다.
            ("Art/Monsters/SandDragon/", 150f),
            // 모래룡 꼬리 대지 폭발: 다 솟은 꼬리(약 650px)가 약 3.2유닛(모래룡 머리보다 조금 높게)이 되게 한다.
            ("Art/Effects/SandTailBurst/", 200f),
            // 노아 대기 프레임도 Jump처럼 캐릭터가 프레임을 거의 꽉 채워서, 150을 쓰면 실제로 거대해진다.
            ("Art/NPC/Noa/Idle/", 400f),
            // 이시스(연습장 NPC)는 갑옷 기사라 플레이어(1.5)보다 조금 큰 약 1.8유닛(프레임 키 약 704px).
            ("Art/NPC/Isis/Idle/", 390f),
            // 대장장이 엘리도 이시스와 비슷한 키(약 1.8유닛, 프레임 키 724px).
            ("Art/NPC/Elly/Idle/", 400f),
            // 엔스룸 릴리: 프레임 키 538px가 약 1.8유닛(LilyNpc가 실제 높이로 다시 맞추지만 기본값도 비슷하게 둔다).
            ("Art/NPC/Lily/Idle/", 300f),
            // 연습장 허수아비: 배경에 그려진 허수아비보다 작아 보이지 않게 약 2.4유닛(프레임 키 약 739px).
            ("Art/Props/Dummy/", 308f),
            // 맵 끝 포탈: 프레임 키 678px가 약 3.4유닛(캐릭터보다 조금 크게)이 되게 한다.
            ("Art/Props/Portal/", 200f),
            // 천상의 계단 성전 포탈: 문틀 키 1448px가 약 3.8유닛(기본 포탈보다 조금 크게)이 되게 한다. 안쪽 소용돌이도 같은 값
            // (Portal.CreateFramedVisual이 두 그림의 PPU를 읽어 크기를 맞추므로 값이 달라도 어긋나지는 않는다).
            ("Art/Props/HeavenPortal/", 380f),
            // 처음 마을 출구 고딕 수정문 포탈: 문틀이 성전 포탈과 같은 1086x1448이라 같은 크기(약 3.8유닛)로 맞춘다.
            ("Art/Props/VillagePortal/", 380f),
            // 잊혀진 폐허 담쟁이 유적 아치 포탈: 문틀이 같은 1086x1448이라 같은 크기로 맞춘다.
            ("Art/Props/RuinsPortal/", 380f),
            // 푸른 바다 청록 수정 석조 아치 포탈: 문틀이 같은 1086x1448이라 같은 크기로 맞춘다.
            ("Art/Props/SeaPortal/", 380f),
            // W스킬 악마의 손: 포탈에서 손끝까지 약 376px가 약 2.2유닛 사거리가 되게 한다.
            ("Art/Effects/DemonHand/", 170f),
            // 모래룡 포효 회오리(여섯 프레임 마법의 모래 회오리): 키 491px가 약 6유닛. SandWhirlwind가 원하는 키로 다시 맞춘다.
            ("Art/Effects/SandWhirlwind/", 80f),
            // 루나벨 차원문 순간이동(붉은 차원문에 흡수되는 마녀): 서 있는 1프레임 머리~부츠 495px가 1페이즈 대기와 같은 키가 되게 한다.
            ("Art/Monsters/FinalBoss/PortalEnter/", 182f),
            // 알리아스 검 꽂기(보랏빛 기사의 검 꽂기 연속 동작): 서 있는 1프레임 키 455px가 대기와 같은 키가 되게 한다.
            ("Art/Monsters/BossKnight/Plant/", 172f),
            // 알리아스 분노 눈 실크(어둠 속 붉은 마력의 실크 웨이브): 길이 580px가 약 1.5유닛.
            ("Art/Effects/AliasEyeSilk/", 380f),
        };

        // 대부분의 캐릭터/이펙트 스프라이트는 바닥에 서 있는 기준(BottomCenter)이 맞지만,
        // 전방으로 뻗어나가는 사슬(ChainThrow)은 캐릭터 손 위치에 왼쪽 끝이 고정된 채
        // 오른쪽으로 길이가 늘어나야 해서 왼쪽 중앙(LeftCenter)을 기준점으로 써야 한다.
        private static readonly string[] LeftCenterPivotFolders =
        {
            "Art/Effects/ChainThrow/",
            // 악마의 손도 포탈(왼쪽 끝)이 고정된 채 손이 앞쪽으로 뻗어나간다. 세로 가운데가 포탈 중심.
            "Art/Effects/DemonHand/",
            // 알리아스 눈 실크: 왼쪽 끝이 눈에 붙고 나머지가 흩날린다.
            "Art/Effects/AliasEyeSilk/",
        };

        // 그 밖에 기준점을 따로 지정해야 하는 폴더.
        // 포탈: 돌 받침대 위 룬 원판 높이(아래에서 약 20%)가 바닥에 닿게 한다.
        // 회전 낫: 프레임을 같은 크기 캔버스 가운데에 맞춰 잘랐으므로, 예전 프레임과 비행 높이가 같도록 약간 올린다.
        private static readonly (string subfolder, Vector2 pivot)[] CustomPivotFolders =
        {
            ("Art/Props/Portal/", new Vector2(0.5f, 0.2f)),
            ("Art/Effects/ScytheSpin/", new Vector2(0.5f, 0.1f)),
            // 루나벨 차원문 순간이동: 칸(512) 안에서 1프레임의 두 부츠 굽 사이 바닥(x 265, y 505)이 발 기준점.
            ("Art/Monsters/FinalBoss/PortalEnter/", new Vector2(265f / 512f, 1f - 505f / 512f)),
            // 붕괴하는 차원문: 프레임마다 소용돌이 한가운데를 캔버스 가운데에 맞춰 잘랐다.
            ("Art/Effects/LunabelPortalCollapse/", new Vector2(0.5f, 0.5f)),
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
