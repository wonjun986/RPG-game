using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Combat;
using Aethoria.Data;
using Aethoria.Monsters;

namespace Aethoria.UI
{
    // 몬스터가 플레이어에게 근접 접촉하면(MonsterAI/BossAI/BossSlimeAI의 "사거리 안" 분기에서 호출)
    // 실시간 전투 대신 이 화면으로 전환해서 턴제 리듬 전투를 진행한다. 배경(UI/RhythmBattleFrame)
    // 아래쪽에 그려진 두 레인(위/아래)으로 노드가 오른쪽에서 왼쪽으로 날아오고, 판정점에 도착하는
    // 타이밍에 맞는 키(위 레인=Z, 아래 레인=X)를 눌러야 한다 — 적 턴이면 막기, 내 턴이면 공격이 성공한다.
    // 노드는 한 번에 하나씩이 아니라 "채보"(NormalCharts/BossCharts)대로 박자에 맞춰 여러 개가 동시에
    // 화면에 떠서 연달아 붙어 온다 — "위위위 아래..아래.. 위위.위" 같은 리듬감이 나도록.
    // 적 턴(채보 1개) → 내 턴(채보 1개)을 몬스터나 플레이어가 쓰러질 때까지 번갈아 반복한다.
    // 데미지 계산/치명타/보상(경험치·골드·드랍·퀘스트)은 전부 기존 Character.DealDamage / Monster.TakeDamage
    // 파이프라인을 그대로 통과시켜서, 승리 시 별도 처리 없이 기존 OnDied/AnyDied 훅이 다 처리해준다.
    // 패배(플레이어 체력 0)도 마찬가지로 Character.OnDied → DeathReturnUI가 그대로 이어받는다.
    public class RhythmBattleUI : MonoBehaviour
    {
        // 배경 그림(UI/RhythmBattleFrame, 1672x941) 픽셀 좌표계. 좌상단이 원점이고 아래로 갈수록 y가 커진다.
        private const float ImageWidth = 1672f;
        private const float ImageHeight = 941f;
        private const float PxToCanvas = 720f / ImageHeight;

        private const int LaneUp = 0;
        private const int LaneDown = 1;

        // 채보: 한 글자가 한 칸(박자 한 조각)이다. U=위 노드, D=아래 노드, -=쉼.
        // 붙어 있는 글자는 한 칸 간격으로 연달아 오고, '-'가 많을수록 다음 노드까지 텀이 길어진다.
        // 턴마다 하나를 무작위로 고르고, 절반 확률로 위/아래를 뒤집어서 같은 채보도 다르게 느껴지게 한다.
        private static readonly string[] NormalCharts =
        {
            "UUU-D---D---UU-U--D---",
            "D-D-U---UUU-D---D-----",
            "U---U---DD-D--U--UU---",
            "DDD-U---U---D-D---U---",
            "U-D---UU----D-DD--U---",
        };
        // 보스는 칸이 더 짧고(더 빠른 박자) 연타 묶음이 더 길고 촘촘하다.
        private static readonly string[] BossCharts =
        {
            "UUU-D-D-UU-U-DDD-U---UUUU-D---",
            "UDUD--UUU-DD-U-D-DDD--U-U-UU--",
            "DD-DD-UUUU--D-U-D-U---DDD-UUU-",
            "UUU-DDD-UDU-D-DD-UUU--D-D-D---",
        };
        // 한 칸의 길이(초).
        private const float NormalStepDuration = 0.2f;
        private const float BossStepDuration = 0.15f;
        // 데미지는 노드 하나가 한 타다(DamageCalculation.md). 기본 데미지 = (공격력 - 방어력) x 0.7 + 5.
        // 내 공격 노드는 리듬 판정 배수를 곱한다: PERFECT 1.2 / GOOD 1.0 / OK 0.7 / MISS 0.3(놓쳐도 조금은 들어간다).
        // 몬스터 공격은 판정과 무관하게 GOOD(1.0) 고정 — 적 턴에 놓친 노드마다 몬스터의 기본 데미지가 그대로 들어온다.
        private const float PerfectMultiplier = 1.2f;
        private const float GoodMultiplier = 1.0f;
        private const float OkMultiplier = 0.7f;
        private const float MissMultiplier = 0.3f;
        // 전투에서 쓸 수 있는 스킬(필살기). 월드 스킬(CharacterSkillQ/W/D/E/R)과 이름·이펙트·시전 모션을 맞췄다.
        // 기본 공격 채보에서 노드를 맞혀 쌓은 필살 게이지를 GaugeCost만큼 소모한다.
        // 내 턴에 공격 대신 고르면 먼저 그 스킬 전용 채보를 친다(이때는 데미지가 들어가지 않는다). 채보가 끝나면
        // 시전 모션 + 스킬 이펙트 연출이 나오고, 맞힌 노드 수만큼 연타로 스킬 데미지(isSkill)가 들어간다.
        // Power = 같은 전투의 일반 공격 한 턴(전부 Perfect) 대비 총 데미지 배수(채보를 전부 맞혔을 때).
        private readonly struct BattleSkill
        {
            public readonly string Key;
            public readonly string Name;
            public readonly float GaugeCost;
            public readonly float Power;
            public readonly string Chart;
            public readonly string EffectPath;    // 마무리 연출 동안 몬스터 위에서 반복 재생할 이펙트.
            public readonly string CastPath;      // 마무리 연출 때 왼쪽 네크로시아가 한 번 재생하는 시전 모션.
            public readonly float FinisherDuration; // 마무리 연출 길이(초). 이 시간에 걸쳐 연타가 들어간다.
            public BattleSkill(string key, string name, float gaugeCost, float power, string chart, string effectPath,
                string castPath, float finisherDuration)
            {
                Key = key;
                Name = name;
                GaugeCost = gaugeCost;
                Power = power;
                Chart = chart;
                EffectPath = effectPath;
                CastPath = castPath;
                FinisherDuration = finisherDuration;
            }
        }

        private static readonly BattleSkill[] Skills =
        {
            // 묵직한 강타 몇 방 — 노드는 적지만 하나하나가 세다. 월드 Q는 시전 모션이 없어 기본 공격 모션으로 내지른다.
            new BattleSkill("Q", "어둠의 주먹", 40f, 2.0f, "U---U---U---D-------DD--U-------", "Art/Effects/DarkFist",
                "Art/Necrosia/Attack", 1.0f),
            // 빠른 4연타 묶음을 위아래로 몰아친다.
            new BattleSkill("W", "악마의 손", 20f, 1.5f, "UUUU-DDDD-UUUU-DDDD-------", "Art/Effects/DemonHand",
                "Art/Necrosia/WSkill", 1.2f),
            // 던질 때 패턴을 돌아올 때 거꾸로 한 번 더 — 갔다가 돌아오는 부메랑.
            new BattleSkill("D", "낫 부메랑", 30f, 1.75f, "UUD-D-------D-DUU-------UDU-------", "Art/Effects/ScytheSpin",
                "Art/Necrosia/DSkill", 1.1f),
            // 위아래로 쉴 새 없이 번갈아 휘감는 연타.
            new BattleSkill("E", "사슬 폭풍", 65f, 2.75f, "UDUDUD--DUDUDU--UUDDUUDD-------", "Art/Effects/ChainStormLoop",
                "Art/Necrosia/ESkill", 1.4f),
            // 궁극기 — 길고 화려하다. 월드 R은 캐릭터가 사라지는 연출이라, 낫 회전 베기(A) 모션으로 대신한다.
            new BattleSkill("R", "죽음의 소용돌이", 100f, 4.0f, "UUU-DDD-UDUD----UUUU-DDDD-UDU-DUD-UUDD-UUDD--UUUUDDDD-------", "Art/Effects/DeathVortex",
                "Art/Necrosia/ASkill", 1.8f),
        };
        private readonly Sprite[][] skillCastFrames = new Sprite[Skills.Length][];
        // 마무리 연출: 시전 모션이 시작되고 이만큼 뒤에 이펙트가 뜨고 첫 타가 들어간다. 마지막 타 뒤 잠깐 여운을 둔다.
        private const float SkillFinisherLeadIn = 0.2f;
        private const float SkillFinisherTail = 0.35f;
        private const float SkillFailPause = 0.8f;
        private const float SkillEffectFrameInterval = 0.06f;

        // 필살 게이지. 기본 공격 채보에서 노드를 맞힐 때만 찬다(막기·스킬 채보로는 안 찬다).
        // 일반 공격 채보 하나(노드 약 9개)를 전부 Perfect로 치면 약 36 — W는 1턴, R은 3턴쯤 모아야 쓴다.
        // 전투가 시작될 때마다 0부터 다시 쌓는다.
        private const float MaxSkillGauge = 100f;
        private const float GaugePerPerfect = 4f;
        private const float GaugePerGood = 2f;
        private const float GaugePerOk = 1f;

        // 동시에 화면에 떠 있을 수 있는 노드 이미지 수(이동 시간 / 칸 길이 ≈ 5개라 넉넉히).
        private const int NotePoolSize = 16;
        // 그림에서 직접 잰 두 노드(판정점, 레인 트랙의 왼쪽 끝)의 중심 좌표.
        private static readonly Vector2[] NodeCenter = { new Vector2(301f, 664f), new Vector2(301f, 802f) };
        // 트랙이 오른쪽 장식 기둥에 닿기 전까지(약 x=1430) 이어지므로, 그 안쪽에서 노트가 출발한다.
        private const float NoteSpawnX = 1420f;
        private const float NoteSize = 80f; // 캔버스 단위(1280x720 기준).

        // 난이도: 일반 몬스터 / 보스. 몬스터별 전용 채보는 범위 밖이라 이 두 단계만 둔다.
        private const float NormalTelegraphDuration = 1.0f;
        // 판정 범위(±초). PERFECT/GOOD는 한 칸 길이의 절반 정도로 잡는다 — 칸보다 넓으면 옆 노드 타이밍에
        // 눌러도 맞은 걸로 쳐져서 박자를 안 지켜도 다 맞는다. 문서의 OK(±150ms)는 일반 채보 칸(200ms) 기준으로
        // 옆 노드와 너무 겹치지 않게 칸 길이의 70% 안쪽으로 줄였다(일반 ±140ms, 보스 ±105ms).
        private const float NormalBlockWindow = 0.1f;
        private const float NormalPerfectWindow = 0.05f;
        private const float NormalGoodWindow = 0.1f;
        private const float NormalOkWindow = 0.14f;
        private const float BossTelegraphDuration = 0.75f;
        private const float BossBlockWindow = 0.08f;
        private const float BossPerfectWindow = 0.04f;
        private const float BossGoodWindow = 0.08f;
        private const float BossOkWindow = 0.105f;
        // 판정 범위보다 이만큼(초) 더 일찍 누르면 그 노드는 "너무 빨라서" 놓친 것으로 처리된다.
        // 이게 없으면 마구 눌러도 언젠가는 판정 범위 안에 한 번 걸려서 전부 맞는다.
        private const float EarlyMissRange = 0.3f;

        private const float PhaseBannerDuration = 0.9f;
        // 채보의 마지막 노드가 판정된 뒤 다음 턴으로 넘어가기 전 쉬는 시간.
        private const float AfterChartPause = 0.5f;
        // 공격 모션(0.5초)이 타격 순간까지 진행된 뒤에 데미지가 들어가게 한다(일격사여도 모션이 먼저 보이게).
        private const float AttackImpactDelay = 0.25f;

        // 도주하면 그 몬스터는 이 시간 동안 깜빡이며, 닿아도 전투가 걸리지 않고 몸도 통과된다.
        private const float FleeCooldown = 3f;
        private const float FleeKnockbackDistance = 1.5f;
        private const float FleeBlinkInterval = 0.1f;

        // 접촉 → 암전 → "BATTLE!!" 연출 → 실제 전투 UI가 드러나는 도입부 타이밍.
        private const float BlackoutFadeDuration = 0.2f;
        private const float BattleSplashFrameInterval = 0.07f;
        private const float PostSplashPause = 0.15f;

        private static readonly Color PerfectColor = new Color(0.55f, 1f, 0.6f);
        private static readonly Color GoodColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color OkColor = new Color(1f, 0.62f, 0.3f);
        private static readonly Color MissColor = new Color(1f, 0.45f, 0.45f);
        private static readonly Color EnemyNoteColor = new Color(1f, 0.35f, 0.35f, 0.95f);
        private static readonly Color PlayerNoteColor = new Color(0.75f, 0.45f, 1f, 0.95f);
        private static readonly Color SkillNoteColor = new Color(1f, 0.8f, 0.3f, 0.95f);

        // 행동 선택 창(UI/BattleActionSelect)의 "공격/스킬/아이템/도주" 4칸 아이콘 중심 x(패널 안쪽 0~1 비율).
        private const int ActionAttack = 0;
        private const int ActionSkill = 1;
        private const int ActionItem = 2;
        private const int ActionFlee = 3;
        private static readonly float[] ActionSlotX = { 0.21875f, 0.40625f, 0.59375f, 0.78125f };

        // 노드를 성공적으로 맞혔을 때(막기 성공/공격 적중 모두) 그 자리에서 터지는 연출의 프레임 수/속도.
        private const float NodeBurstFrameInterval = 0.04f;

        private readonly struct HpBarRefs
        {
            public readonly Image Fill;
            public readonly Text NameLabel;
            public readonly Text ValueLabel;
            public HpBarRefs(Image fill, Text nameLabel, Text valueLabel)
            {
                Fill = fill;
                NameLabel = nameLabel;
                ValueLabel = valueLabel;
            }
        }

        private GameObject panel;
        private Image dimBackdrop;
        private GameObject contentRoot; // 프레임/체력바/초상화/레인 — 암전+BATTLE 연출이 끝난 뒤에 드러난다.
        private Image battleSplashImage;
        private Sprite[] battleSplashFrames = System.Array.Empty<Sprite>();
        private Image playerPortrait;
        private Image monsterPortrait;
        private Image monsterHpFill;
        private Text monsterNameLabel;
        private Text monsterHpText;
        private Image playerHpFill;
        private Text playerHpText;
        private Text phaseBannerText;
        private Text resultText;

        // 판정 로고(UI/Node_LoGo에서 잘라낸 GOOD/PERFECT/EXELLENT/MISS). resultText와 같은 자리에 뜬다.
        private const float JudgmentDuration = 0.5f;
        private const float JudgmentPopTime = 0.08f;
        private const float JudgmentPopScale = 1.35f;
        private Image judgmentImage;
        private Coroutine judgmentRoutine;
        private Sprite judgmentPerfect;
        private Sprite judgmentExcellent;
        private Sprite judgmentGood;
        private Sprite judgmentMiss;
        private readonly Image[] notePool = new Image[NotePoolSize];
        private readonly Text[] exclaim = new Text[2];

        // 채보를 재생하는 동안 화면에 떠 있는(또는 곧 뜰) 노드 하나.
        private class ChartNote
        {
            public int Lane;
            public float HitTime;   // 채보 시작부터 이 노드가 판정점에 닿는 시각(초).
            public Image Image;     // 화면에 떠 있는 동안만 풀에서 빌려온다.
            public bool Judged;
        }

        // 내 턴 시작 시 한 번 뜨는 행동 선택 창(공격/스킬/아이템/도주 중 고르고 Z/Enter로 확정).
        // 아이템을 고르면 회복 포션(ItemDatabase.Potion)을 하나 마셔 체력을 회복하고, 공격 없이 그 턴이 끝난다.
        // 도주를 고르면 도망 처리(쿨다운+넉백)로 이어진다. 전투 중 도망은 이 도주로만 할 수 있다(Esc로는 나가지 않는다).
        private GameObject actionSelectPanel;
        // 내 턴 동안(행동 선택 + 스킬 목록) 뒤에 깔리는 테두리 창(UI/BattleMyTurn)과 위쪽 띠의 제목.
        private GameObject myTurnFrame;
        private Text myTurnTitleText;
        private RectTransform actionCursor;
        private RawImage actionCursorImage;
        private Text actionMessageText;
        private int actionSelectIndex;
        private bool actionSelectFlee;
        private bool actionSelectPotion;
        private const float PotionResultDuration = 0.9f;

        // 행동 선택 창에서 "스킬"을 고르면 뜨는 스킬 목록. ↑/↓로 고르고 Z/Enter로 확정, X/Backspace로 돌아간다.
        private GameObject skillSelectPanel;
        private readonly Text[] skillRowTexts = new Text[Skills.Length];
        private Text skillMessageText;
        private Image skillGaugeFill;
        private Text skillGaugeText;
        private float skillGauge;
        private int skillSelectIndex;
        private int chosenSkill = -1; // 이번 턴에 쓸 스킬. -1이면 일반 공격.

        // 스킬 채보가 끝난 뒤 마무리 연출 동안 몬스터 초상화 위에서 반복 재생하는 스킬 이펙트.
        private Image skillEffectImage;
        private readonly Sprite[][] skillEffectFrames = new Sprite[Skills.Length][];
        private Coroutine skillEffectRoutine;

        // 노드 적중 시 터지는 연출(공격 성공/막기 성공 공통으로 재사용).
        private Image nodeBurstImage;
        private Sprite[] nodeBurstFrames = System.Array.Empty<Sprite>();

        // 플레이어 대기 애니메이션(왼쪽 초상화). 월드의 WalkSpriteAnimator와 같은 경로/속도(4fps)를 쓰되,
        // 전투 화면 전용으로 따로 돌린다 — 실제 플레이어가 그 순간 어떤 포즈로 멈췄는지와 무관하게
        // 항상 매끈하게 반복 재생된다.
        private const string PlayerIdlePath = "Art/Necrosia/Idle";
        private const float IdleFrameInterval = 1f / 4f;
        private Sprite[] playerIdleFrames = System.Array.Empty<Sprite>();
        private int idleFrameIndex;
        private float idleFrameTimer;

        // 공격 노드를 맞혔을 때 잠깐 재생하는 Z 공격 모션(월드의 AttackSpriteAnimator와 같은 소스/길이).
        // 재생 중에는 대기 애니메이션 쪽 Update 루프를 잠깐 쉬게 한다.
        private const string PlayerAttackPath = "Art/Necrosia/Attack";
        private const float PlayerAttackAnimDuration = 0.5f;
        private Sprite[] playerAttackFrames = System.Array.Empty<Sprite>();
        private bool isPlayingAttackAnim;
        private Coroutine attackAnimRoutine;

        // 적 턴에 노드를 놓쳐 맞았을 때 재생하는 피격(스태거) 모션(월드의 HitReactSpriteAnimator와 같은 소스).
        // 공격 모션과 같은 자리(attackAnimRoutine)를 써서 둘이 겹치면 나중 것이 앞의 것을 끊는다.
        // 맞는 동안 초상화를 붉게 물들였다가 원래 색으로 되돌린다.
        private const string PlayerHitPath = "Art/Necrosia/Hit";
        private const float PlayerHitAnimDuration = 0.4f;
        private static readonly Color PlayerHitTint = new Color(1f, 0.45f, 0.45f);
        private Sprite[] playerHitFrames = System.Array.Empty<Sprite>();

        // 적 턴에 노드를 막았을 때 재생하는 가드 모션(낫을 세워 막고 보랏빛 방패가 번쩍인다).
        private const string PlayerGuardPath = "Art/Necrosia/Guard";
        private const float PlayerGuardAnimDuration = 0.36f;
        private Sprite[] playerGuardFrames = System.Array.Empty<Sprite>();

        // 몬스터(오른쪽) 애니메이션. 월드에서 쓰던 스프라이트를 그대로 가져온다 — 대기는 이동 프레임
        // (MonsterWalkSpriteAnimator)을 반복하고, 적 턴에 노드가 판정점에 닿으면 공격 프레임
        // (MonsterAttackSpriteAnimator)을 한 번 재생한다. 허수아비는 맞을 때 흔들리는 프레임(TrainingDummy)을 쓴다.
        // 프레임마다 그림 크기가 달라서(아래 중앙 기준점), 월드에서처럼 "픽셀 / PPU" 크기로 발밑을 맞춰 그린다.
        private const float MonsterIdleFrameInterval = 1f / 6f;
        private const float MonsterAttackFrameInterval = 0.07f;
        private const float MonsterHitFrameInterval = 0.06f;
        private RectTransform monsterStage; // 몬스터를 세울 칸. 이 칸의 아래쪽 가운데가 발밑이다.
        private Sprite[] monsterIdleFrames = System.Array.Empty<Sprite>();
        private Sprite[] monsterAttackFrames = System.Array.Empty<Sprite>();
        private Sprite[] monsterHitFrames = System.Array.Empty<Sprite>();

        // 맞을 때 몬스터가 붉게 두 번 깜빡인다.
        private static readonly Color MonsterHitFlashColor = new Color(1f, 0.25f, 0.25f);
        private const float MonsterHitFlashOn = 0.07f;
        private const float MonsterHitFlashOff = 0.05f;
        private Coroutine monsterFlashRoutine;

        // 일반 공격 노드가 적중할 때마다 몬스터 위에 한 번 재생하는 베기 이펙트(UI/Attack_UI에서 잘라낸 8프레임).
        private const float BasicSlashFrameInterval = 0.035f;
        private Sprite[] basicSlashFrames = System.Array.Empty<Sprite>();
        private Image basicSlashImage;
        private Coroutine basicSlashRoutine;

        // 몬스터 위로 떠오르는 데미지 숫자(UI/Damage_Number에서 잘라낸 0~9). 치명타는 베인 자국이 있는 숫자를
        // 크게, 놓친 노드(MISS 배수)는 일반 숫자를 작고 흐리게 띄운다.
        private const float DamageDigitHeight = 64f;
        private const float CritDigitHeight = 86f;
        private const float MissDigitHeight = 46f;
        private const float DamageDigitOverlap = 0.18f; // 숫자 둘레의 가시 장식끼리 살짝 겹치게 붙인다
        private const float DamageNumberLifetime = 0.8f;
        private const float DamageNumberRise = 70f;
        private readonly Sprite[] normalDigits = new Sprite[10];
        private readonly Sprite[] critDigits = new Sprite[10];
        private RectTransform damageNumberLayer;
        private bool nextHitIsMiss;
        private Sprite[] monsterOneShot; // 재생 중인 공격/피격 모션. null이면 대기 반복.
        private float monsterOneShotInterval;
        private int monsterFrameIndex;
        private float monsterFrameTimer;
        private Vector2 monsterBaseWorldSize = Vector2.one; // 대기 첫 프레임의 월드 크기. 이게 칸에 꽉 차게 맞춘다.

        private Monster monster;
        private Character player;
        private CharacterMovement2D playerMovement;
        private Coroutine turnRoutine;

        private bool isBossBattle;
        // 연습장 허수아비와의 연습 전투. 허수아비는 맞아도 체력이 다시 차고(TrainingDummy), 플레이어도
        // 연습장에서는 체력이 계속 가득 차므로(PracticeRoomRules) 끝나지 않는다 — 도주로 나간다.
        // 스킬을 마음껏 시험해 보도록 내 턴마다 필살 게이지를 가득 채우고, 몬스터 체력 대신 누적 피해를 보여준다.
        private bool isPractice;
        private float practiceDamageTotal;
        private float telegraphDuration;
        private float blockWindow;
        private float perfectWindow;
        private float goodWindow;
        private float okWindow;
        private float stepDuration;

        // 전투가 바뀔 때마다 올라간다. 공격 모션 뒤에 늦게 들어가는 데미지 코루틴이, 그새 전투가
        // 끝나고 새 전투가 시작됐으면 엉뚱한 몬스터를 때리지 않도록 확인하는 데 쓴다.
        private int battleSerial;

        // 도망친 몬스터는 잠깐 재접촉해도 다시 전투가 걸리지 않게 한다(안 그러면 쿨다운이 끝나자마자
        // 같은 자리에서 또 접촉해 전투가 반복된다). 동시에 한 마리만 기억하면 충분하다(전투는 한 번에 하나).
        private Monster lastMonster;
        private float cooldownUntil;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize()
        {
            playerIdleFrames = Resources.LoadAll<Sprite>(PlayerIdlePath);
            System.Array.Sort(playerIdleFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            playerAttackFrames = Resources.LoadAll<Sprite>(PlayerAttackPath);
            System.Array.Sort(playerAttackFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            playerHitFrames = Resources.LoadAll<Sprite>(PlayerHitPath);
            System.Array.Sort(playerHitFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            playerGuardFrames = Resources.LoadAll<Sprite>(PlayerGuardPath);
            System.Array.Sort(playerGuardFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            battleSplashFrames = Resources.LoadAll<Sprite>("Art/Effects/BattleSplash");
            System.Array.Sort(battleSplashFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            nodeBurstFrames = Resources.LoadAll<Sprite>("Art/Effects/NodeBurst");
            System.Array.Sort(nodeBurstFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            judgmentPerfect = Resources.Load<Sprite>("UI/Judgment/perfect");
            judgmentExcellent = Resources.Load<Sprite>("UI/Judgment/excellent");
            judgmentGood = Resources.Load<Sprite>("UI/Judgment/good");
            judgmentMiss = Resources.Load<Sprite>("UI/Judgment/miss");

            basicSlashFrames = Resources.LoadAll<Sprite>("Art/Effects/BasicSlash");
            System.Array.Sort(basicSlashFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            for (int d = 0; d < 10; d++)
            {
                normalDigits[d] = Resources.Load<Sprite>("UI/DamageDigits/normal_" + d);
                critDigits[d] = Resources.Load<Sprite>("UI/DamageDigits/crit_" + d);
            }

            for (int i = 0; i < Skills.Length; i++)
            {
                skillEffectFrames[i] = Resources.LoadAll<Sprite>(Skills[i].EffectPath);
                System.Array.Sort(skillEffectFrames[i], (a, b) => string.CompareOrdinal(a.name, b.name));
                skillCastFrames[i] = Resources.LoadAll<Sprite>(Skills[i].CastPath);
                System.Array.Sort(skillCastFrames[i], (a, b) => string.CompareOrdinal(a.name, b.name));
            }

            BuildPanel();
        }

        public bool TryBegin(Monster targetMonster, Character targetPlayer)
        {
            if (IsOpen) return false;
            if (targetMonster == null || targetMonster.IsDead) return false;
            if (targetPlayer == null || targetPlayer.IsDead) return false;
            if (targetMonster == lastMonster && Time.unscaledTime < cooldownUntil) return false;
            // 대시로 몹을 뚫고 지나가는 중에는 닿아도 전투가 걸리지 않는다.
            var dash = targetPlayer.GetComponent<CharacterSkillDash>();
            if (dash != null && dash.IsDashing) return false;

            Begin(targetMonster, targetPlayer);
            return true;
        }

        private void Begin(Monster targetMonster, Character targetPlayer)
        {
            monster = targetMonster;
            player = targetPlayer;
            playerMovement = player.GetComponent<CharacterMovement2D>();

            isBossBattle = monster.GetComponent<BossAI>() != null || monster.GetComponent<BossSlimeAI>() != null;
            isPractice = monster.GetComponent<TrainingDummy>() != null;
            practiceDamageTotal = 0f;
            telegraphDuration = isBossBattle ? BossTelegraphDuration : NormalTelegraphDuration;
            blockWindow = isBossBattle ? BossBlockWindow : NormalBlockWindow;
            perfectWindow = isBossBattle ? BossPerfectWindow : NormalPerfectWindow;
            goodWindow = isBossBattle ? BossGoodWindow : NormalGoodWindow;
            okWindow = isBossBattle ? BossOkWindow : NormalOkWindow;
            stepDuration = isBossBattle ? BossStepDuration : NormalStepDuration;
            battleSerial++;

            monsterNameLabel.text = monster.Data != null ? monster.Data.monsterName : "몬스터";
            if (isPractice) monsterNameLabel.text += " (연습)";
            SetupMonsterAnimation();

            // 플레이어는 왼쪽, 몬스터는 오른쪽에서 서로 마주 본다. 대기 애니메이션은 매 프레임
            // Update에서 돌려주고, 여기서는 새 전투가 시작할 때마다 첫 프레임으로 되돌리기만 한다.
            idleFrameIndex = 0;
            idleFrameTimer = 0f;
            if (playerIdleFrames.Length > 0)
            {
                playerPortrait.sprite = playerIdleFrames[0];
                playerPortrait.enabled = true;
            }

            phaseBannerText.text = "";
            resultText.text = "";
            HideJudgment();
            foreach (var note in notePool) note.gameObject.SetActive(false);
            for (int i = 0; i < 2; i++) exclaim[i].gameObject.SetActive(false);

            actionSelectPanel.SetActive(false);
            actionSelectFlee = false;
            skillSelectPanel.SetActive(false);
            myTurnFrame.SetActive(false);
            chosenSkill = -1;
            skillGauge = isPractice ? MaxSkillGauge : 0f;
            StopSkillEffect();
            if (attackAnimRoutine != null)
            {
                StopCoroutine(attackAnimRoutine);
                attackAnimRoutine = null;
            }
            isPlayingAttackAnim = false;
            playerPortrait.color = Color.white;
            ResetMonsterHitFeedback();

            monster.OnDied += HandleMonsterDied;
            monster.OnDamaged += HandleMonsterDamaged;
            player.OnDied += HandlePlayerDied;

            RefreshHpBars();

            // 접촉한 그 순간 바로 월드를 멈추고(몬스터가 더 때리지 못하게), 암전 → BATTLE 연출이
            // 끝난 뒤에야 프레임/체력바/레인 등 실제 전투 UI를 드러낸다.
            contentRoot.SetActive(false);
            battleSplashImage.gameObject.SetActive(false);
            dimBackdrop.color = new Color(dimBackdrop.color.r, dimBackdrop.color.g, dimBackdrop.color.b, 0f);

            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;

            turnRoutine = StartCoroutine(IntroThenEnemyTurn());
        }

        // 암전(알파 0→1) → "BATTLE!!" 슬램 애니메이션 재생 → 전투 UI 공개 → 적 턴 시작, 순서로 진행한다.
        private IEnumerator IntroThenEnemyTurn()
        {
            Color baseColor = dimBackdrop.color;
            float t = 0f;
            while (t < BlackoutFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                dimBackdrop.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(t / BlackoutFadeDuration));
                yield return null;
            }
            dimBackdrop.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);

            if (battleSplashFrames.Length > 0)
            {
                battleSplashImage.gameObject.SetActive(true);
                foreach (var frame in battleSplashFrames)
                {
                    battleSplashImage.sprite = frame;
                    yield return new WaitForSecondsRealtime(BattleSplashFrameInterval);
                }
                battleSplashImage.gameObject.SetActive(false);
            }

            yield return new WaitForSecondsRealtime(PostSplashPause);

            if (BattleEnded()) yield break;
            contentRoot.SetActive(true);
            // 화면이 켜진 뒤의 실제 칸 크기로 몬스터 크기를 다시 맞춘다(대기 프레임이 한 장뿐인 허수아비 등).
            if (monsterPortrait.enabled) SetMonsterFrame(monsterPortrait.sprite);
            turnRoutine = StartCoroutine(EnemyTurn());
        }

        private void HandleMonsterDied(Monster _) => EndBattle(fled: false);
        private void HandlePlayerDied(Character _) => EndBattle(fled: false);
        private void HandleMonsterDamaged(Monster damagedMonster, float damage)
        {
            practiceDamageTotal += damage;
            PlayMonsterOneShot(monsterHitFrames, MonsterHitFrameInterval, restart: true);
            FlashMonsterRed();
            ShowDamageNumber(damage, damagedMonster.LastHitWasCritical, nextHitIsMiss);
        }

        private void FlashMonsterRed()
        {
            if (!monsterPortrait.enabled) return;
            if (monsterFlashRoutine != null) StopCoroutine(monsterFlashRoutine);
            monsterFlashRoutine = StartCoroutine(MonsterFlashRoutine());
        }

        private IEnumerator MonsterFlashRoutine()
        {
            for (int i = 0; i < 2; i++)
            {
                monsterPortrait.color = MonsterHitFlashColor;
                yield return new WaitForSecondsRealtime(MonsterHitFlashOn);
                monsterPortrait.color = Color.white;
                yield return new WaitForSecondsRealtime(MonsterHitFlashOff);
            }
            monsterFlashRoutine = null;
        }

        private void PlayBasicSlash()
        {
            if (basicSlashFrames.Length == 0) return;
            if (basicSlashRoutine != null) StopCoroutine(basicSlashRoutine);
            basicSlashRoutine = StartCoroutine(BasicSlashRoutine());
        }

        private IEnumerator BasicSlashRoutine()
        {
            basicSlashImage.gameObject.SetActive(true);
            foreach (var frame in basicSlashFrames)
            {
                basicSlashImage.sprite = frame;
                yield return new WaitForSecondsRealtime(BasicSlashFrameInterval);
            }
            basicSlashImage.gameObject.SetActive(false);
            basicSlashRoutine = null;
        }

        // 숫자 한 자리씩 Image로 이어 붙여 몬스터 머리 위쪽에 띄우고, 위로 떠오르며 사라지게 한다.
        private void ShowDamageNumber(float damage, bool critical, bool miss)
        {
            var digits = critical ? critDigits : normalDigits;
            string text = Mathf.Max(0, Mathf.RoundToInt(damage)).ToString();
            float height = critical ? CritDigitHeight : miss ? MissDigitHeight : DamageDigitHeight;

            var go = new GameObject("DamageNumber", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(damageNumberLayer, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(Random.Range(-40f, 40f), -Random.Range(0f, 30f));

            var widths = new float[text.Length];
            float totalWidth = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                var sprite = digits[text[i] - '0'];
                if (sprite == null) continue;
                widths[i] = sprite.rect.width / sprite.rect.height * height;
                totalWidth += widths[i] * (i == 0 ? 1f : 1f - DamageDigitOverlap);
            }

            var images = new List<Image>();
            float x = -totalWidth / 2f;
            for (int i = 0; i < text.Length; i++)
            {
                var sprite = digits[text[i] - '0'];
                if (sprite == null) continue;
                if (i > 0) x -= widths[i] * DamageDigitOverlap;

                var digitGO = new GameObject("Digit", typeof(RectTransform), typeof(Image));
                var digitRect = (RectTransform)digitGO.transform;
                digitRect.SetParent(rect, false);
                digitRect.anchorMin = digitRect.anchorMax = new Vector2(0.5f, 0.5f);
                digitRect.pivot = new Vector2(0f, 0.5f);
                digitRect.sizeDelta = new Vector2(widths[i], height);
                digitRect.anchoredPosition = new Vector2(x, 0f);
                var image = digitGO.GetComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;
                images.Add(image);
                x += widths[i];
            }

            StartCoroutine(DamageNumberRoutine(rect, images, miss ? 0.6f : 1f, critical));
        }

        private IEnumerator DamageNumberRoutine(RectTransform rect, List<Image> images, float alpha, bool critical)
        {
            Vector2 start = rect.anchoredPosition;
            float t = 0f;
            while (t < DamageNumberLifetime && rect != null)
            {
                t += Time.unscaledDeltaTime;
                float k = t / DamageNumberLifetime;
                rect.anchoredPosition = start + Vector2.up * (DamageNumberRise * (1f - (1f - k) * (1f - k)));
                // 처음 0.1초는 크게 튀어나왔다가 제 크기로 돌아온다(치명타는 더 크게).
                float pop = Mathf.Lerp(critical ? 1.6f : 1.3f, 1f, Mathf.Clamp01(t / 0.1f));
                rect.localScale = new Vector3(pop, pop, 1f);
                float fade = k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f;
                foreach (var image in images) image.color = new Color(1f, 1f, 1f, alpha * fade);
                yield return null;
            }
            if (rect != null) Destroy(rect.gameObject);
        }

        // 전투가 시작/끝날 때 깜빡임·베기 이펙트·떠 있는 숫자를 모두 정리한다.
        private void ResetMonsterHitFeedback()
        {
            if (monsterFlashRoutine != null)
            {
                StopCoroutine(monsterFlashRoutine);
                monsterFlashRoutine = null;
            }
            if (basicSlashRoutine != null)
            {
                StopCoroutine(basicSlashRoutine);
                basicSlashRoutine = null;
            }
            if (monsterPortrait != null) monsterPortrait.color = Color.white;
            if (basicSlashImage != null) basicSlashImage.gameObject.SetActive(false);
            if (damageNumberLayer != null)
            {
                for (int i = damageNumberLayer.childCount - 1; i >= 0; i--) Destroy(damageNumberLayer.GetChild(i).gameObject);
            }
            nextHitIsMiss = false;
        }

        private void EndBattle(bool fled)
        {
            if (!IsOpen) return;

            if (turnRoutine != null)
            {
                StopCoroutine(turnRoutine);
                turnRoutine = null;
            }

            if (fled && monster != null && player != null)
            {
                lastMonster = monster;
                cooldownUntil = Time.unscaledTime + FleeCooldown;

                float dir = Mathf.Sign(player.transform.position.x - monster.transform.position.x);
                if (Mathf.Approximately(dir, 0f)) dir = 1f;
                var body = player.GetComponent<Rigidbody2D>();
                if (body != null) body.position += new Vector2(dir * FleeKnockbackDistance, 0f);

                StartCoroutine(FleeGraceRoutine(monster, player));
            }

            battleSerial++; // 아직 남아 있는 지연 데미지 코루틴들이 아무것도 하지 않고 끝나게 한다.
            StopSkillEffect();
            ResetMonsterHitFeedback();
            if (monster != null)
            {
                monster.OnDied -= HandleMonsterDied;
                monster.OnDamaged -= HandleMonsterDamaged;
            }
            if (player != null) player.OnDied -= HandlePlayerDied;

            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);

            monster = null;
            player = null;
            playerMovement = null;
        }

        // 도주 직후 유예 시간 동안 그 몬스터를 깜빡이게 하고, 플레이어와 몸이 부딪히지 않게 해서
        // 그대로 지나쳐 갈 수 있게 한다(전투 재진입은 TryBegin의 cooldownUntil이 막는다).
        private IEnumerator FleeGraceRoutine(Monster fledMonster, Character fledPlayer)
        {
            var sprite = fledMonster.GetComponent<SpriteRenderer>();
            var monsterCollider = fledMonster.GetComponent<Collider2D>();
            var playerCollider = fledPlayer.GetComponent<Collider2D>();
            bool ignoring = monsterCollider != null && playerCollider != null;

            float endTime = Time.unscaledTime + FleeCooldown;
            while (Time.unscaledTime < endTime && fledMonster != null && !fledMonster.IsDead)
            {
                // 그 사이 대시가 끝나면서 충돌을 되돌려 놓을 수 있어서 매 프레임 다시 건다.
                if (ignoring && playerCollider != null) Physics2D.IgnoreCollision(playerCollider, monsterCollider, true);
                if (sprite != null) sprite.enabled = Mathf.FloorToInt((endTime - Time.unscaledTime) / FleeBlinkInterval) % 2 == 0;
                yield return null;
            }

            if (sprite != null) sprite.enabled = true;
            if (ignoring && monsterCollider != null && playerCollider != null)
            {
                Physics2D.IgnoreCollision(playerCollider, monsterCollider, false);
            }
        }

        // 플레이어/몬스터 대기 애니메이션만 여기서 돈다. 입력(↑/↓ 판정, 행동 선택)은 각 코루틴이 직접 읽는다.
        // Esc로 바로 도망치던 것은 없앴다 — 전투에서 빠져나가려면 내 턴의 행동 선택에서 도주를 골라야 한다.
        private void Update()
        {
            if (!IsOpen) return;

            if (!isPlayingAttackAnim && playerIdleFrames.Length > 1)
            {
                idleFrameTimer += Time.unscaledDeltaTime;
                if (idleFrameTimer >= IdleFrameInterval)
                {
                    idleFrameTimer -= IdleFrameInterval;
                    idleFrameIndex = (idleFrameIndex + 1) % playerIdleFrames.Length;
                    playerPortrait.sprite = playerIdleFrames[idleFrameIndex];
                }
            }

            UpdateMonsterAnimation();
        }

        private void SetupMonsterAnimation()
        {
            var walk = monster.GetComponent<MonsterWalkSpriteAnimator>();
            var attack = monster.GetComponent<MonsterAttackSpriteAnimator>();
            var dummy = monster.GetComponent<TrainingDummy>();
            var worldSprite = monster.GetComponent<SpriteRenderer>();

            monsterAttackFrames = attack != null && attack.HasFrames ? attack.Frames : System.Array.Empty<Sprite>();
            monsterHitFrames = System.Array.Empty<Sprite>();
            if (dummy != null && dummy.Frames != null && dummy.Frames.Length > 0)
            {
                monsterIdleFrames = new[] { dummy.Frames[0] };
                if (dummy.Frames.Length > 1)
                {
                    monsterHitFrames = new Sprite[dummy.Frames.Length - 1];
                    System.Array.Copy(dummy.Frames, 1, monsterHitFrames, 0, monsterHitFrames.Length);
                }
            }
            else if (walk != null && walk.HasFrames) monsterIdleFrames = walk.Frames;
            else if (worldSprite != null && worldSprite.sprite != null) monsterIdleFrames = new[] { worldSprite.sprite };
            else monsterIdleFrames = System.Array.Empty<Sprite>();

            monsterOneShot = null;
            monsterFrameIndex = 0;
            monsterFrameTimer = 0f;
            monsterPortrait.enabled = monsterIdleFrames.Length > 0;
            if (!monsterPortrait.enabled) return;

            monsterBaseWorldSize = WorldSize(monsterIdleFrames[0]);
            SetMonsterFrame(monsterIdleFrames[0]);
        }

        private static Vector2 WorldSize(Sprite sprite) => sprite.rect.size / sprite.pixelsPerUnit;

        private void SetMonsterFrame(Sprite sprite)
        {
            // 대기 첫 프레임이 칸 안에 꽉 차게 들어가는 배율을 기준으로, 공격 프레임도 같은 배율로 그린다
            // (공격 모션이 칸보다 크면 옆으로 삐져나오는 게 맞다 — 월드에서도 그만큼 크게 휘두르니까).
            // 전투 시작 시점엔 패널이 꺼져 있을 수 있어서 칸 크기는 그릴 때마다 다시 읽는다.
            Vector2 stageSize = monsterStage.rect.size;
            float unitsToCanvas = Mathf.Min(stageSize.x / monsterBaseWorldSize.x, stageSize.y / monsterBaseWorldSize.y);
            monsterPortrait.sprite = sprite;
            monsterPortrait.rectTransform.sizeDelta = WorldSize(sprite) * unitsToCanvas;
        }

        // restart=false면 같은 모션이 이미 재생 중일 때 처음부터 다시 틀지 않는다 — 노드가 촘촘히 오는
        // 동안 공격 모션이 매번 첫 프레임으로 끊기지 않고 끝까지 휘두르게 한다.
        private void PlayMonsterOneShot(Sprite[] clip, float interval, bool restart)
        {
            if (clip == null || clip.Length == 0 || !monsterPortrait.enabled) return;
            if (!restart && monsterOneShot == clip) return;
            monsterOneShot = clip;
            monsterOneShotInterval = interval;
            monsterFrameIndex = 0;
            monsterFrameTimer = 0f;
            SetMonsterFrame(clip[0]);
        }

        private void UpdateMonsterAnimation()
        {
            if (!monsterPortrait.enabled) return;

            monsterFrameTimer += Time.unscaledDeltaTime;
            if (monsterOneShot != null)
            {
                if (monsterFrameTimer < monsterOneShotInterval) return;
                monsterFrameTimer -= monsterOneShotInterval;
                monsterFrameIndex++;
                if (monsterFrameIndex < monsterOneShot.Length)
                {
                    SetMonsterFrame(monsterOneShot[monsterFrameIndex]);
                    return;
                }
                // 모션이 끝나면 대기 반복으로 돌아간다.
                monsterOneShot = null;
                monsterFrameIndex = 0;
                monsterFrameTimer = 0f;
                SetMonsterFrame(monsterIdleFrames[0]);
                return;
            }

            if (monsterIdleFrames.Length < 2 || monsterFrameTimer < MonsterIdleFrameInterval) return;
            monsterFrameTimer -= MonsterIdleFrameInterval;
            monsterFrameIndex = (monsterFrameIndex + 1) % monsterIdleFrames.Length;
            SetMonsterFrame(monsterIdleFrames[monsterFrameIndex]);
        }

        private bool BattleEnded() => !IsOpen || monster == null || monster.IsDead || player == null || player.IsDead;

        private IEnumerator EnemyTurn()
        {
            phaseBannerText.color = MissColor;
            phaseBannerText.text = "적의 턴!";
            yield return new WaitForSecondsRealtime(PhaseBannerDuration);
            phaseBannerText.text = "";

            if (BattleEnded()) yield break;

            var notes = BuildChartNotes(PickChart());
            yield return RunChart(notes, EnemyNoteColor, blockWindow, (note, hit, diff) =>
            {
                PlayMonsterOneShot(monsterAttackFrames, MonsterAttackFrameInterval, restart: false);
                if (hit)
                {
                    TriggerPlayerGuardAnim();
                    ShowResult("막기 성공!", GoodColor);
                }
                else
                {
                    // 연타 중 연속 피격이 피격 무적에 묻히지 않도록 hitReaction 없이 맞는다.
                    // 몬스터 공격은 GOOD(1.0) 고정: (몬스터 공격력 - 내 방어력) x 0.7 + 5, 랜덤 -1~+1.
                    float damage = CombatMath.PhysicalDamage(monster.Attack, player.Stats.defense);
                    player.TakeDamage(damage, monster.transform.position, hitReaction: false);
                    TriggerPlayerHitAnim();
                    ShowResult($"피격! -{damage:0}", MissColor);
                }
                RefreshHpBars();
            });

            if (BattleEnded()) yield break;
            yield return new WaitForSecondsRealtime(AfterChartPause);
            if (BattleEnded()) yield break;
            turnRoutine = StartCoroutine(PlayerTurn());
        }

        private static float AverageNoteCount(string[] charts)
        {
            int total = 0;
            foreach (var chart in charts)
            {
                foreach (char c in chart)
                {
                    if (c == 'U' || c == 'D') total++;
                }
            }
            return charts.Length > 0 ? (float)total / charts.Length : 1f;
        }

        private string PickChart()
        {
            var charts = isBossBattle ? BossCharts : NormalCharts;
            return charts[Random.Range(0, charts.Length)];
        }

        // 채보 문자열을 "몇 초에 어느 레인" 목록으로 바꾼다. 첫 노드가 화면 오른쪽 끝에서 출발해
        // 판정점까지 날아올 시간(telegraphDuration)을 앞에 깔아 둔다. 절반 확률로 위/아래를 뒤집는다.
        private List<ChartNote> BuildChartNotes(string chart)
        {
            bool mirror = Random.value < 0.5f;
            var notes = new List<ChartNote>();
            for (int i = 0; i < chart.Length; i++)
            {
                int lane;
                if (chart[i] == 'U') lane = LaneUp;
                else if (chart[i] == 'D') lane = LaneDown;
                else continue;
                if (mirror) lane = 1 - lane;
                notes.Add(new ChartNote { Lane = lane, HitTime = telegraphDuration + i * stepDuration });
            }
            return notes;
        }

        private IEnumerator PlayerTurn()
        {
            phaseBannerText.color = PlayerNoteColor;
            phaseBannerText.text = "나의 턴!";
            yield return new WaitForSecondsRealtime(PhaseBannerDuration);
            phaseBannerText.text = "";

            if (BattleEnded()) yield break;
            if (isPractice)
            {
                skillGauge = MaxSkillGauge;
                RefreshHpBars();
            }
            yield return ShowActionSelect();
            if (BattleEnded()) yield break;

            if (actionSelectFlee)
            {
                EndBattle(fled: true);
                yield break;
            }

            if (actionSelectPotion)
            {
                // 포션은 선택 창에서 이미 마셨다. 회복량을 잠깐 보여주고 공격 없이 적 턴으로 넘긴다.
                yield return new WaitForSecondsRealtime(PotionResultDuration);
                if (BattleEnded()) yield break;
                turnRoutine = StartCoroutine(EnemyTurn());
                yield break;
            }

            // 스킬을 골랐으면 필살 게이지는 선택 창에서 이미 썼다. 채보 → 마무리 연출 순서로 따로 진행한다.
            if (chosenSkill >= 0)
            {
                int skillIndex = chosenSkill;
                chosenSkill = -1;
                yield return SkillTurn(skillIndex);
                if (BattleEnded()) yield break;
                yield return new WaitForSecondsRealtime(AfterChartPause);
                if (BattleEnded()) yield break;
                turnRoutine = StartCoroutine(EnemyTurn());
                yield break;
            }

            var notes = BuildChartNotes(PickChart());

            yield return RunChart(notes, PlayerNoteColor, okWindow, (note, hit, diff) =>
            {
                if (hit)
                {
                    float rhythm;
                    float gauge;
                    if (diff <= perfectWindow)
                    {
                        rhythm = PerfectMultiplier;
                        gauge = GaugePerPerfect;
                        ShowJudgment(judgmentPerfect);
                    }
                    else if (diff <= goodWindow)
                    {
                        rhythm = GoodMultiplier;
                        gauge = GaugePerGood;
                        ShowJudgment(judgmentExcellent);
                    }
                    else
                    {
                        rhythm = OkMultiplier;
                        gauge = GaugePerOk;
                        ShowJudgment(judgmentGood);
                    }
                    TriggerPlayerAttackAnim();
                    // 채보는 멈추지 않고 계속 흘러가야 하므로 기다리지 않고 따로 띄운다.
                    StartCoroutine(DealDamageAfterImpact(rhythm, false, battleSerial));
                    skillGauge = Mathf.Min(MaxSkillGauge, skillGauge + gauge);
                    RefreshHpBars();
                }
                else
                {
                    // 놓쳐도 MISS 배수(0.3)만큼은 들어간다. 모션 없이 바로 넣는다.
                    ShowJudgment(judgmentMiss);
                    nextHitIsMiss = true;
                    player.DealDamage(monster, MissMultiplier, false);
                    nextHitIsMiss = false;
                    RefreshHpBars();
                }
            });

            if (BattleEnded()) yield break;
            // 마지막 노드의 지연 데미지까지 들어간 뒤에 넘어간다.
            yield return new WaitForSecondsRealtime(Mathf.Max(AfterChartPause, AttackImpactDelay));
            if (BattleEnded()) yield break;
            turnRoutine = StartCoroutine(EnemyTurn());
        }

        // 스킬 턴: 1) 스킬 전용 채보를 친다 — 판정만 하고 데미지는 아직 넣지 않는다(놓친 노드는 0).
        // 2) 채보가 끝나면 시전 모션 + 스킬 이펙트 마무리 연출을 틀고, 맞힌 노드 수만큼 연출 시간에 걸쳐
        //    연타를 넣는다. 노드당 위력 = 일반 공격 한 턴(평균 노드 수 x 1타)의 Power배를 채보 노드 수로 나눈 값
        //    x 그 노드의 판정 배수 — 그래서 많이, 정확히 맞힐수록 총 데미지가 비례해서 커진다.
        private IEnumerator SkillTurn(int skillIndex)
        {
            var skill = Skills[skillIndex];
            var notes = BuildChartNotes(skill.Chart);
            float perNodeMultiplier = skill.Power * AverageNoteCount(isBossBattle ? BossCharts : NormalCharts) / notes.Count;
            var hitMultipliers = new List<float>();

            phaseBannerText.color = SkillNoteColor;
            phaseBannerText.text = skill.Name + " — 준비!";

            yield return RunChart(notes, SkillNoteColor, okWindow, (note, hit, diff) =>
            {
                if (!hit)
                {
                    ShowJudgment(judgmentMiss);
                    return;
                }
                float rhythm;
                if (diff <= perfectWindow)
                {
                    rhythm = PerfectMultiplier;
                    ShowJudgment(judgmentPerfect);
                }
                else if (diff <= goodWindow)
                {
                    rhythm = GoodMultiplier;
                    ShowJudgment(judgmentExcellent);
                }
                else
                {
                    rhythm = OkMultiplier;
                    ShowJudgment(judgmentGood);
                }
                hitMultipliers.Add(rhythm * perNodeMultiplier);
            });

            if (BattleEnded()) yield break;

            if (hitMultipliers.Count == 0)
            {
                phaseBannerText.text = "";
                ShowResult(skill.Name + " 실패...", MissColor);
                yield return new WaitForSecondsRealtime(SkillFailPause);
                yield break;
            }

            // 마무리 연출
            phaseBannerText.text = $"{skill.Name}! ({hitMultipliers.Count}/{notes.Count})";
            PlayPlayerOneShot(skillCastFrames[skillIndex], skill.FinisherDuration, Color.white);
            yield return new WaitForSecondsRealtime(SkillFinisherLeadIn);
            if (BattleEnded()) yield break;
            StartSkillEffect(skillIndex);

            float hitSpan = Mathf.Max(0.1f, skill.FinisherDuration - SkillFinisherLeadIn);
            float interval = hitSpan / hitMultipliers.Count;
            foreach (var multiplier in hitMultipliers)
            {
                player.DealDamage(monster, multiplier, true);
                RefreshHpBars();
                if (BattleEnded()) yield break;
                yield return new WaitForSecondsRealtime(interval);
                if (BattleEnded()) yield break;
            }

            yield return new WaitForSecondsRealtime(SkillFinisherTail);
            StopSkillEffect();
            phaseBannerText.text = "";
        }

        // 모션이 타격 지점까지 진행된 뒤에 데미지를 적용한다 — 안 그러면 일격사일 때 공격
        // 모션이 뜨기도 전에 전투 화면이 바로 닫혀서 몬스터가 이유 없이 사라지는 것처럼 보였다.
        private IEnumerator DealDamageAfterImpact(float attackMultiplier, bool isSkill, int serial)
        {
            yield return new WaitForSecondsRealtime(AttackImpactDelay);
            if (serial != battleSerial || BattleEnded()) yield break;
            if (!isSkill) PlayBasicSlash(); // 스킬은 채보 내내 스킬 이펙트가 따로 돈다
            player.DealDamage(monster, attackMultiplier, isSkill);
            RefreshHpBars();
        }

        // 내 턴 시작 시 한 번, 공격/스킬/아이템/도주 중 고르게 한다. ←/→로 선택을 옮기고 Z/Enter로
        // 확정한다. 도망은 여기서 도주를 골라야만 할 수 있다.
        // 스킬을 고르면 스킬 목록이 뜨고, 거기서 취소하면 이 창으로 돌아온다.
        // 아이템을 고르면 회복 포션을 바로 마시고 확정된다. 포션이 없거나 체력이 가득하면 안내만 뜨고 창이 그대로 열려 있는다.
        private IEnumerator ShowActionSelect()
        {
            chosenSkill = -1;
            actionSelectPotion = false;
            actionSelectIndex = ActionAttack;
            actionMessageText.text = "";
            RefreshActionCursor();
            myTurnTitleText.text = "나의 턴";
            myTurnFrame.SetActive(true);
            actionSelectPanel.SetActive(true);

            bool confirmed = false;
            while (!confirmed)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.leftArrowKey.wasPressedThisFrame)
                    {
                        actionSelectIndex = (actionSelectIndex + 3) % 4;
                        actionMessageText.text = actionSelectIndex == ActionItem ? PotionHint() : "";
                        RefreshActionCursor();
                    }
                    else if (keyboard.rightArrowKey.wasPressedThisFrame)
                    {
                        actionSelectIndex = (actionSelectIndex + 1) % 4;
                        actionMessageText.text = actionSelectIndex == ActionItem ? PotionHint() : "";
                        RefreshActionCursor();
                    }
                    else if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    {
                        if (actionSelectIndex == ActionItem)
                        {
                            string refusal = TryDrinkPotion();
                            if (refusal == null) confirmed = true;
                            else actionMessageText.text = refusal;
                        }
                        else if (actionSelectIndex == ActionSkill)
                        {
                            actionSelectPanel.SetActive(false);
                            yield return ShowSkillSelect();
                            if (BattleEnded()) yield break;
                            if (chosenSkill >= 0)
                            {
                                confirmed = true;
                            }
                            else
                            {
                                actionMessageText.text = "";
                                myTurnTitleText.text = "나의 턴";
                                actionSelectPanel.SetActive(true);
                            }
                        }
                        else
                        {
                            confirmed = true;
                        }
                    }
                }
                yield return null;
            }

            actionSelectPanel.SetActive(false);
            myTurnFrame.SetActive(false);
            actionSelectFlee = actionSelectIndex == ActionFlee;
        }

        // 스킬 목록. 확정하면 필살 게이지를 쓰고 chosenSkill에 담아 돌아가고, 취소하면 chosenSkill=-1로 돌아간다.
        // 게이지가 모자란 스킬은 회색으로 보이고 골라도 안내만 뜬다.
        private IEnumerator ShowSkillSelect()
        {
            chosenSkill = -1;
            skillMessageText.text = "";
            RefreshSkillRows();
            myTurnTitleText.text = "스킬";
            skillSelectPanel.SetActive(true);

            // 행동 선택 창을 확정한 Z가 같은 프레임에 여기서도 눌린 걸로 읽히지 않게 한 프레임 넘긴다.
            yield return null;

            while (true)
            {
                if (BattleEnded()) yield break;
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.upArrowKey.wasPressedThisFrame)
                    {
                        skillSelectIndex = (skillSelectIndex + Skills.Length - 1) % Skills.Length;
                        skillMessageText.text = "";
                        RefreshSkillRows();
                    }
                    else if (keyboard.downArrowKey.wasPressedThisFrame)
                    {
                        skillSelectIndex = (skillSelectIndex + 1) % Skills.Length;
                        skillMessageText.text = "";
                        RefreshSkillRows();
                    }
                    else if (keyboard.xKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
                    {
                        break;
                    }
                    else if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    {
                        if (skillGauge >= Skills[skillSelectIndex].GaugeCost)
                        {
                            skillGauge -= Skills[skillSelectIndex].GaugeCost;
                            chosenSkill = skillSelectIndex;
                            RefreshHpBars();
                            break;
                        }
                        skillMessageText.text = "필살 게이지가 부족해요";
                    }
                }
                yield return null;
            }

            skillSelectPanel.SetActive(false);
            // 스킬 목록을 닫은 키가 바로 행동 선택 창(또는 채보)에서 다시 읽히지 않게 한 프레임 넘긴다.
            yield return null;
        }

        private void RefreshSkillRows()
        {
            for (int i = 0; i < Skills.Length; i++)
            {
                var skill = Skills[i];
                bool selected = i == skillSelectIndex;
                bool affordable = skillGauge >= skill.GaugeCost;
                skillRowTexts[i].text = $"{(selected ? "> " : "   ")}[{skill.Key}] {skill.Name}   게이지 {skill.GaugeCost:0}";
                skillRowTexts[i].color = !affordable ? new Color(0.5f, 0.5f, 0.55f)
                    : selected ? SkillNoteColor : Color.white;
            }
        }

        private void StartSkillEffect(int skillIndex)
        {
            StopSkillEffect();
            var frames = skillEffectFrames[skillIndex];
            if (frames == null || frames.Length == 0) return;
            skillEffectRoutine = StartCoroutine(SkillEffectRoutine(frames));
        }

        private void StopSkillEffect()
        {
            if (skillEffectRoutine != null)
            {
                StopCoroutine(skillEffectRoutine);
                skillEffectRoutine = null;
            }
            if (skillEffectImage != null) skillEffectImage.gameObject.SetActive(false);
        }

        private IEnumerator SkillEffectRoutine(Sprite[] frames)
        {
            skillEffectImage.gameObject.SetActive(true);
            int index = 0;
            while (true)
            {
                skillEffectImage.sprite = frames[index];
                index = (index + 1) % frames.Length;
                yield return new WaitForSecondsRealtime(SkillEffectFrameInterval);
            }
        }

        private Inventory PlayerInventory => player != null ? player.GetComponent<Inventory>() : null;

        private string PotionHint()
        {
            var inventory = PlayerInventory;
            int count = inventory != null ? inventory.GetCount(ItemDatabase.Potion.id) : 0;
            return $"{ItemDatabase.Potion.itemName} {count}개 (체력 +{Mathf.RoundToInt(ItemDatabase.Potion.healAmount)}, 턴 종료)";
        }

        // 포션을 하나 마셔 체력을 회복한다. 마셨으면 null, 못 마시면 그 이유(선택 창에 띄울 안내)를 돌려준다.
        private string TryDrinkPotion()
        {
            var potion = ItemDatabase.Potion;
            var inventory = PlayerInventory;
            if (inventory == null || inventory.GetCount(potion.id) <= 0) return "포션이 없어요";
            if (player.CurrentHp >= player.MaxHp) return "체력이 이미 가득 찼어요";
            if (!inventory.TryRemoveItem(potion, 1)) return "포션이 없어요";

            float before = player.CurrentHp;
            player.Heal(potion.healAmount);
            float healed = player.CurrentHp - before;
            actionSelectPotion = true;
            ShowResult($"체력 +{Mathf.RoundToInt(healed)}", PerfectColor);
            RefreshHpBars();
            return null;
        }

        private void RefreshActionCursor()
        {
            actionCursor.anchorMin = actionCursor.anchorMax = new Vector2(ActionSlotX[actionSelectIndex], 0.30f);
        }

        // 공격 노드를 맞혔을 때 왼쪽 초상화에 Z 공격 모션을 한 번 재생한다. 턴 진행을 막지 않도록
        // StartCoroutine으로 띄워두기만 하고 끝날 때까지 기다리지 않는다.
        private void TriggerPlayerAttackAnim() => PlayPlayerOneShot(playerAttackFrames, PlayerAttackAnimDuration, Color.white);

        // 적 턴에 노드를 놓쳐 맞을 때마다 피격 모션을 처음부터 다시 재생한다(연타로 맞으면 계속 휘청인다).
        private void TriggerPlayerHitAnim() => PlayPlayerOneShot(playerHitFrames, PlayerHitAnimDuration, PlayerHitTint);

        // 적 턴에 노드를 막을 때마다 가드 모션을 처음부터 다시 재생한다.
        private void TriggerPlayerGuardAnim() => PlayPlayerOneShot(playerGuardFrames, PlayerGuardAnimDuration, Color.white);

        private void PlayPlayerOneShot(Sprite[] frames, float duration, Color tint)
        {
            if (frames.Length == 0) return;
            if (attackAnimRoutine != null) StopCoroutine(attackAnimRoutine);
            attackAnimRoutine = StartCoroutine(PlayPlayerOneShotRoutine(frames, duration, tint));
        }

        private IEnumerator PlayPlayerOneShotRoutine(Sprite[] frames, float duration, Color tint)
        {
            isPlayingAttackAnim = true;
            playerPortrait.color = tint;
            float frameDuration = duration / frames.Length;
            foreach (var frame in frames)
            {
                playerPortrait.sprite = frame;
                yield return new WaitForSecondsRealtime(frameDuration);
            }
            playerPortrait.color = Color.white;
            isPlayingAttackAnim = false;
            idleFrameTimer = 0f; // 애니메이션이 끝나면 바로 다음 프레임부터 대기 애니메이션이 매끈하게 이어진다.
            attackAnimRoutine = null;
        }

        // 채보 하나를 처음부터 끝까지 재생한다. 각 노드는 자기 HitTime보다 telegraphDuration 먼저
        // 오른쪽 끝에서 출발해 일정한 속도로 날아오므로, 화면 위 노드 사이 간격이 곧 리듬 간격이다.
        // 여러 노드가 동시에 떠 있을 수 있고, 키(위=Z, 아래=X)를 누르면 그 레인에서 아직 판정 안 된
        // 노드 중 타이밍이 가장 가까운 것을 판정한다: ±acceptWindow 안이면 맞힘, 그보다 일찍인데
        // EarlyMissRange 안이면 그 노드를 놓친 것으로 소모한다(마구 누르기 방지). 그보다 더 멀면 무시.
        // 판정점을 acceptWindow 넘게 지나친 노드도 놓친 것이다. 판정될 때마다 onJudged(노드, 맞힘?, 오차)를 부른다.
        private IEnumerator RunChart(List<ChartNote> notes, Color noteColor, float acceptWindow, System.Action<ChartNote, bool, float> onJudged)
        {
            resultText.text = "";
            HideJudgment();
            int nextSpawn = 0;
            int judgedCount = 0;
            float t = 0f;

            while (judgedCount < notes.Count)
            {
                t += Time.unscaledDeltaTime;

                while (nextSpawn < notes.Count && t >= notes[nextSpawn].HitTime - telegraphDuration)
                {
                    var spawned = notes[nextSpawn++];
                    spawned.Image = RentNoteImage();
                    if (spawned.Image != null)
                    {
                        spawned.Image.color = noteColor;
                        spawned.Image.gameObject.SetActive(true);
                    }
                }

                // 지나쳐 버린 노드를 먼저 놓침 처리하고 위치를 옮긴 뒤에 입력을 본다 — 그래야 이미 늦은
                // 노드가 이번 입력의 판정 대상으로 잡히지 않는다.
                for (int i = 0; i < nextSpawn; i++)
                {
                    var note = notes[i];
                    if (note.Judged) continue;
                    if (t > note.HitTime + acceptWindow)
                    {
                        Judge(note, false, float.MaxValue, onJudged);
                        judgedCount++;
                        if (BattleEnded()) yield break;
                        continue;
                    }
                    if (note.Image != null)
                    {
                        float travelT = 1f - (note.HitTime - t) / telegraphDuration;
                        float x = Mathf.LerpUnclamped(NoteSpawnX, NodeCenter[note.Lane].x, travelT);
                        note.Image.rectTransform.anchoredPosition = ImageToCanvasPos(x, NodeCenter[note.Lane].y);
                    }
                }

                var keyboard = Keyboard.current;
                for (int lane = 0; lane < 2; lane++)
                {
                    bool pressed = keyboard != null && (lane == LaneUp
                        ? keyboard.zKey.wasPressedThisFrame
                        : keyboard.xKey.wasPressedThisFrame);
                    if (!pressed) continue;

                    var target = ClosestNote(notes, nextSpawn, lane, t);
                    if (target == null) continue;
                    float signedDiff = t - target.HitTime; // 음수면 일찍 누른 것.
                    float diff = Mathf.Abs(signedDiff);
                    if (diff <= acceptWindow)
                    {
                        Judge(target, true, diff, onJudged);
                    }
                    else if (signedDiff < 0f && diff <= acceptWindow + EarlyMissRange)
                    {
                        Judge(target, false, diff, onJudged);
                    }
                    else
                    {
                        continue;
                    }
                    judgedCount++;
                    if (BattleEnded()) yield break;
                }

                // 레인 위에 아직 처리할 노드가 날아오고 있으면 그 레인 판정점 위에 "!"를 띄운다.
                for (int lane = 0; lane < 2; lane++)
                {
                    exclaim[lane].gameObject.SetActive(FrontNote(notes, nextSpawn, lane) != null);
                }

                yield return null;
            }

            for (int lane = 0; lane < 2; lane++) exclaim[lane].gameObject.SetActive(false);
        }

        private void Judge(ChartNote note, bool hit, float diff, System.Action<ChartNote, bool, float> onJudged)
        {
            note.Judged = true;
            if (note.Image != null)
            {
                note.Image.gameObject.SetActive(false);
                note.Image = null;
            }
            if (hit) PlayNodeBurst(note.Lane);
            onJudged(note, hit, diff);
        }

        // 이미 출발한 노드 중, 이 레인에서 아직 판정 안 된 가장 앞(가장 먼저 도착할) 노드.
        private static ChartNote FrontNote(List<ChartNote> notes, int spawnedCount, int lane)
        {
            for (int i = 0; i < spawnedCount; i++)
            {
                if (!notes[i].Judged && notes[i].Lane == lane) return notes[i];
            }
            return null;
        }

        // 이미 출발한 노드 중, 이 레인에서 아직 판정 안 됐고 지금 시각 t와 타이밍이 가장 가까운 노드.
        private static ChartNote ClosestNote(List<ChartNote> notes, int spawnedCount, int lane, float t)
        {
            ChartNote best = null;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < spawnedCount; i++)
            {
                var note = notes[i];
                if (note.Judged || note.Lane != lane) continue;
                float diff = Mathf.Abs(t - note.HitTime);
                if (diff < bestDiff)
                {
                    best = note;
                    bestDiff = diff;
                }
            }
            return best;
        }

        private Image RentNoteImage()
        {
            foreach (var image in notePool)
            {
                if (!image.gameObject.activeSelf) return image;
            }
            return null;
        }

        private void ShowResult(string text, Color color)
        {
            HideJudgment();
            resultText.text = text;
            resultText.color = color;
        }

        // 내 공격/스킬 채보의 판정은 글자 대신 판정 로고(UI/Judgment)를 띄운다. 판정 단계와 로고는 강한 순서대로
        // Perfect → PERFECT, Good → EXELLENT, OK → GOOD, Miss → MISS로 맞췄다.
        private void ShowJudgment(Sprite logo)
        {
            if (logo == null) return;
            resultText.text = "";
            if (judgmentRoutine != null) StopCoroutine(judgmentRoutine);
            judgmentRoutine = StartCoroutine(JudgmentRoutine(logo));
        }

        private IEnumerator JudgmentRoutine(Sprite logo)
        {
            judgmentImage.sprite = logo;
            judgmentImage.gameObject.SetActive(true);
            var rect = judgmentImage.rectTransform;
            float t = 0f;
            while (t < JudgmentDuration)
            {
                t += Time.unscaledDeltaTime;
                // 처음엔 크게 튀어나왔다가 제 크기로 줄어들고, 끝 무렵 흐려지며 사라진다.
                float pop = Mathf.Lerp(JudgmentPopScale, 1f, Mathf.Clamp01(t / JudgmentPopTime));
                rect.localScale = new Vector3(pop, pop, 1f);
                float k = t / JudgmentDuration;
                float alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                judgmentImage.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
            judgmentImage.gameObject.SetActive(false);
            judgmentRoutine = null;
        }

        private void HideJudgment()
        {
            if (judgmentRoutine != null)
            {
                StopCoroutine(judgmentRoutine);
                judgmentRoutine = null;
            }
            if (judgmentImage != null) judgmentImage.gameObject.SetActive(false);
        }

        // 노드를 제때 맞혔을 때(막기 성공/공격 적중 공통) 그 노드 자리에서 한 번 터지는 연출.
        // 턴 진행을 막지 않도록 fire-and-forget으로 띄운다.
        private void PlayNodeBurst(int lane)
        {
            if (nodeBurstFrames.Length == 0) return;
            StartCoroutine(PlayNodeBurstRoutine(lane));
        }

        private IEnumerator PlayNodeBurstRoutine(int lane)
        {
            nodeBurstImage.rectTransform.anchoredPosition = ImageToCanvasPos(NodeCenter[lane].x, NodeCenter[lane].y);
            nodeBurstImage.gameObject.SetActive(true);
            foreach (var frame in nodeBurstFrames)
            {
                nodeBurstImage.sprite = frame;
                yield return new WaitForSecondsRealtime(NodeBurstFrameInterval);
            }
            nodeBurstImage.gameObject.SetActive(false);
        }

        private void RefreshHpBars()
        {
            if (monster != null && isPractice)
            {
                monsterHpFill.fillAmount = 1f;
                monsterHpText.text = $"누적 피해 {Mathf.RoundToInt(practiceDamageTotal):N0}";
            }
            else if (monster != null)
            {
                float ratio = monster.MaxHp > 0f ? Mathf.Clamp01(monster.CurrentHp / monster.MaxHp) : 0f;
                monsterHpFill.fillAmount = ratio;
                monsterHpText.text = $"{Mathf.CeilToInt(monster.CurrentHp)} / {Mathf.CeilToInt(monster.MaxHp)}";
            }
            if (player != null)
            {
                float ratio = player.MaxHp > 0f ? Mathf.Clamp01(player.CurrentHp / player.MaxHp) : 0f;
                playerHpFill.fillAmount = ratio;
                playerHpText.text = $"{Mathf.CeilToInt(player.CurrentHp)} / {Mathf.CeilToInt(player.MaxHp)}";
            }
            skillGaugeFill.fillAmount = Mathf.Clamp01(skillGauge / MaxSkillGauge);
            skillGaugeText.text = $"{Mathf.FloorToInt(skillGauge)} / {Mathf.CeilToInt(MaxSkillGauge)}";
        }

        // ---- 화면 구성 ----

        private void BuildPanel()
        {
            panel = new GameObject("RhythmBattlePanel", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            // 전투 중에는 뒤쪽 월드가 아예 안 보이도록 완전히 가린다(반투명으로 비치지 않게 알파 1).
            // 전투가 시작되는 순간엔 알파 0에서 시작해서 암전 연출로 서서히 1까지 올라간다(Begin 참고).
            dimBackdrop = panel.GetComponent<Image>();
            dimBackdrop.color = new Color(0.03f, 0.02f, 0.05f, 1f);
            dimBackdrop.raycastTarget = true;

            contentRoot = new GameObject("Content", typeof(RectTransform));
            var contentRect = (RectTransform)contentRoot.transform;
            contentRect.SetParent(rect, false);
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var frame = new GameObject("Frame", typeof(RectTransform), typeof(RawImage));
            var frameRect = (RectTransform)frame.transform;
            frameRect.SetParent(contentRect, false);
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            frame.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/RhythmBattleFrame");

            var monsterBar = BuildHpBar(contentRect, "MonsterBar", 950f, 60f, 1520f, 150f,
                new Color(0.8f, 0.1f, 0.15f), new Color(1f, 0.85f, 0.4f), "");
            monsterHpFill = monsterBar.Fill;
            monsterNameLabel = monsterBar.NameLabel;
            monsterHpText = monsterBar.ValueLabel;

            var playerBar = BuildHpBar(contentRect, "PlayerBar", 150f, 60f, 650f, 150f,
                new Color(0.25f, 0.55f, 0.95f), new Color(0.85f, 0.9f, 1f), "플레이어");
            playerHpFill = playerBar.Fill;
            playerHpText = playerBar.ValueLabel;
            // 플레이어 체력바 바로 아래의 필살 게이지.
            var gaugeBar = BuildHpBar(contentRect, "SkillGauge", 150f, 155f, 560f, 225f,
                SkillNoteColor, SkillNoteColor, "필살 게이지");
            skillGaugeFill = gaugeBar.Fill;
            skillGaugeText = gaugeBar.ValueLabel;

            playerPortrait = CreateImage(contentRect, "PlayerPortrait", 270f, 250f, 530f, 510f);
            playerPortrait.preserveAspect = true;

            monsterStage = CreateRect(contentRect, "MonsterStage", 1080f, 230f, 1380f, 530f, typeof(RectTransform));
            monsterPortrait = new GameObject("MonsterPortrait", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            monsterPortrait.raycastTarget = false;
            var monsterPortraitRect = monsterPortrait.rectTransform;
            monsterPortraitRect.SetParent(monsterStage, false);
            // 칸의 아래쪽 가운데에 발밑(스프라이트 기준점과 같은 아래 중앙)을 붙이고, 크기는 프레임마다 SetMonsterFrame이 정한다.
            monsterPortraitRect.anchorMin = new Vector2(0.5f, 0f);
            monsterPortraitRect.anchorMax = new Vector2(0.5f, 0f);
            monsterPortraitRect.pivot = new Vector2(0.5f, 0f);
            monsterPortraitRect.anchoredPosition = Vector2.zero;
            // 몬스터 원화는 기본이 오른쪽을 보는 방향이라(월드의 flipX 관례와 동일), 왼쪽의
            // 플레이어를 바라보도록 좌우로 뒤집는다.
            monsterPortraitRect.localScale = new Vector3(-1f, 1f, 1f);

            // 스킬 마무리 연출 동안 몬스터 위에 겹쳐 반복 재생하는 이펙트(몬스터 초상화보다 살짝 크게).
            skillEffectImage = CreateImage(contentRect, "SkillEffect", 1030f, 190f, 1430f, 570f);
            skillEffectImage.preserveAspect = true;
            skillEffectImage.gameObject.SetActive(false);

            // 일반 공격 베기 이펙트는 몬스터 몸통 위에 겹친다.
            basicSlashImage = CreateImage(contentRect, "BasicSlash", 1060f, 230f, 1400f, 560f);
            basicSlashImage.preserveAspect = true;
            basicSlashImage.gameObject.SetActive(false);

            // 데미지 숫자는 몬스터 칸 위쪽 가장자리에서 떠오른다(이펙트보다 위에 그려지게 뒤에 만든다).
            damageNumberLayer = CreateRect(contentRect, "DamageNumbers", 1080f, 230f, 1380f, 530f, typeof(RectTransform));

            phaseBannerText = CreateText(contentRect, "PhaseBanner", 600f, 160f, 1070f, 230f, 42f, Color.white, TextAnchor.MiddleCenter);
            phaseBannerText.fontStyle = FontStyle.Bold;

            resultText = CreateText(contentRect, "ResultText", 550f, 380f, 1120f, 460f, 40f, Color.white, TextAnchor.MiddleCenter);
            resultText.fontStyle = FontStyle.Bold;

            judgmentImage = CreateImage(contentRect, "JudgmentLogo", 640f, 335f, 1030f, 505f);
            judgmentImage.preserveAspect = true;
            judgmentImage.gameObject.SetActive(false);

            var nodeSprite = Resources.Load<Sprite>("UI/NodeDiamond");
            for (int i = 0; i < NotePoolSize; i++)
            {
                var note = CreateMovableImage(contentRect, "Note" + i, NoteSize);
                note.sprite = nodeSprite;
                note.gameObject.SetActive(false);
                notePool[i] = note;
            }
            for (int i = 0; i < 2; i++)
            {
                float cx = NodeCenter[i].x;
                float cy = NodeCenter[i].y;
                var exText = CreateText(contentRect, "Exclaim" + i, cx - 36f, cy - 128f, cx + 36f, cy - 40f, 54f, Color.white, TextAnchor.MiddleCenter);
                exText.fontStyle = FontStyle.Bold;
                exText.text = "!";
                exText.gameObject.SetActive(false);
                exclaim[i] = exText;
            }

            nodeBurstImage = CreateMovableImage(contentRect, "NodeBurst", 220f);
            nodeBurstImage.raycastTarget = false;
            nodeBurstImage.gameObject.SetActive(false);

            BuildMyTurnFrame(contentRect);

            // "BATTLE!!" 슬램 연출. 암전 직후, 전투 UI(contentRoot)가 드러나기 전에 잠깐 재생된다.
            // 프레임 한 장이 세로로 긴 비율(307:512)이라 그에 맞춰 박스도 세로로 길게 잡는다.
            battleSplashImage = CreateImage(rect, "BattleSplash", 656f, 150f, 1016f, 750f);
            battleSplashImage.preserveAspect = true;
            battleSplashImage.gameObject.SetActive(false);

            panel.SetActive(false);
        }

        // 내 턴 동안 뜨는 테두리 창(UI/BattleMyTurn, 1448x1086). 위쪽 가로 띠(캐릭터 오른쪽)에 제목("나의 턴"/"스킬")을,
        // 아래 큰 칸(대략 x 200~1370, y 465~940)에 행동 선택 창 또는 스킬 목록을 띄운다.
        private const float MyTurnImageWidth = 1448f;
        private const float MyTurnImageHeight = 1086f;
        private const float MyTurnMainCenterX = 785f;
        private const float MyTurnMainCenterY = 702f;

        private void BuildMyTurnFrame(Transform parent)
        {
            myTurnFrame = new GameObject("MyTurnFrame", typeof(RectTransform), typeof(RawImage));
            var frameRect = (RectTransform)myTurnFrame.transform;
            frameRect.SetParent(parent, false);
            frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.sizeDelta = new Vector2(960f, 720f); // 기준 해상도 높이에 꽉 차게, 그림 비율(약 4:3) 그대로.
            frameRect.anchoredPosition = Vector2.zero;
            var bg = myTurnFrame.GetComponent<RawImage>();
            bg.texture = Resources.Load<Texture2D>("UI/BattleMyTurn");
            bg.raycastTarget = false;

            myTurnTitleText = CreateText(frameRect, "Title", 0f, 0f, 0f, 0f, 30f, SkillNoteColor, TextAnchor.MiddleCenter);
            SetMyTurnAnchors(myTurnTitleText.rectTransform, 540f, 290f, 1320f, 420f);
            myTurnTitleText.fontSize = 36;
            myTurnTitleText.fontStyle = FontStyle.Bold;

            BuildActionSelectPanel(frameRect);
            BuildSkillSelectPanel(frameRect);

            myTurnFrame.SetActive(false);
        }

        // 행동 선택 창. UI/BattleActionSelect 그림 안에 공격/스킬/아이템/도주 4칸 아이콘+글자가 이미 그려져 있어서,
        // 고른 칸 아래에 Select_Effect 빛줄기만 옮겨가며 띄운다. 내 턴 테두리 창의 아래 큰 칸 한가운데에 놓는다
        // (위아래 장식 가시는 칸 테두리에 살짝 걸친다).
        private void BuildActionSelectPanel(RectTransform parent)
        {
            actionSelectPanel = new GameObject("ActionSelect", typeof(RectTransform));
            var panelRect = (RectTransform)actionSelectPanel.transform;
            panelRect.SetParent(parent, false);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(MyTurnMainCenterX / MyTurnImageWidth, 1f - MyTurnMainCenterY / MyTurnImageHeight);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(724f, 543f); // UI/BattleActionSelect(1448x1086)의 절반 크기.
            panelRect.anchoredPosition = Vector2.zero;

            var bgGO = new GameObject("BG", typeof(RectTransform), typeof(RawImage));
            var bgRect = (RectTransform)bgGO.transform;
            bgRect.SetParent(panelRect, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGO.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/BattleActionSelect");

            var cursorGO = new GameObject("Cursor", typeof(RectTransform), typeof(RawImage));
            actionCursor = (RectTransform)cursorGO.transform;
            actionCursor.SetParent(panelRect, false);
            actionCursor.pivot = new Vector2(0.5f, 0.5f);
            actionCursor.sizeDelta = new Vector2(120f, 16f);
            actionCursor.anchorMin = actionCursor.anchorMax = new Vector2(ActionSlotX[0], 0.30f);
            actionCursorImage = cursorGO.GetComponent<RawImage>();
            actionCursorImage.texture = Resources.Load<Texture2D>("UI/Select_Effect");
            actionCursorImage.raycastTarget = false;

            // 안내 문구(포션 개수, 사용 불가 이유 등)는 테두리 창의 큰 칸 아래쪽에 띄운다. 행동 선택 창과 같이 켜지고
            // 꺼지도록 창의 자식으로 두되, 위치는 테두리 창 기준으로 잡는다.
            var msgGO = new GameObject("Message", typeof(RectTransform), typeof(Text), typeof(Outline));
            var msgRect = (RectTransform)msgGO.transform;
            msgRect.SetParent(panelRect, false);
            msgRect.anchorMin = msgRect.anchorMax = new Vector2(0.5f, 0.5f);
            msgRect.pivot = new Vector2(0.5f, 0.5f);
            msgRect.sizeDelta = new Vector2(700f, 36f);
            msgRect.anchoredPosition = new Vector2(0f, -(905f - MyTurnMainCenterY) * (720f / MyTurnImageHeight));
            msgGO.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            actionMessageText = msgGO.GetComponent<Text>();
            actionMessageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            actionMessageText.fontSize = 22;
            actionMessageText.fontStyle = FontStyle.Bold;
            actionMessageText.alignment = TextAnchor.MiddleCenter;
            actionMessageText.color = MissColor;
            actionMessageText.raycastTarget = false;

            actionSelectPanel.SetActive(false);
        }

        // 스킬 목록. 내 턴 테두리 창 전체를 덮는 빈 판이라 좌표는 UI/BattleMyTurn 그림 픽셀 기준 그대로 쓴다.
        // 행동 선택 창 대신 아래 큰 칸에 스킬 목록/안내 문구/조작 안내를 글자로 그린다(제목은 테두리 창이 "스킬"로 바꿔 보여준다).
        private void BuildSkillSelectPanel(RectTransform parent)
        {
            skillSelectPanel = new GameObject("SkillSelect", typeof(RectTransform));
            var panelRect = (RectTransform)skillSelectPanel.transform;
            panelRect.SetParent(parent, false);
            SetRelativeAnchors(panelRect, 0f, 0f, 1f, 1f);

            // 아래 큰 칸(대략 x 200~1370, y 465~940) 안에 다섯 줄. 왼쪽은 캐릭터 옷자락이 걸쳐 있어 조금 띄운다.
            const float rowTop = 485f;
            const float rowHeight = 64f;
            for (int i = 0; i < Skills.Length; i++)
            {
                var row = CreateText(panelRect, "Skill" + i, 0f, 0f, 0f, 0f, 26f, Color.white, TextAnchor.MiddleLeft);
                float top = rowTop + i * rowHeight;
                SetMyTurnAnchors(row.rectTransform, 300f, top, 1330f, top + rowHeight);
                row.fontSize = 24;
                row.fontStyle = FontStyle.Bold;
                skillRowTexts[i] = row;
            }

            skillMessageText = CreateText(panelRect, "Message", 0f, 0f, 0f, 0f, 22f, MissColor, TextAnchor.MiddleCenter);
            SetMyTurnAnchors(skillMessageText.rectTransform, 220f, 810f, 1350f, 865f);
            skillMessageText.fontSize = 20;
            skillMessageText.fontStyle = FontStyle.Bold;

            var hint = CreateText(panelRect, "Hint", 0f, 0f, 0f, 0f, 18f, new Color(0.75f, 0.75f, 0.8f), TextAnchor.MiddleCenter);
            SetMyTurnAnchors(hint.rectTransform, 220f, 870f, 1350f, 925f);
            hint.fontSize = 17;
            hint.text = "↑↓ 선택   Z 사용   X 뒤로";

            skillSelectPanel.SetActive(false);
        }

        // UI/BattleMyTurn 그림 속 픽셀 사각형(왼쪽 위 기준)을 스킬 창 안의 앵커로 바꾼다.
        private static void SetMyTurnAnchors(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            SetRelativeAnchors(rect, x0 / MyTurnImageWidth, 1f - y1 / MyTurnImageHeight, x1 / MyTurnImageWidth, 1f - y0 / MyTurnImageHeight);
        }

        // CreateText 등은 배경 그림 픽셀 좌표 기준이라, 작은 창 안쪽 요소는 만든 뒤 창 기준 0~1 비율로 다시 잡는다.
        private static void SetRelativeAnchors(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private HpBarRefs BuildHpBar(Transform parent, string name, float x0, float y0, float x1, float y1, Color barColor, Color nameColor, string nameText)
        {
            var rect = CreateRect(parent, name, x0, y0, x1, y1, typeof(RectTransform), typeof(Image));
            rect.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            rect.GetComponent<Image>().raycastTarget = false;

            var nameGO = new GameObject("Name", typeof(RectTransform), typeof(Text));
            var nameRect = (RectTransform)nameGO.transform;
            nameRect.SetParent(rect, false);
            nameRect.anchorMin = new Vector2(0f, 0.55f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            var nameLabel = nameGO.GetComponent<Text>();
            nameLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameLabel.fontSize = Mathf.RoundToInt(22f * PxToCanvas);
            nameLabel.fontStyle = FontStyle.Bold;
            nameLabel.alignment = TextAnchor.MiddleCenter;
            nameLabel.color = nameColor;
            nameLabel.text = nameText;
            nameLabel.raycastTarget = false;

            var fillBG = new GameObject("FillBG", typeof(RectTransform), typeof(Image));
            var fillBGRect = (RectTransform)fillBG.transform;
            fillBGRect.SetParent(rect, false);
            fillBGRect.anchorMin = new Vector2(0f, 0f);
            fillBGRect.anchorMax = new Vector2(1f, 0.5f);
            fillBGRect.offsetMin = new Vector2(6f, 4f);
            fillBGRect.offsetMax = new Vector2(-6f, 0f);
            fillBG.GetComponent<Image>().color = new Color(0.12f, 0.08f, 0.1f, 0.9f);
            fillBG.GetComponent<Image>().raycastTarget = false;

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGO.transform;
            fillRect.SetParent(fillBGRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            var fill = fillGO.GetComponent<Image>();
            fill.color = barColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            var valueGO = new GameObject("Value", typeof(RectTransform), typeof(Text));
            var valueRect = (RectTransform)valueGO.transform;
            valueRect.SetParent(fillBGRect, false);
            valueRect.anchorMin = Vector2.zero;
            valueRect.anchorMax = Vector2.one;
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;
            var valueLabel = valueGO.GetComponent<Text>();
            valueLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueLabel.fontSize = Mathf.RoundToInt(16f * PxToCanvas);
            valueLabel.fontStyle = FontStyle.Bold;
            valueLabel.alignment = TextAnchor.MiddleCenter;
            valueLabel.color = Color.white;
            valueLabel.raycastTarget = false;

            return new HpBarRefs(fill, nameLabel, valueLabel);
        }

        // ---- 그림 픽셀 좌표(왼쪽 위 x0,y0 ~ 오른쪽 아래 x1,y1)로 요소를 만드는 도우미들 ----

        private static RectTransform CreateRect(Transform parent, string name, float x0, float y0, float x1, float y1, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0 / ImageWidth, 1f - y1 / ImageHeight);
            rect.anchorMax = new Vector2(x1 / ImageWidth, 1f - y0 / ImageHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image CreateImage(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            var image = CreateRect(parent, name, x0, y0, x1, y1, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        // 고정된 두 변 사이를 늘려 채우는 CreateImage와 달리, 날아오는 노드처럼 매 프레임
        // anchoredPosition을 직접 바꿔 움직여야 하는 요소용 — 왼쪽위 한 점에 고정 크기로 붙고
        // 중심 기준(pivot 0.5,0.5)으로 움직인다.
        private static Image CreateMovableImage(Transform parent, string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        // 그림 픽셀 좌표(좌상단 기준)를 CreateMovableImage로 만든 요소의 anchoredPosition(좌상단 anchor,
        // 캔버스 단위, 아래로 갈수록 y가 음수)으로 바꾼다.
        private static Vector2 ImageToCanvasPos(float ximg, float yimg)
        {
            return new Vector2(ximg * PxToCanvas, -(yimg * PxToCanvas));
        }

        private static Text CreateText(Transform parent, string name, float x0, float y0, float x1, float y1, float fontPx, Color color, TextAnchor alignment)
        {
            var text = CreateRect(parent, name, x0, y0, x1, y1, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(fontPx * PxToCanvas));
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
