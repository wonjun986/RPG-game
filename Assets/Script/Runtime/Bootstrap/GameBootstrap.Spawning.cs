using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Data;
using Aethoria.Monsters;
using Aethoria.Quests;
using Aethoria.Save;
using Aethoria.Stages;
using Aethoria.UI;

namespace Aethoria.Bootstrap
{
    // 플레이어/몬스터/보스 생성, 몬스터 데이터, 리듬 대결 상대, 드랍.
    public static partial class GameBootstrap
    {
        private static Character SpawnPlayer()
        {
            var data = ScriptableObject.CreateInstance<CharacterData>();
            data.characterName = "네크로시아";
            data.minLevel = 1;
            data.maxLevel = 50;
            // 데미지 계산식(DamageCalculation.md) 규모: 1레벨 공격력 15 / 방어력 1. 레벨당 +0.5씩 오른다.
            data.baseStats = new StatBlock { attack = 15, magic = 18, hp = 150, agility = 30, defense = 1, mana = 120 };
            data.growthPerLevel = new StatBlock { attack = 0.5f, magic = 0.5f, hp = 5, agility = 0, defense = 0.5f, mana = 2 };

            var go = new GameObject("PlayerCharacter");
            go.AddComponent<Rigidbody2D>();

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.6f, 0.5f);
            collider.offset = new Vector2(0f, 0.25f);

            go.AddComponent<CharacterMovement2D>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            // 네크로시아는 2D 조명의 영향을 받지 않는 기본 재질로 그린다(조명을 넣기 전과 똑같은 색·밝기).
            visual.AddComponent<SpriteRenderer>().sharedMaterial = Aethoria.Fx.FxUtil.SpriteMaterial;
            visual.AddComponent<CharacterPlaceholderVisual>();

            go.AddComponent<WalkBobVisual>();
            go.AddComponent<WalkSpriteAnimator>();
            go.AddComponent<JumpSpriteAnimator>();
            var character = go.AddComponent<Character>();
            character.AssignData(data);
            go.AddComponent<HitReactSpriteAnimator>();
            go.AddComponent<Inventory>();
            go.AddComponent<SkillBook>();
            // 필드에서 직접 싸우는 실시간 전투: 기본 공격 + 스킬(마나/쿨타임은 PlayerHUD에 표시).
            go.AddComponent<CharacterAttack2D>();
            go.AddComponent<CharacterSkillDash>();
            go.AddComponent<CharacterSkillQ>();
            go.AddComponent<CharacterSkillA>();
            go.AddComponent<CharacterSkillS>();
            go.AddComponent<CharacterSkillD>();
            go.AddComponent<CharacterSkillW>();
            go.AddComponent<CharacterSkillE>();
            go.AddComponent<CharacterSkillR>();
            go.AddComponent<CharacterPotionUse>();
            // 방향 전환 스쿼시·점프/착지 먼지·빠른 이동 잔상·발소리, 레벨업 연출.
            go.AddComponent<Aethoria.Fx.PlayerMotionFx>();
            go.AddComponent<AttackAfterimage>(); // 모든 공격·스킬 자세에 잔상
            go.AddComponent<Aethoria.Fx.LevelUpFx>();

            return character;
        }

        // 일반 몬스터 수. 위치는 맵에 들어올 때마다 MonsterSpawner가 무작위로 고른다.
        // 어둠의 숲(1~2스테이지)에는 기사만, 광활한 평야(1~2번째 맵)에는 슬라임만 나온다. 보스 맵에는 일반 몬스터가 없다.
        private const int SlimesPerStage = 4;
        private const int ColossiPerStage = 3;

        private static void SpawnSlimes(Transform parent, int floorY, IEnumerable<float> xOffsets)
        {
            var data = SlimeData();

            foreach (var x in xOffsets)
            {
                var go = new GameObject("Slime");
                go.transform.position = new Vector3(x, floorY, 0f);
                go.transform.SetParent(parent);
                go.AddComponent<SpriteRenderer>();
                go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.3f, 0.55f, 0.95f), 24, 16);

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.9f, 0.6f);
                collider.offset = new Vector2(0f, 0.3f);

                go.AddComponent<Rigidbody2D>();
                go.AddComponent<MonsterWalkSpriteAnimator>().Configure("Art/Monsters/Slime/Walk");
                go.AddComponent<Monster>().AssignData(data);
                go.AddComponent<MonsterAI>();
                go.AddComponent<MonsterHealthBar>();
            }
        }

        // 평야 오른쪽 끝의 필드 보스. 평야에 들어올 때마다 다시 나타난다.
        private static void SpawnBossSlime(Transform parent, int floorY)
        {
            var data = BossSlimeData();

            var go = new GameObject("Boss_Slime");
            go.transform.position = new Vector3(16f, floorY, 0f);
            go.transform.SetParent(parent);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.2f, 0.45f, 0.95f), 48, 32);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(2.6f, 1.8f);
            collider.offset = new Vector2(0f, 0.9f);

            go.AddComponent<Rigidbody2D>();
            go.AddComponent<MonsterWalkSpriteAnimator>().Configure("Art/Monsters/BossSlime/Walk");
            go.AddComponent<MonsterAttackSpriteAnimator>().Configure("Art/Monsters/BossSlime/Attack", 1.4f);
            var monster = go.AddComponent<Monster>();
            monster.AssignData(data);
            go.AddComponent<BossSlimeAI>();

            bossHealthUI.Bind(monster, data.monsterName);
            monster.OnDied += _ => OpenBossExitPortal(PlainsPortalStyle);
        }

        // 잊혀진 폐허의 필드 보스 황금 모래룡(황금 모래룡 애니메이션 스프라이트 시트, 8프레임). 마지막 폐허 맵 오른쪽 끝에서
        // 천천히 다가오다 가까이 붙으면 대지 강타로 앞쪽 바닥을 내려찍는다(SandDragonAI).
        // 잡으면 맵 오른쪽 끝에 마을로 돌아가는 포탈이 열린다.
        private const string SandDragonWalkPath = "Art/Monsters/SandDragon/Walk";
        // 알리아스 걷기는 8프레임에 두 걸음이 들어 있다. 걸음 폭(약 1유닛)을 이동 속도(2.5)로 나눈 0.4초에 한 걸음이 되게 한다.
        private const float AliasWalkFrameRate = 10f;
        private const string SandDragonAttackPath = "Art/Monsters/SandDragon/Attack";
        private const float SandDragonScale = 2f;

        private static SandDragonIntroCutscene SpawnSandDragon(Transform parent, int floorY, int xMax)
        {
            var data = SandDragonData();

            var go = new GameObject("Boss_SandDragon");
            go.transform.position = new Vector3(xMax - 4f, floorY, 0f);
            go.transform.SetParent(parent);
            // 그림(150px = 1칸)대로면 네크로시아와 키가 비슷해서, 오브젝트째 두 배로 키워 네크로시아의 약 두 배로 보이게 한다.
            // 몸통 판정/체력바도 자식·로컬 크기라 함께 커진다(아래 값은 키우기 전 기준).
            go.transform.localScale = new Vector3(SandDragonScale, SandDragonScale, 1f);
            go.AddComponent<SpriteRenderer>().flipX = true; // 원화가 오른쪽을 보므로 처음엔 플레이어 쪽(왼쪽)을 보게
            go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.85f, 0.65f, 0.25f), 56, 40);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(3f, 2.2f);
            collider.offset = new Vector2(0f, 1.1f);

            go.AddComponent<Rigidbody2D>();
            // 모션이 모래 속에서 꿈틀거리는 한 종류뿐이라, 멈춰 있을 때도 같은 프레임을 조금 느리게 돌린다(안 그러면 첫 장에 굳는다).
            var walk = go.AddComponent<MonsterWalkSpriteAnimator>();
            walk.Configure(SandDragonWalkPath);
            walk.ConfigureIdle(SandDragonWalkPath, 6f);
            // 대지 강타(모래룡 대지 강타 연속 장면, 6프레임). 필드 공격 모션이자 리듬 대결의 모래룡 공격 모션이다.
            go.AddComponent<MonsterAttackSpriteAnimator>().Configure(SandDragonAttackPath, 1.2f);
            var monster = go.AddComponent<Monster>();
            monster.AssignData(data);
            var dragonAI = go.AddComponent<SandDragonAI>();
            dragonAI.SetArenaBounds(mapXMin, mapXMax + 1f);

            // 쓰러지면 몸이 모래로 허물어지는 모션(SandDragonDeathCutscene)이 끝난 뒤 맵 끝에 마을로 돌아가는 포탈을 연다.
            go.AddComponent<SandDragonDeathCutscene>().Initialize(() => OpenBossExitPortal(RuinsPortalStyle));
            // 처음엔 땅속에 숨어 있다가, 가까이 오면 등장 연출(땅에서 솟아오름 → 포효 → 네크로시아 대사 → 꼬리로 출구를 부숨 →
            // 네크로시아 대사 → 대시)로 전투가 열린다. 상단 보스 체력바도 모래룡이 모습을 드러낸 뒤에 띄운다.
            // 부술 출구(뒤쪽 포탈)는 이 뒤에 만들어지므로 BuildRuins가 SetExitPortal로 따로 알려 준다.
            var intro = go.AddComponent<SandDragonIntroCutscene>();
            intro.Initialize(dragonAI, player, npcDialogueUI, sandDragonIntroSeen,
                onShown: () => sandDragonIntroSeen = true,
                onReveal: () => bossHealthUI.Bind(monster, data.monsterName));
            return intro;
        }

        // 잊혀진 폐허의 기본 몬스터. 이끼 낀 돌 거인이라 기사보다 크고(약 2.2유닛) 단단하다.
        // groundYAt: x 위치의 지면 높이(입구처럼 경사 지형이 있는 맵에서 땅 속에 박히지 않게).
        // 푸른 바다의 심해 정령(청록빛 심해 정령 애니메이션 시트, 4프레임). 폐허의 콜로서스보다 한 단계 강하다.
        private const string SeaSpiritWalkPath = "Art/Monsters/SeaSpirit/Walk";

        private static void SpawnSeaSpirits(Transform parent, int floorY, IEnumerable<float> xOffsets)
        {
            var data = SeaSpiritData();

            foreach (var x in xOffsets)
            {
                var go = new GameObject("SeaSpirit");
                go.transform.position = new Vector3(x, floorY, 0f);
                go.transform.SetParent(parent);
                go.AddComponent<SpriteRenderer>();
                go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.3f, 0.55f, 0.95f), 36, 40);

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(1.1f, 1.6f);
                collider.offset = new Vector2(0f, 0.8f);

                go.AddComponent<Rigidbody2D>();
                go.AddComponent<MonsterWalkSpriteAnimator>().Configure(SeaSpiritWalkPath);
                go.AddComponent<Monster>().AssignData(data);
                go.AddComponent<MonsterAI>();
                go.AddComponent<MonsterHealthBar>().Configure(newWidth: 1.3f, newVerticalOffset: 0.15f);
            }
        }

        private static void SpawnColossi(Transform parent, System.Func<float, float> groundYAt, IEnumerable<float> xOffsets)
        {
            var data = ColossusData();

            foreach (var x in xOffsets)
            {
                var go = new GameObject("Colossus");
                go.transform.position = new Vector3(x, groundYAt(x), 0f);
                go.transform.SetParent(parent);
                go.AddComponent<SpriteRenderer>();
                go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.6f, 0.6f, 0.55f), 40, 44);

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(1.3f, 2f);
                collider.offset = new Vector2(0f, 1f);

                go.AddComponent<Rigidbody2D>();
                go.AddComponent<MonsterWalkSpriteAnimator>().Configure("Art/Monsters/Colossus/Walk");
                go.AddComponent<MonsterAttackSpriteAnimator>().Configure("Art/Monsters/Colossus/Attack", 0.9f);
                go.AddComponent<Monster>().AssignData(data);
                go.AddComponent<MonsterAI>();
                go.AddComponent<MonsterHealthBar>().Configure(newWidth: 1.4f, newVerticalOffset: 0.15f);
            }
        }

        private static void SpawnKnights(Transform parent, int floorY, IEnumerable<float> xOffsets)
        {
            var data = KnightData();

            foreach (var x in xOffsets)
            {
                var go = new GameObject("Knight");
                go.transform.position = new Vector3(x, floorY, 0f);
                go.transform.SetParent(parent);
                go.AddComponent<SpriteRenderer>();
                go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.85f, 0.3f, 0.3f), 24, 24);

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.6f, 1.3f);
                collider.offset = new Vector2(0f, 0.65f);

                go.AddComponent<Rigidbody2D>();
                go.AddComponent<MonsterWalkSpriteAnimator>();
                go.AddComponent<MonsterAttackSpriteAnimator>().Configure("Art/Monsters/Knight/Attack", 0.6f);
                go.AddComponent<Monster>().AssignData(data);
                go.AddComponent<MonsterAI>();
                go.AddComponent<MonsterHealthBar>();
            }
        }

        // 보스 기사는 맵 오른쪽 끝에서 플레이어 쪽(왼쪽)을 보고 대기 모션으로 서 있다가, 등장 연출(BossIntroCutscene)의
        // 대사가 끝나면 점프 내려찍기로 뛰어든다.
        private static void SpawnBoss(Transform parent, int floorY, int xMax)
        {
            var data = AliasData();

            var go = new GameObject("Boss_Knight");
            go.transform.position = new Vector3(xMax - 1.5f, floorY, 0f);
            go.transform.SetParent(parent);
            go.AddComponent<SpriteRenderer>().flipX = true;
            go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.5f, 0.1f, 0.5f), 48, 72, PlaceholderShape.Humanoid);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.3f, 1.8f);
            collider.offset = new Vector2(0f, 0.9f);

            go.AddComponent<Rigidbody2D>();
            var bossWalkAnimator = go.AddComponent<MonsterWalkSpriteAnimator>();
            bossWalkAnimator.Configure("Art/Monsters/BossKnight/Walk");
            bossWalkAnimator.ConfigureFrameRate(AliasWalkFrameRate);
            bossWalkAnimator.ConfigureIdle("Art/Monsters/BossKnight/Idle", 6f);
            var bossAttackAnimator = go.AddComponent<MonsterAttackSpriteAnimator>();
            bossAttackAnimator.AddClip(BossAI.JumpAttackClip, "Art/Monsters/BossKnight/JumpAttack");
            var monster = go.AddComponent<Monster>();
            monster.AssignData(data);
            var bossAI = go.AddComponent<BossAI>();

            bossHealthUI.Bind(monster, data.monsterName);
            // 보스를 잡으면 월드로 돌아와 유언 → 쓰러지는 모션(BossDeathCutscene)이 끝난 뒤 맵 끝에 마을로 돌아가는 포탈을 연다.
            go.AddComponent<BossDeathCutscene>().Initialize(player, npcDialogueUI, () => OpenBossExitPortal(ForestPortalStyle));

            go.AddComponent<BossIntroCutscene>().Initialize(bossAI, player, npcDialogueUI, skippable: bossIntroSeen,
                onShown: () => bossIntroSeen = true);
            monster.OnDied += _ => TryDropDevilNecklace();
        }

        // 절망의 언덕의 최종보스 루나벨. 걷지 않고 언덕 오른쪽 끝에 서서 플레이어를 바라보며, 중거리에서는 손을 뻗어
        // 피의 손을 날리고, 몸에 닿으면 접촉 피해를 준다(FinalBossAI). 보스 기사보다 단단하고 아프다.
        // 언덕에 들어오면 의자에 앉은 루나벨과 대사를 주고받고(LunabelIntroCutscene), 피의 가시가 플레이어 발밑까지
        // 솟구친 뒤, 루나벨이 일어서서 차원문을 타고 플레이어 앞으로 넘어와 한마디 하고 전투가 시작된다.
        private static LunabelIntroCutscene SpawnLunabel(Transform parent, int floorY, int xMax)
        {
            var data = LunabelData();

            var go = new GameObject("Boss_Lunabel");
            go.transform.position = new Vector3(xMax - 3f, floorY, 0f);
            go.transform.SetParent(parent);
            go.AddComponent<SpriteRenderer>().flipX = true;
            go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.75f, 0.1f, 0.2f), 48, 72, PlaceholderShape.Humanoid);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.3f, 2.2f);
            collider.offset = new Vector2(0f, 1.1f);

            go.AddComponent<Rigidbody2D>();
            // 걷기 모션이 없어서 대기 프레임을 걷기 자리에도 넣는다(걷기 프레임이 비면 애니메이터가 꺼진다). 제자리에 서 있으므로 늘 대기 모션이 돈다.
            var walkAnimator = go.AddComponent<MonsterWalkSpriteAnimator>();
            walkAnimator.Configure(LunabelIdlePath);
            walkAnimator.ConfigureIdle(LunabelIdlePath, 6f);
            go.AddComponent<MonsterAttackSpriteAnimator>().Configure(LunabelAttackPath, 1.0f);
            var monster = go.AddComponent<Monster>();
            monster.AssignData(data);
            var bossAI = go.AddComponent<FinalBossAI>();
            go.AddComponent<LunabelGazeCurse>().Initialize(bossAI, monster, player, npcDialogueUI, lunabelGazeTalkSeen,
                () => lunabelGazeTalkSeen = true);

            // 루나벨을 쓰러뜨리면 필드로 돌아와 패배 모션 → 네크로시아와의 마지막 대화 → 쓰러져 사라진 뒤 마을의 노아 앞으로 돌아간다.
            // 처음 쓰러뜨렸을 때는 마을로 돌아가기 전에 엔딩(PlayEndingThenReturn)이 한 번 나온다.
            go.AddComponent<LunabelDeathCutscene>().Initialize(player, npcDialogueUI, PlayEndingThenReturn);

            var intro = go.AddComponent<LunabelIntroCutscene>();
            intro.Initialize(bossAI, player, npcDialogueUI, skippable: lunabelIntroSeen,
                onShown: () => lunabelIntroSeen = true);
            // 체력바와 이름 자막은 언덕 입구가 아니라 왕좌 앞에서 마주치는 순간(등장 연출 시작) 띄운다.
            intro.OnReveal = () => bossHealthUI.Bind(monster, data.monsterName);
            return intro;
        }

        // 원본 시트(고딕 붉은 여왕 6프레임, 고딕 붉은 마법 공격 시퀀스)는 왼쪽을 보고 있어서, 다른 몬스터처럼
        // 오른쪽을 보도록 좌우 반전해 잘라 두었다. 공격 시퀀스는 검은 배경을 지우고 8프레임으로 나눴다.
        private const string LunabelIdlePath = FinalBossAI.IdlePath;
        private const string LunabelAttackPath = "Art/Monsters/FinalBoss/Phase1/Attack";

        // ---- 몬스터 능력치 ----
        // 필드 스폰과 릴리의 리듬 대결(CreateMinigameOpponent)이 같은 값을 쓰도록 여기 한곳에 모아 둔다.
        // 피해 = (공격력 - 방어력) x 0.7 + 5 (CombatMath). 1레벨 네크로시아(공격력 15)의 기본 공격 한 대는 약 12~15.
        // 일반 몬스터 체력은 실시간 전투 기준(기본 공격 서너 대 남짓)이고, 리듬 대결에서는 MinigameOpponents의
        // 체력(리듬 채보 한두 턴 기준)으로 바꿔 쓴다. 보스는 두 곳 모두 같은 체력을 쓴다.
        private static MonsterData ForestSpiritData()
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "숲의 정령";
            data.level = 1;
            // 튜토리얼 상대. 기본 공격 두세 대면 절반, 그 뒤로는 스킬로만 쓰러진다(TutorialSkillFinish).
            data.maxHp = 40f;
            data.attack = 1f;
            data.defense = 0f;
            data.expReward = 10;
            data.goldReward = 8;
            return data;
        }

        private static MonsterData SlimeData()
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "슬라임";
            data.level = 1;
            // 기본 공격 서너 대. 몸으로 부딪혀 한 번에 약 6.
            data.maxHp = 45f;
            data.attack = 3f;
            data.defense = 1f;
            data.expReward = 10;
            data.goldReward = 8;
            return data;
        }

        private static MonsterData BossSlimeData()
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "보스 슬라임";
            data.level = 6;
            // 몸통 접촉 약 11, 물대포(x1.5) 약 15.
            data.maxHp = 800f;
            data.attack = 10f;
            data.defense = 4f;
            data.expReward = 300;
            data.goldReward = 200;
            return data;
        }

        private static MonsterData SandDragonData()
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "황금 모래룡";
            data.level = 11;
            // 대지 강타(x1.3) 약 19. 3장 순서(알리아스 다음)라 알리아스보다 조금 단단하다.
            data.maxHp = 2000f;
            data.attack = 15f;
            data.defense = 6f;
            data.expReward = 700;
            data.goldReward = 450;
            return data;
        }

        private static MonsterData AquaData()
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "아쿠아";
            data.level = 13;
            // 드릴(x1.2) 약 22, 번개 약 18. 모래룡 다음 바다의 주인이라 한 단계 단단하다.
            data.maxHp = 2400f;
            data.attack = 17f;
            data.defense = 7f;
            data.expReward = 900;
            data.goldReward = 600;
            return data;
        }

        private static MonsterData SeaSpiritData()
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "심해 정령";
            data.level = 5;
            // 몸으로 부딪혀 한 번에 약 11. 폐허의 콜로서스보다 한 단계 강하다.
            data.maxHp = 140f;
            data.attack = 10f;
            data.defense = 6f;
            data.expReward = 45;
            data.goldReward = 32;
            return data;
        }

        private static MonsterData ColossusData()
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "콜로서스";
            data.level = 4;
            // 돌주먹 한 방 약 10. 기사보다 크고 단단하다.
            data.maxHp = 110f;
            data.attack = 8f;
            data.defense = 5f;
            data.expReward = 35;
            data.goldReward = 26;
            return data;
        }

        private static MonsterData KnightData()
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "기사";
            data.level = 1;
            // 칼 한 번 약 8. 슬라임보다 조금 질기다.
            data.maxHp = 60f;
            data.attack = 5f;
            data.defense = 3f;
            data.expReward = 20;
            data.goldReward = 15;
            return data;
        }

        private static MonsterData AliasData()
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "알리아스";
            data.level = 10;
            // 돌진/휩쓸기 3연타 한 대 약 15, 점프 내려찍기(x1.6) 약 24.
            data.maxHp = 1800f;
            data.attack = 15f;
            data.defense = 5f;
            data.expReward = 600;
            data.goldReward = 400;
            return data;
        }

        private static MonsterData LunabelData()
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "루나벨";
            data.level = 15;
            // 몸통 접촉 약 18, 피의 손(x1.4) 약 23. 1페이즈 체력을 다 깎으면 변신해 체력을 모두 채운다(FinalBossAI).
            data.maxHp = 2600f;
            data.attack = 20f;
            data.defense = 7f;
            data.expReward = 1000;
            data.goldReward = 700;
            return data;
        }

        // ---- 릴리의 리듬 대결(미니게임) ----
        // 고를 수 있는 상대와 리듬 대결용 체력(0이면 필드와 같은 체력). 도감에 한 번이라도 기록된(쓰러뜨린) 상대만 고를 수 있다(LilyNpc).
        public static readonly (string name, float rhythmHp)[] MinigameOpponents =
        {
            ("숲의 정령", 100f),
            ("슬라임", 150f),
            ("기사", 170f),
            ("콜로서스", 300f),
            ("심해 정령", 380f),
            ("보스 슬라임", 0f),
            ("알리아스", 0f),
            ("황금 모래룡", 0f),
            ("루나벨", 0f),
        };

        // 리듬 대결 상대를 만든다. 화면 밖 멀리에 보이지 않게, 움직이지 않게 세워 둔다(Monster.IsMinigameOpponent).
        // 전투 화면은 이 몬스터의 대기/공격 프레임과 붙어 있는 보스 AI 종류만 보고 채보와 보스 패턴을 고른다(RhythmBattleUI).
        // 대결이 끝나면 RhythmBattleUI가 지운다.
        public static Monster CreateMinigameOpponent(int index)
        {
            var go = new GameObject("MinigameOpponent");
            go.transform.position = new Vector3(0f, -500f, 0f);
            go.AddComponent<SpriteRenderer>().enabled = false;
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;

            MonsterData data;
            switch (index)
            {
                case 0:
                    data = ForestSpiritData();
                    go.AddComponent<MonsterWalkSpriteAnimator>().Configure(ForestSpiritWalkPath);
                    break;
                case 1:
                    data = SlimeData();
                    go.AddComponent<MonsterWalkSpriteAnimator>().Configure("Art/Monsters/Slime/Walk");
                    break;
                case 2:
                    data = KnightData();
                    go.AddComponent<MonsterWalkSpriteAnimator>();
                    go.AddComponent<MonsterAttackSpriteAnimator>().Configure("Art/Monsters/Knight/Attack", 0.6f);
                    break;
                case 3:
                    data = ColossusData();
                    go.AddComponent<MonsterWalkSpriteAnimator>().Configure("Art/Monsters/Colossus/Walk");
                    go.AddComponent<MonsterAttackSpriteAnimator>().Configure("Art/Monsters/Colossus/Attack", 0.9f);
                    break;
                case 4:
                    data = SeaSpiritData();
                    go.AddComponent<MonsterWalkSpriteAnimator>().Configure(SeaSpiritWalkPath);
                    break;
                case 5:
                    data = BossSlimeData();
                    go.AddComponent<MonsterWalkSpriteAnimator>().Configure("Art/Monsters/BossSlime/Walk");
                    go.AddComponent<MonsterAttackSpriteAnimator>().Configure("Art/Monsters/BossSlime/Attack", 1.4f);
                    break;
                case 6:
                    data = AliasData();
                    var aliasWalk = go.AddComponent<MonsterWalkSpriteAnimator>();
                    aliasWalk.Configure("Art/Monsters/BossKnight/Walk");
                    aliasWalk.ConfigureFrameRate(AliasWalkFrameRate);
                    aliasWalk.ConfigureIdle("Art/Monsters/BossKnight/Idle", 6f);
                    go.AddComponent<MonsterAttackSpriteAnimator>();
                    break;
                case 7:
                    data = SandDragonData();
                    var dragonWalk = go.AddComponent<MonsterWalkSpriteAnimator>();
                    dragonWalk.Configure(SandDragonWalkPath);
                    dragonWalk.ConfigureIdle(SandDragonWalkPath, 6f);
                    go.AddComponent<MonsterAttackSpriteAnimator>().Configure(SandDragonAttackPath, 1.2f);
                    break;
                default:
                    data = LunabelData();
                    var lunabelWalk = go.AddComponent<MonsterWalkSpriteAnimator>();
                    lunabelWalk.Configure(LunabelIdlePath);
                    lunabelWalk.ConfigureIdle(LunabelIdlePath, 6f);
                    go.AddComponent<MonsterAttackSpriteAnimator>().Configure(LunabelAttackPath, 1.0f);
                    break;
            }

            float rhythmHp = index >= 0 && index < MinigameOpponents.Length ? MinigameOpponents[index].rhythmHp : 0f;
            if (rhythmHp > 0f) data.maxHp = rhythmHp;

            var monster = go.AddComponent<Monster>();
            monster.IsMinigameOpponent = true;
            monster.AssignData(data);

            // 보스 AI는 전투 화면이 보스 패턴을 고르는 표시이자 필살기/2페이즈 상태를 들고 있다. 필드 행동은 IsMinigameOpponent로 멈춘다.
            switch (index)
            {
                case 5: go.AddComponent<BossSlimeAI>(); break;
                case 6: go.AddComponent<BossAI>(); break;
                case 7: go.AddComponent<SandDragonAI>(); break;
                case 8: go.AddComponent<FinalBossAI>(); break;
            }
            return monster;
        }

        // 첫 클리어 엔딩: 엔딩이 도는 동안 플레이어를 묶어 두고, 끝나면 저장한 뒤 마을의 노아 앞으로 돌아간다.
        private static void PlayEndingThenReturn()
        {
            if (endingSeen || endingCreditsUI == null || player == null)
            {
                ReturnToVillage(spawnAtNoa: true);
                return;
            }

            var movement = player.GetComponent<CharacterMovement2D>();
            if (movement != null) movement.SetCutsceneLocked(true);
            endingCreditsUI.Play(() =>
            {
                endingSeen = true;
                if (movement != null) movement.SetCutsceneLocked(false);
                ReturnToVillage(spawnAtNoa: true);
                SaveGame();
            });
        }

        // 강화석 드랍: 일반 몬스터는 25% 확률로 1개, 보스(알리아스/보스 슬라임/모래룡/아쿠아/루나벨)는 항상 3개.
        private const float EnhanceStoneDropChance = 0.25f;
        private const int BossEnhanceStoneDrop = 3;
        // 수정구: 보스를 잡을 때마다 항상 3개.
        private const int BossCrystalOrbDrop = 3;
        private const int StarterPotionCount = 3;

        private static void HandleMonsterDiedForDrops(Monster monster)
        {
            if (player == null || player.IsDead || monster == null) return;

            bool isBoss = monster.GetComponent<BossAI>() != null || monster.GetComponent<BossSlimeAI>() != null || monster.GetComponent<SandDragonAI>() != null
                || monster.GetComponent<AquaAI>() != null || monster.GetComponent<FinalBossAI>() != null;
            // 떨어진 아이템 연출(튀어 오름·등급 빛기둥·자석). 보상은 원래대로 바로 가방에 들어간다.
            var monsterCollider = monster.GetComponent<Collider2D>();
            float floorY = monsterCollider != null ? monsterCollider.bounds.min.y : monster.transform.position.y;
            Vector2 dropFrom = (Vector2)monster.transform.position + Vector2.up * 0.6f;
            if (isBoss)
            {
                player.GetComponent<Inventory>().AddItem(ItemDatabase.EnhanceStone, BossEnhanceStoneDrop);
                player.GetComponent<Inventory>().AddItem(ItemDatabase.CrystalOrb, BossCrystalOrbDrop);
                Aethoria.Fx.LootFx.SpawnItem(dropFrom, floorY, ItemDatabase.EnhanceStone, BossEnhanceStoneDrop);
                Aethoria.Fx.LootFx.SpawnItem(dropFrom, floorY, ItemDatabase.CrystalOrb, BossCrystalOrbDrop);
                TryDropBossEquipment(monster);
            }
            else if (Random.value < EnhanceStoneDropChance)
            {
                player.GetComponent<Inventory>().AddItem(ItemDatabase.EnhanceStone);
                Aethoria.Fx.LootFx.SpawnItem(dropFrom, floorY, ItemDatabase.EnhanceStone);
            }
        }

        // 보스 전용 장비: 보스 슬라임/황금 모래룡/아쿠아는 30% 확률, 루나벨은 처음 잡을 때 확정으로 준다.
        // 이미 가진 장비는 다시 주지 않는다(장비는 여러 개 가져도 쓸모가 없다). 알리아스의 악마의 목걸이는 TryDropDevilNecklace.
        // 표시용 목록(Bestiary.cs)도 함께 맞춰 둔다.
        private const float BossEquipmentDropChance = 0.3f;

        private static void TryDropBossEquipment(Monster monster)
        {
            ItemData item = null;
            float chance = BossEquipmentDropChance;
            if (monster.GetComponent<BossSlimeAI>() != null) item = ItemDatabase.SlimeKingRing;
            else if (monster.GetComponent<SandDragonAI>() != null) item = ItemDatabase.GoldenScaleBoots;
            else if (monster.GetComponent<AquaAI>() != null) item = ItemDatabase.AbyssQueenRing;
            else if (monster.GetComponent<FinalBossAI>() != null) { item = ItemDatabase.CrimsonMoonScythe; chance = 1f; }
            if (item == null) return;

            var inventory = player.GetComponent<Inventory>();
            if (inventory.GetCount(item.id) > 0 || Random.value >= chance) return;
            inventory.AddItem(item);
        }

        // 보스 기사를 잡으면 30% 확률로 악마의 목걸이(장식)를 준다.
        private const float DevilNecklaceDropChance = 0.3f;

        private static void TryDropDevilNecklace()
        {
            if (Random.value >= DevilNecklaceDropChance) return;
            if (player == null) return;

            player.GetComponent<Inventory>().AddItem(ItemDatabase.DevilNecklace);
        }
    }
}
