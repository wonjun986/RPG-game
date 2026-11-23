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
            // 네크로시아 이동 모션(대기/걷기/대시/점프/2단 점프)은 예전엔 포즈별 전체 키로 맞췄는데, 포즈마다 키가
            // 달라서(웅크림, 퍼진 머리카락) 모션이 바뀔 때 머리 크기가 들쭉날쭉했다. 지금은 대기(240)의 얼굴 크기를
            // 기준으로 각 시트의 얼굴이 같은 크기가 되게 환산한 값이다. 점프 300 → 230, 2단 점프 185 → 218,
            // 걷기/대시 240 → 258.
            ("Art/Necrosia/Jump/", 230f),
            ("Art/Necrosia/DoubleJump/", 218f),
            ("Art/Necrosia/Walk/", 258f),
            ("Art/Necrosia/Dash/", 258f),
            ("Art/Necrosia/Attack/", 150f), ("Art/Necrosia/ChainSkill/", 150f),
            // 새 기본 공격 연속기(1타 내려치기, 2·3타 X자 베기). 크기는 AttackSpriteAnimator.PlayClip이 선 자세 키로 맞춘다.
            ("Art/Necrosia/AttackDown/", 150f), ("Art/Necrosia/AttackX/", 150f),
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
            // 대기는 서 있는 모습이 작아 보인다는 피드백으로 걷기보다 조금 큰 약 2.57유닛(투구~부츠)으로 맞췄다.
            // 지금 대기(8프레임 다크 나이트 호흡 루프)는 투구~부츠 468px로, 예전 6프레임 대기(520px, 202)와 같은 키가 되게 환산한 값.
            // 걷기(보랏빛 기사 8프레임 보행 스프라이트, 443x421 칸, 두 걸음 한 바퀴)는 투구~부츠 약 406px로,
            // 예전 6프레임 걷기(489px, 192)와 같은 키가 되게 환산한 값.
            ("Art/Monsters/BossKnight/Walk/", 160f), ("Art/Monsters/BossKnight/Idle/", 182f),
            // 필살기(검 들어올리기/기운 모으기) 프레임은 잘라낼 때 대기 모션과 같은 픽셀 크기로 맞춰 두었다.
            ("Art/Monsters/BossKnight/SpecialRaise/", 178f), ("Art/Monsters/BossKnight/SpecialCharge/", 178f),
            ("Art/Monsters/BossKnight/Die/", 178f),
            // 기본 공격(Boss_Knight_Attack, 선 자세 키 약 311px)은 걷기와 같은 키로 보이게 환산한 값.
            // 점프 내려찍기(보라빛 중갑 기사의 검격 연속)는 걷기 시트와 같은 크기로 그려져 있어 같은 값을 쓴다.
            // 점프 시트는 공중 장면도 칸 바닥에 붙어 있어서, 1~4프레임 아래에 빈 칸을 덧대 뛰어오른 높이(최대 260px)를 만들었다.
            ("Art/Monsters/BossKnight/Attack/", 117f), ("Art/Monsters/BossKnight/JumpAttack/", 160f),
            // 루나벨 시트들은 시트마다 그린 크기가 제각각이라(2페이즈/순간이동/패배 장면이 서로 커졌다 작아졌다 했다),
            // 의자에 앉은 모습을 뺀 모든 시트를 얼굴 가운데~부츠 끝 키가 1페이즈 대기와 같은 약 2.08유닛이 되도록 환산했다
            // (시트마다 첫 장의 얼굴 위치와 발끝을 재서 맞춘 값). 아래 각 줄의 옛 설명은 처음 값을 정할 때의 기록이다.
            // 절망의 언덕 최종보스(붉은 여왕) 대기: 후광 끝까지 프레임 키 528px가 약 2.9유닛(보스 기사보다 조금 크게)이 되게 한다.
            ("Art/Monsters/FinalBoss/Phase1/Idle/", 180f),
            // 첫 조우 때 의자에 앉아 있는 모습(First Encounter with Lunabel, 4프레임): 대기와 같은 180(약 3.4유닛)은
            // 주인공보다 너무 커 보인다는 피드백으로, 의자 꼭대기~받침 620px가 약 2.4유닛이 되게 줄였다.
            ("Art/Monsters/FinalBoss/Phase1/Seated/", 258f),
            // 손 뻗기 공격 시트는 루나벨을 더 작게 그렸다(키 433px, 대기는 516px). 대기와 같은 키로 보이게 환산한 값.
            ("Art/Monsters/FinalBoss/Phase1/Attack/", 155f),
            // 2페이즈 변신(Lunabel`s_Phase2, 12프레임 중 9프레임) 시트는 키 500px로, 대기와 같은 키로 환산한 값.
            // 새 변신 18장(웅크림·각성·펼침 세 시트를 발끝·크기 맞춰 이어 붙임): 서 있는 첫 장 머리~부츠 610px가 1페이즈 대기와 같은 약 2.43유닛.
            ("Art/Monsters/FinalBoss/Phase2/Transform/", 251f),
            // 변신 효과: 핏빛 고치(221x329 칸)는 웅크린 몸을 감쌀 만큼(약 2.4유닛), 각성 폭발(256x306)은 날개를 덮을 만큼.
            ("Art/Effects/LunabelTransformCocoon/", 120f), ("Art/Effects/LunabelTransformBurst/", 70f),
            // 2페이즈 대기/필살기/차지 시트는 각성한 느낌이 나도록 변신 마지막 프레임보다 1.2배 큰 키(머리~부츠 약 2.74유닛)로
            // 환산한 값. 공중 대기(Lunabel Aerial Motion)는 크게 그려서 머리~부츠 565px, 차지 322px.
            // 필살기 시트는 차지 시트보다 루나벨을 약 1.13배 크게 그렸다(몸을 겹쳐 맞춰 잰 값). 차지와 같은 크기로 보이게 환산한 값.
            // 2페이즈 대기/기본 공격은 공중에 비스듬히 뜬 포즈라 머리~몸통이 1페이즈보다 작아 보인다는 피드백으로, 둘 다 1.2배 키웠다(254→212, 237→198).
            ("Art/Monsters/FinalBoss/Phase2/Idle/", 212f),
            ("Art/Monsters/FinalBoss/Phase2/Special/", 151f), ("Art/Monsters/FinalBoss/Phase2/Charge/", 152f),
            // 구체 던지기(Lunabel's sphere-throwing motion)는 두 팔을 든 첫 프레임 머리~부츠 393px 기준.
            ("Art/Monsters/FinalBoss/Phase2/Throw/", 196f),
            // 2페이즈 기본 공격(붉은 마법의 팔중 캐릭터 스프라이트, 8프레임 — 원화는 왼쪽을 봐서 좌우 반전해 잘랐다)은
            // 2페이즈 대기와 머리·몸통 크기가 같아 보이는 값. 발밑에 대기와 같은 만큼(약 0.33유닛) 여백을 둬서 공중에 떠 있는 높이도 맞췄다.
            ("Art/Monsters/FinalBoss/Phase2/Attack/", 165f),
            // 1페이즈 혈류 흡수(뱀파이어 엘프의 혈류 흡수 연속 동작)는 1페이즈 대기와 나란히 놓고 키를 맞춘 값.
            ("Art/Monsters/FinalBoss/Phase1/Absorb/", 151f),
            // 패배 모션(Lunabel_Defeat, 8프레임): 대기와 같은 키(166)로는 클리어 연출에서 너무 커 보인다는 피드백으로 두 번 줄여,
            // 프레임 키(후광 포함) 481px가 약 1.7유닛 — 네크로시아(대기 1.8)와 비슷한 키가 되게 했다.
            ("Art/Monsters/FinalBoss/Phase1/Defeat/", 205f), // 다른 시트와 같은 키(167)로 맞췄더니 쓰러지는 모션이 커 보여 약 0.8배로 줄였다.
            // 맞는 모션(Lunabel`s_Hit) 시트는 더 크게 그렸다(키 629px).
            ("Art/Monsters/FinalBoss/Phase1/Hit/", 232f),
            // 루나벨이 날리는 피의 손(Lunabel's hand launch, 6프레임): 그림 폭 638px가 약 2.2유닛이 되게 한다.
            ("Art/Effects/BloodHand/", 290f),
            // 평야 발판 조각(Yard_scattfolding에서 잘라낸 것)은 폭 210~325px인데, 캐릭터가 여유 있게 올라설 수 있도록
            // 2~3유닛 폭이 되게 한다.
            // 평야 발판(이끼 낀 고대 유적 타일셋): 100이면 돌 결이 캐릭터에 비해 너무 커서 어색했다 — 3/4 크기로 줄인 값.
            ("Art/Props/Plains/", 133f),
            // 절망의 언덕 발판(용암 균열 타일셋, 1536x1024 시트에서 잘라 냄): 작은 돌섬 약 1.4칸, 사슬 다리 약 10칸.
            ("Art/Props/Hill/", 130f),
            // 천상의 신전 소품(천상풍 대리석 플랫폼 시트, 1774x887에서 잘라 냄): 폭포 흐르는 떠 있는 섬 약 3.1칸.
            ("Art/Props/Heaven/", 130f),
            // 이리아의 성소 내부 장식(촛대·꽃 화분·수정 구슬 받침·깃발·앞쪽 백합): 놓을 때 높이에 맞춰 늘리므로 밀도는 기준값만.
            ("Art/Props/Sanctuary/", 135f),
            // 어둠의 숲 방 테두리(흙벽 타일·숲길 입구·천장 뿌리·빛기둥·절벽 틈): 원하는 크기로 미리 줄여 두었다(1칸 = 135px, 시차 배경과 같은 밀도).
            ("Art/Props/ForestEdge/", 135f),
            // 평야·폐허·바다 방 테두리(돌벽 타일·천장 덩굴 띠·구멍 모서리·석문·절벽·구덩이): 숲과 같은 1칸 = 135px.
            ("Art/Props/PlainsEdge/", 135f), ("Art/Props/RuinsEdge/", 135f), ("Art/Props/SeaEdge/", 135f),
            // 방 장치(전멸방 철문·철창·부서지는 벽·스위치·벽화·봉인 문·부서진 조각)와 어둠의 숲 장치(썩은 가지·덮개·열쇠·봉인 문·수정).
            ("Art/Props/RoomDevices/", 135f), ("Art/Props/ForestDevices/", 135f),
            // 보물상자(닫힘/열림): 몸통 폭 약 1.4칸으로 미리 줄여 두었다. 두 장 모두 몸통 가운데가 그림 가운데라 피벗(아래 가운데)이 맞는다.
            ("Art/Props/Chest/", 135f),
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
            // 노아 대기(고딕 마녀의 8프레임 윙크, 250x443 칸): 머리~부츠 약 428px가 예전 대기와 같은 약 1.66유닛이 되게 한다.
            ("Art/NPC/Noa/Idle/", 257f),
            // 노아 숨쉬기(윙크 첫 장에서 허리 위만 0~4px 들어 올린 5장): 같은 칸이라 같은 값.
            ("Art/NPC/Noa/Breath/", 257f),
            // 이시스(연습장 NPC, 8프레임 기사 시트, 362x498 칸)는 갑옷 기사라 플레이어(1.5)보다 조금 큰 약 1.8유닛 —
            // 머리 장식~부츠 약 470px 기준. 숨쉬기(Breath 5장)는 첫 장에서 만든 같은 칸이라 같은 값.
            ("Art/NPC/Isis/Idle/", 260f), ("Art/NPC/Isis/Breath/", 260f),
            // 대장장이 엘리(애니 판타지 캐릭터 8프레임, 365x717 칸): 두건~부츠 약 680px가 예전 대기와 같은 약 1.8유닛이 되게 한다.
            // 숨쉬기(Breath 5장)는 첫 장에서 만든 같은 칸이라 같은 값.
            ("Art/NPC/Elly/Idle/", 379f), ("Art/NPC/Elly/Breath/", 379f),
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
            // 엔스룸 마을 하얀 대리석 꽃 아치 포탈: 문틀이 같은 1086x1448이라 같은 크기로 맞춘다.
            ("Art/Props/EnsroomPortal/", 380f),
            // 광활한 평야 이끼 낀 돌 아치 포탈: 문틀이 1159x1356이라, 다른 포탈(1448px → 약 3.8유닛)과 같은 키가 되게 356으로 맞춘다.
            ("Art/Props/PlainsPortal/", 356f),
            // 어둠의 숲 고목 아치 포탈(1159x1358)·이시스의 연습장 나무 문 포탈(1166x1349): 다른 포탈과 같은 키(약 3.8유닛)가 되게 맞춘다.
            ("Art/Props/ForestPortal/", 356f),
            ("Art/Props/PracticePortal/", 354f),
            // W스킬 악마의 손: 포탈에서 손끝까지 약 376px가 약 2.2유닛 사거리가 되게 한다.
            ("Art/Effects/DemonHand/", 170f),
            // 모래룡 포효 회오리(여섯 프레임 마법의 모래 회오리): 키 491px가 약 6유닛. SandWhirlwind가 원하는 키로 다시 맞춘다.
            ("Art/Effects/SandWhirlwind/", 80f),
            // 루나벨 차원문 순간이동(붉은 차원문에 흡수되는 마녀): 서 있는 1프레임 머리~부츠 495px가 1페이즈 대기와 같은 키가 되게 한다.
            ("Art/Monsters/FinalBoss/Phase1/PortalEnter/", 182f),
            // 2페이즈 차원문 순간이동(붉은 소용돌이에 잠식되는 고딕 악마 소녀, 8프레임): 2페이즈 대기와 머리·몸통 크기가 같아 보이는 값.
            ("Art/Monsters/FinalBoss/Phase2/PortalEnter/", 180f),
            // 2페이즈 붕괴하는 소용돌이(4프레임): 첫 장 소용돌이 폭 669px가 빨려 든 마지막 장의 소용돌이 폭(430px, 180)과 같은 크기가 되게.
            ("Art/Effects/LunabelPortalCollapsePhase2/", 280f),
            // 2페이즈 개막의 거대한 에너지 구체(진홍빛 마법 에너지 오브, 6프레임): 가운데 구슬 지름 약 220px가 2.2유닛(배율 1)이 되게.
            ("Art/Effects/LunabelGiantOrb/", 100f),
            // 거대 구체 모으기·터짐·꼬리(새 구체 이펙트): 구체와 같은 크기 기준.
            ("Art/Effects/LunabelOrbCharge/", 100f), ("Art/Effects/LunabelOrbBurst/", 100f), ("Art/Effects/LunabelOrbTrail/", 100f),
            // 알리아스 종막(검의 의식): 마검(키 904px → 약 2.4유닛), 마법진(지름 약 500px → 4유닛, 바닥에 눕혀 쓴다),
            // 사슬 구속(감긴 사슬 기둥 약 300px → 1.7유닛, 네크로시아 몸을 감쌀 만큼), 참격(칼날 길이 약 600px → 2.2유닛).
            ("Art/Effects/AliasRitualSword/", 375f), ("Art/Effects/AliasMagicCircle/", 125f),
            ("Art/Effects/AliasChainBind/", 175f), ("Art/Effects/AliasRitualSlash/", 280f),
            // 아쿠아 흡입 모션(물의 여왕 흡인 소환 애니메이션 시트, 443px 칸): 칸 키가 대기 모션 칸(564px, 240)과 같은 실제 키가 되게 한다.
            // 흡입 깔때기(얼음빛 물 소용돌이)와 가두는 소용돌이(푸른 소용돌이 물마법)는 AquaAI가 원하는 크기로 다시 맞춘다.
            ("Art/Monsters/Aqua/Pull/", 190f),
            // 아쿠아 사망 모션(물거품으로 사라지는 물의 여왕, 443px 칸): 흡입 시트와 같은 칸이라 같은 값으로 대기와 키를 맞춘다.
            ("Art/Monsters/Aqua/Death/", 190f),
            // 알리아스 점프 내려찍기의 어둠의 마력 기둥(6프레임): 다 솟은 기둥 키 약 700px가 약 3.5유닛(알리아스보다 조금 크게)이 되게 한다.
            ("Art/Effects/AliasDarkPillar/", 200f),
            // 알리아스 검 꽂기(보랏빛 기사의 검 꽂기 연속 동작): 서 있는 1프레임 키 455px가 대기와 같은 키가 되게 한다.
            ("Art/Monsters/BossKnight/Plant/", 172f),
            // 알리아스 종막 검 꽂기(Final Pattern): 서 있는 1프레임 키 406px가 Plant 1프레임과 같은 키(약 2.64유닛)가 되게 한다.
            ("Art/Monsters/BossKnight/FinalePlant/", 154f),
            // 종막 검을 꽂은 뒤 무릎 꿇은 대기(6프레임 반복): 갑옷이 FinalePlant 마지막 프레임과 겹치도록 0.76배 크기로 맞춘 값.
            ("Art/Monsters/BossKnight/FinalePlantIdle/", 203f),
            // 알리아스 분노 눈 실크(진홍빛 에너지 궤적, 973x216 칸 8장): 길이 973px가 약 1.9유닛.
            ("Art/Effects/AliasEyeSilk/", 520f),
            // 알리아스 분노 안광(붉은 불꽃 눈 에너지, 471x418 칸 4장): 가운데 가로 플레어 약 330px가 약 0.6유닛.
            ("Art/Effects/AliasEyeGlow/", 550f),
            // 알리아스 각성 폭발(암흑 보랏빛 화염 분출, 306x694 칸 8장): 가장 높이 솟은 불기둥 약 690px가 약 4.5유닛(알리아스 키의 1.7배).
            ("Art/Effects/AliasAwakenBurst/", 155f),
            // 알리아스 흑염 오라(보랏빛 불꽃에 싸인 기사 실루엣, 370x683 칸 6장): 칸 키 683px가 약 3.4유닛(알리아스 키 2.55유닛을 넉넉히 감싸게).
            ("Art/Effects/AliasDarkAura/", 200f),
            // 궁극기 소환: 땅에서 솟는 악마의 문(문틀 키 342px)이 약 6.8유닛(네크로시아 키의 4배 남짓), 날개를 편 악룡은 폭 약 11유닛.
            // (CharacterSkillR이 그릴 때 같은 크기로 다시 맞추므로 이 값이 달라도 게임 속 크기는 같다.)
            ("Art/Effects/UltGate/", 50f), ("Art/Effects/UltDragon/", 50f),
            // Q스킬 차원 참격: 차원 균열(보랏빛 차원 균열 시트, 462x708 칸)은 키 약 1.8유닛(네크로시아보다 조금 크게),
            // 보랏빛 참격(보랏빛 마법 참격 시퀀스, 555x608 칸)은 키 약 1.6유닛.
            ("Art/Effects/VoidRift/", 400f), ("Art/Effects/VoidSlash/", 380f),
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
            // 기본 공격(보랏빛 낫의 연속 베기 자세, 598x364 칸): 1번째 프레임 두 발 한가운데(x 264)와 부츠 바닥(아래에서 13px).
            ("Art/Necrosia/Attack/", new Vector2(264f / 598f, 13f / 364f)),
            // 새 기본 공격 연속기. 1타(494x568 칸)는 6장 모두 뒷발 굽(x 197)과 부츠 밑창을 같은 자리에 맞춰 잘랐고,
            // 기준점은 1번째(선 자세) 두 발 가운데(뒷발 굽보다 43px 앞). X자(302x393 칸)는 8장 모두 앞발 굽(x 216)에 맞췄고,
            // 기준점은 두 발 가운데(앞발 굽보다 55px 뒤). X자 잔상은 프레임마다 X 한가운데를 칸 가운데에 맞췄다.
            ("Art/Necrosia/AttackDown/", new Vector2(240f / 494f, 22f / 568f)),
            ("Art/Necrosia/AttackX/", new Vector2(161f / 302f, 3f / 393f)),
            ("Art/Effects/XSlash/", new Vector2(0.5f, 0.5f)),
            // 달리기(고딕 사신의 질주, 470x326 칸)와 대시(보랏빛 낫을 휘두르는 고딕 질주, 658x340 칸)는 낫이 뒤로 길게 끌려서
            // 캔버스 가운데가 몸보다 한참 뒤쪽이다. 가운데를 기준점으로 쓰면 달리기/대시를 시작하는 순간 몸이 앞으로 튀어 보여서,
            // 엉덩이 아래(얼굴에서 뒤로 약 90px)를 기준점으로, 부츠 바닥을 높이 기준으로 삼는다.
            ("Art/Necrosia/Walk/", new Vector2(330f / 470f, 4f / 326f)),
            ("Art/Necrosia/Dash/", new Vector2(530f / 658f, 25f / 340f)),
            // 수영(보랏빛 낫을 든 고딕 수영 동작, 500x370 칸): 8장 모두 낫 끝을 같은 자리에 맞춰 잘랐다. 누운 몸의 허리(x 270, 위에서 215px)가
            // 바다 추격전의 충돌 상자 가운데(발 기준점 위 0.6유닛)에 오도록, 그보다 0.6유닛(약 194px) 아래 캔버스 밖에 기준점을 둔다.
            ("Art/Necrosia/Swim/", new Vector2(270f / 500f, -39f / 370f)),
            // A스킬(보랏빛 사슬낫 공격, 467x295 칸): 12장 모두 두 발 한가운데(x 176)와 부츠 바닥(아래에서 3px)을 같은 자리에 맞춰 잘랐다.
            ("Art/Necrosia/ASkill/", new Vector2(176f / 467f, 3f / 295f)),
            // D스킬(고딕 마녀의 연속 투척 동작, 303x474 칸): 8장 모두 두 발 한가운데(x 189)와 부츠 바닥(아래에서 2px)을 같은 자리에 맞춰 잘랐다.
            ("Art/Necrosia/DSkill/", new Vector2(189f / 303f, 2f / 474f)),
            // 궁극기 소환(589x374 칸): 네 시트(문 솟음/문 틈 빛/소환/포효)를 문 가운데 첨탑과 땅 높이에 맞춰 한 캔버스에 놓았다.
            ("Art/Effects/UltGate/", new Vector2(290f / 589f, 19f / 374f)),
            ("Art/Effects/UltDragon/", new Vector2(290f / 589f, 19f / 374f)),
            // 루나벨 차원문 순간이동: 칸(512) 안에서 1프레임의 두 부츠 굽 사이 바닥(x 265, y 505)이 발 기준점.
            ("Art/Monsters/FinalBoss/Phase1/PortalEnter/", new Vector2(265f / 512f, 1f - 505f / 512f)),
            // 붕괴하는 차원문: 프레임마다 소용돌이 한가운데를 캔버스 가운데에 맞춰 잘랐다.
            ("Art/Effects/LunabelPortalCollapse/", new Vector2(0.5f, 0.5f)),
            // 2페이즈 차원문 순간이동(463x543 칸): 대기와 반대쪽을 보고 그려져 있어 게임에서는 좌우를 뒤집어 튼다.
            // 뒤집었을 때 1프레임 부츠(x 194, 바닥 3px 위)가 기준점에서 앞쪽 0.25유닛·위 0.34유닛(대기 모습의 부츠 자리)에
            // 오도록, 부츠보다 0.25유닛(45px) 오른쪽에 둔 기준점. 바닥 아래로 내려가 있다.
            ("Art/Monsters/FinalBoss/Phase2/PortalEnter/", new Vector2(239f / 463f, -58f / 543f)),
            ("Art/Effects/LunabelPortalCollapsePhase2/", new Vector2(0.5f, 0.5f)),
            // 거대한 에너지 구체: 프레임마다 하얗게 빛나는 구슬 한가운데를 캔버스 가운데에 맞춰 잘랐다.
            ("Art/Effects/LunabelGiantOrb/", new Vector2(0.5f, 0.5f)),
            ("Art/Effects/LunabelTransformCocoon/", new Vector2(0.5f, 0.5f)),
            ("Art/Effects/LunabelTransformBurst/", new Vector2(0.5f, 0.5f)),
            ("Art/Effects/LunabelOrbCharge/", new Vector2(0.5f, 0.5f)),
            ("Art/Effects/LunabelOrbBurst/", new Vector2(0.5f, 0.5f)),
            // 꼬리: 오른쪽 끝(밝은 머리)이 구체에 붙는다.
            ("Art/Effects/LunabelOrbTrail/", new Vector2(0.93f, 0.5f)),
            // 아쿠아가 플레이어를 가두는 둥근 소용돌이: 프레임마다 소용돌이 눈을 칸 가운데에 맞춰 잘랐다(가운데를 축으로 돌린다).
            ("Art/Effects/AquaVortex/", new Vector2(0.5f, 0.5f)),
            // 공중 회전 베기(346px 정사각 캔버스, 회전 중심이 한가운데): 키 1.76으로 그리면 한가운데가 발 기준점 위 0.75유닛
            // (점프 모션의 몸 한가운데 높이)에 오도록 0.5 - 0.75/1.76.
            ("Art/Necrosia/JumpAttack/", new Vector2(0.5f, 0.5f - 0.75f / 1.76f)),
            // 몬스터 처치 이펙트: 프레임마다 폭발 한가운데를 캔버스 가운데에 맞춰 잘랐다(크기는 MonsterDeathFx가 몸에 맞춘다).
            ("Art/Effects/MonsterDeathBurst/", new Vector2(0.5f, 0.5f)),
            // 루나벨 거대 기둥(진홍빛 혈마법 기둥 소용돌이, 320x548 칸): 프레임마다 바닥 빛의 가운데를 x 160, 위에서 508px에 맞춰 잘랐다.
            // 그 아래 40px은 바닥에 비친 핏물이라 땅에 살짝 겹쳐 보인다.
            ("Art/Effects/LunabelGiantPillarRise/", new Vector2(0.5f, 40f / 548f)),
            ("Art/Effects/LunabelGiantPillarLoop/", new Vector2(0.5f, 40f / 548f)),
            // 알리아스 종막: 마검은 칼끝이 발판에 박히도록 키의 12% 위(칼끝보다 조금 위)가 기준점, 마법진·참격은 가운데,
            // 사슬 구속은 감긴 사슬 기둥의 아래 가운데(칸 556px 중 x 약 265px).
            ("Art/Effects/AliasRitualSword/", new Vector2(0.5f, 0.12f)), ("Art/Effects/AliasMagicCircle/", new Vector2(0.5f, 0.5f)),
            ("Art/Effects/AliasChainBind/", new Vector2(265f / 556f, 0.03f)), ("Art/Effects/AliasRitualSlash/", new Vector2(0.5f, 0.5f)),
            // 종막 검 꽂기: 450x560 칸을 프레임마다 갑옷 무게중심을 가로 가운데에 맞춰 잘랐다. 부츠 바닥(아래에서 24px)이 발 기준점이고,
            // 그 아래로 튀는 돌 부스러기는 땅에 살짝 겹쳐 보인다.
            ("Art/Monsters/BossKnight/FinalePlant/", new Vector2(0.5f, 24f / 560f)),
            // 꽂은 뒤 대기(378x476 칸, 프레임마다 갑옷 위치를 맞춰 잘랐다): FinalePlant 마지막 프레임의 기준점 자리
            // (칼날에서 뒤쪽 62px, 부츠 아래 11px)에 맞춘 점.
            ("Art/Monsters/BossKnight/FinalePlantIdle/", new Vector2(226f / 378f, 18f / 476f)),
            // 차원 균열·보랏빛 참격: 시트에서 프레임마다 그림 한가운데를 칸 가운데에 맞춰 잘랐다.
            ("Art/Effects/VoidRift/", new Vector2(0.5f, 0.5f)), ("Art/Effects/VoidSlash/", new Vector2(0.5f, 0.5f)),
            // 알리아스 눈 실크(973x216 칸): 8장 모두 왼쪽 끝의 밝은 시작점(x 55, 세로 가운데)을 같은 자리에 맞춰 잘랐다. 이 점이 눈에 붙는다.
            ("Art/Effects/AliasEyeSilk/", new Vector2(55f / 973f, 0.5f)),
            // 알리아스 안광(471x418 칸): 4장 모두 가장 밝은 플레어 중심(x 248, 위에서 300px)을 같은 자리에 맞춰 잘랐다.
            ("Art/Effects/AliasEyeGlow/", new Vector2(248f / 471f, 1f - 300f / 418f)),
        };

        // 화면을 꽉 채울 만큼 크게 키워 쓰는 그림은 Point로 두면 픽셀이 네모나게 드러나서 부드럽게 늘린다.
        private static readonly string[] SmoothFilterFolders =
        {
            "Art/Effects/LunabelGiantPillarRise/", "Art/Effects/LunabelGiantPillarLoop/",
            // 몸 크기에 맞춰 줄이거나 키워 쓰는 부드러운 빛·연기라 Point면 테두리가 계단처럼 깨진다.
            "Art/Effects/MonsterDeathBurst/",
            // 알리아스 각성 이펙트: 원본보다 크게 줄여 쓰는 빛·불꽃·연기라 Point면 반짝이며 깨진다.
            "Art/Effects/AliasEyeSilk/", "Art/Effects/AliasEyeGlow/", "Art/Effects/AliasAwakenBurst/", "Art/Effects/AliasDarkAura/",
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
            bool smooth = false;
            foreach (var subfolder in SmoothFilterFolders)
            {
                if (normalizedPath.Contains(subfolder)) smooth = true;
            }
            importer.filterMode = smooth ? FilterMode.Bilinear : FilterMode.Point;
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
