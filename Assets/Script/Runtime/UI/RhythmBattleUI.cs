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
    // 적 턴(채보 1개)과 내 턴(채보 1개)을 몬스터나 플레이어가 쓰러질 때까지 번갈아 반복한다(보스전은 적 턴, 그 외는 내 턴이 먼저).
    // 데미지 계산/치명타/보상(경험치·골드·드랍·퀘스트)은 전부 기존 Character.DealDamage / Monster.TakeDamage
    // 파이프라인을 그대로 통과시켜서, 승리 시 별도 처리 없이 기존 OnDied/AnyDied 훅이 다 처리해준다.
    // 패배(플레이어 체력 0)도 마찬가지로 Character.OnDied → DeathReturnUI가 그대로 이어받는다.
    public partial class RhythmBattleUI : MonoBehaviour
    {
        // 배경 그림(UI/RhythmBattleFrame, 1672x941) 픽셀 좌표계. 좌상단이 원점이고 아래로 갈수록 y가 커진다.
        private const float ImageWidth = 1672f;
        private const float ImageHeight = 941f;
        private const float PxToCanvas = 720f / ImageHeight;

        private const int LaneUp = 0;
        private const int LaneDown = 1;

        // 채보: 한 글자가 한 칸(박자 한 조각)이다. U=위 노드, D=아래 노드, -=쉼.
        // 붙어 있는 글자는 한 칸 간격으로 연달아 오고, '-'가 많을수록 다음 노드까지 텀이 길어진다.
        // 소문자 u/d는 롱노트 머리이고, 바로 뒤의 '='가 그 칸 수만큼 꼬리를 늘인다(예: "u===" = 위 레인 3칸 동안 누르고 있기).
        // 롱노트는 머리를 타이밍에 맞춰 누른 뒤 꼬리가 지나갈 때까지 떼지 않아야 성공이다(판정은 머리 기준으로 한 번).
        // A/B는 페이크 노드다: A는 위 레인으로 날아오다가 판정점 직전에 아래 레인으로 미끄러지고, B는 그 반대다(판정은 바뀐 레인 기준).
        // 턴마다 하나를 무작위로 고르고, 절반 확률로 위/아래를 뒤집어서 같은 채보도 다르게 느껴지게 한다.
        private static readonly string[] NormalCharts =
        {
            "UUU-D---D---UU-U--D---",
            "D-D-U---UUU-D---D-----",
            "U---U---DD-D--U--UU---",
            "DDD-U---U---D-D---U---",
            "U-D---UU----D-DD--U---",
            "u===--D-D---UU--d===----",
            "D-D-u=====--U---D-D-----",
        };
        // 보스는 칸이 더 짧고(더 빠른 박자) 연타 묶음이 더 길고 촘촘하다.
        private static readonly string[] BossCharts =
        {
            "UUU-D-D-UU-U-DDD-U---UUUU-D---",
            "UDUD--UUU-DD-U-D-DDD--U-U-UU--",
            "DD-DD-UUUU--D-U-D-U---DDD-UUU-",
            "UUU-DDD-UDU-D-DD-UUU--D-D-D---",
            "UUU-D-D-u=====--DDD-U---UUUU-D---",
            "DD-DD-d====-U-D-D-U---UUU-u===---",
        };
        // ---- 보스 패턴 ----
        // 보스의 적 턴은 아래 패턴 중 하나를 무작위로(직전 패턴은 피해서) 골라 친다. 패턴마다 채보·박자·노드 속도·피해 배수가 다르고,
        // 일부는 특수 규칙(노드가 사라짐 / 막지 못한 피해만큼 보스 회복)이 붙는다. 체력이 절반 이하가 되면 분노 상태가 되어
        // 노드가 더 빨리 날아오고 피해가 커지며, 분노 전용 패턴이 풀리고, 일정 확률로 패턴 두 개를 쉬지 않고 이어 친다(연계).
        private class BossPattern
        {
            public string Name;
            public string Hint;
            // 루나벨처럼 검을 쓰지 않는 보스에게 보여 줄 이름/설명(규칙은 같다).
            public string BloodName;
            public string BloodHint;
            public string[] Charts;
            public float StepScale = 1f;     // 한 칸 길이 배수(작을수록 박자가 빠르다)
            public float ApproachScale = 1f; // 노드가 날아오는 시간 배수(작을수록 노드가 빠르다)
            // 루나벨전에서만 쓰는 속도 배수. 0이면 위의 StepScale/ApproachScale을 그대로 쓴다.
            public float BloodStepScale;
            public float BloodApproachScale;
            // 루나벨전에서만 쓰는 피해 배수. 0이면 DamageMultiplier를 그대로 쓴다.
            public float BloodDamageMultiplier;
            public float DamageMultiplier = 1f;
            public bool Vanish;              // 노드가 판정점에 닿기 전에 어둠 속으로 사라진다("!" 표시도 없음)
            public bool Drain;               // 막지 못한 피해의 일부만큼 보스가 회복한다
            public bool EnragedOnly;
        }

        private static readonly BossPattern[] BossPatterns =
        {
            new BossPattern
            {
                Name = "연속 베기", Hint = "쉴 새 없는 칼날을 막아라!",
                BloodName = "피의 손길", BloodHint = "쉴 새 없이 뻗어 오는 피의 손을 쳐내라!", Charts = BossCharts,
            },
            new BossPattern
            {
                Name = "질풍 연격", Hint = "눈에 보이지 않을 만큼 빠른 연타!",
                BloodName = "혈우 난무", BloodHint = "피의 비처럼 쏟아지는 손!", StepScale = 0.7f, ApproachScale = 0.9f, DamageMultiplier = 0.6f,
                // 루나벨의 혈우 난무는 너무 빠르다는 피드백으로 보스 기사보다 조금 느리게 친다.
                BloodStepScale = 0.85f, BloodApproachScale = 1.05f,
                Charts = new[]
                {
                    "UDUDUDUD--DDUUDDUU--UUUDDDUD---",
                    "UUDDUUDD--UDUDUU-DDDUUUD---",
                    "DUDUDDUU--UUUU-DDDD-UDUDU---",
                    "UDDUDDUDDU--UUDUUDUUD--DUDU---",
                },
            },
            new BossPattern
            {
                Name = "대검 내려치기", Hint = "한 방 한 방이 무겁다! 노드가 빠르다!",
                BloodName = "심홍의 일격", BloodHint = "한 방 한 방이 무겁다! 손이 빠르다!", ApproachScale = 0.7f, DamageMultiplier = 2.5f,
                Charts = new[]
                {
                    "U-----D---d=====---U-----UD----",
                    "D----U----u======----D-D-----",
                    "U---U-----d====---UD-----U----",
                    "D---DU----u===---D----d=====----",
                },
            },
            new BossPattern
            {
                Name = "환영 베기", Hint = "노드가 레인을 바꾼다! 끝까지 지켜봐라!",
                BloodName = "환혹의 손", BloodHint = "손이 레인을 바꾼다! 끝까지 지켜봐라!", DamageMultiplier = 1.3f,
                // 루나벨의 환혹의 손은 너무 아프다는 피드백으로 보스 기사보다 약하게 친다.
                BloodDamageMultiplier = 0.8f,
                Charts = new[]
                {
                    "U-A-D-B---UU-A--DD-B---U-A----",
                    "B--D-A--UU--B-A-D---AB----",
                    "D-B-U-AA--D-U-B-B--DDA-----",
                    "A-A-B-B--U-D-A-B--UUD-BA----",
                },
            },
            new BossPattern
            {
                Name = "그림자 베기", Hint = "노드가 어둠 속으로 사라진다! 박자를 기억해라!",
                BloodName = "그림자 손아귀", BloodHint = "손이 붉은 안개 속으로 사라진다! 박자를 기억해라!", DamageMultiplier = 1.2f, Vanish = true, EnragedOnly = true,
                Charts = new[]
                {
                    "U-U-D-D-U-D-U-U-D---",
                    "D-D-U-D-D-U-U-D-U---",
                    "U-D-U-D-D-U-D-U-UU--",
                    "UU--DD--UD--DU--UUD---",
                },
            },
            new BossPattern
            {
                Name = "흡혈 참격", Hint = "막지 못한 만큼 보스가 체력을 빨아들인다!",
                BloodName = "혈류 흡수", BloodHint = "막지 못한 만큼 루나벨이 피를 빨아들인다!", DamageMultiplier = 1.1f, Drain = true, EnragedOnly = true,
                Charts = BossCharts,
            },
        };

        private const float BossEnrageHealthFraction = 0.5f;
        private const float BossEnrageApproachScale = 0.85f;
        private const float BossEnrageDamageScale = 1.25f;
        private const float BossComboChance = 0.4f;
        private const float BossDrainRatio = 0.8f;
        private const float BossPatternAnnounceDuration = 0.8f;
        private const float BossComboPause = 0.35f;

        // ---- 플레이어 공격 패턴 ----
        // 일반 공격 턴도 매번 같은 느낌이 나지 않도록 아래 패턴 중 하나를 무작위로(직전 패턴은 피해서) 골라 친다.
        // 노드당 위력은 "기본 채보 평균 노드 수 / 이 패턴 평균 노드 수 x Power"라서, 노드가 많든 적든 한 턴 총 위력은
        // 기본 공격과 비슷하고, 어려운 패턴(Power > 1)만 조금 더 세다. Charts가 null이면 기본 채보(Normal/BossCharts)를 쓴다.
        private class PlayerPattern
        {
            public string Name;
            public string Hint;
            public string[] Charts;
            public float StepScale = 1f;
            public float ApproachScale = 1f;
            public float Power = 1f;
        }

        private static readonly PlayerPattern[] PlayerPatterns =
        {
            new PlayerPattern { Name = "사신의 연격", Hint = "박자에 맞춰 베어라!" },
            new PlayerPattern
            {
                Name = "질풍 연참", Hint = "빠른 연타! 콤보를 이어 가라!", StepScale = 0.8f,
                Charts = new[]
                {
                    "UDUD-UUDD-DUDU--UUDD-DDUU---",
                    "UUDD-UDUD-DDUU-DUDU--UUUDDD---",
                    "DUDU-DDUU--UDUD-UUDD-DUUD---",
                },
            },
            new PlayerPattern
            {
                Name = "참수 일격", Hint = "노드는 적지만 한 방이 묵직하다!", ApproachScale = 0.85f,
                Charts = new[]
                {
                    "U----D----u===----D-----U---",
                    "D----d====----U----U--D----",
                    "u===----D----U-U----d===----",
                },
            },
            new PlayerPattern
            {
                Name = "허상 연격", Hint = "노드가 레인을 바꾼다! 대신 더 강하다!", Power = 1.15f,
                Charts = new[]
                {
                    "U-A-D---B-U--A-D---B---",
                    "A--U-B--D-A--UB--D-----",
                    "D-B-U---A-A--D-U-B-----",
                },
            },
        };

        // 콤보: 내 공격 턴 안에서 노드를 연달아 맞힐 때마다 위력이 ComboBonusPerHit씩(최대 MaxComboBonus) 오르고, 놓치면 끊긴다.
        // 한 턴을 하나도 놓치지 않으면(풀 콤보) 채보가 끝난 뒤 마무리 일격이 한 번 더 들어간다.
        private const float ComboBonusPerHit = 0.03f;
        private const float MaxComboBonus = 0.3f;
        private const float FullComboFinisherHits = 2f; // 마무리 일격 위력 = Perfect 노드 이만큼
        private const float PlayerPatternAnnounceDuration = 0.6f;
        // 페이크 노드가 판정점에 닿기 이만큼(날아오는 시간 대비 비율) 전에 레인을 바꾸고, 바꾸는 데 이만큼(초) 걸린다.
        private const float FeintSwitchLeadFraction = 0.42f;
        private const float FeintSwitchDuration = 0.1f;
        // 사라지는 노드: 경로의 이 비율 지점부터 흐려지기 시작해 이 비율 지점에서 완전히 사라진다.
        private const float VanishFadeStart = 0.4f;
        private const float VanishFadeEnd = 0.6f;
        private static readonly Color EnragedNoteColor = new Color(0.85f, 0.2f, 1f, 0.95f);

        // 루나벨 전투: 적 턴에 노드가 판정점에 닿기 직전마다 루나벨이 손을 뻗어 피의 손을 날린다. 피의 손은
        // 노드가 판정점에 닿는 순간 플레이어에게 도착해서, 막으면 튕겨 나가고 놓치면 그대로 할퀸다(피해는 판정에서 들어간다).
        private bool isLunabelBattle;
        // 루나벨의 적 턴 노드: 탭은 붉은 눈, 롱노트는 눈 → 핏빛 막대 → 화살촉 꼬리(lunabel`s Node에서 잘라낸 것).
        private const string LunabelNodePath = "UI/Battle/LunabelNode";
        private const float LunabelNoteSize = 110f;
        private const float LunabelBarThickness = LunabelNoteSize * 0.27f;
        private const float LunabelTailSize = LunabelNoteSize * 0.55f;
        private Sprite lunabelTapSprite;
        private Sprite lunabelBarSprite;
        private Sprite lunabelTailSprite;
        private Sprite defaultNoteSprite;
        // 지금 흐르는 채보에 루나벨 노드를 쓸지(적 턴 패턴 동안만 켠다).
        private bool useLunabelNotes;
        private const int BloodHandPoolSize = 8;
        private const float BloodHandTravelTime = 0.3f;
        private const float BloodHandFrameInterval = 0.06f;
        private const float BloodHandFadeTime = 0.12f;
        // 그림 픽셀 좌표. 몬스터 칸(1080~1380) 앞 손끝 높이에서 출발해 플레이어 초상화(270~530) 가슴께에 닿는다.
        private const float BloodHandStartX = 1120f;
        private const float BloodHandEndX = 470f;
        private const float BloodHandCenterY = 380f;
        private const float BloodHandHeight = 120f;
        private readonly Image[] bloodHandPool = new Image[BloodHandPoolSize];
        private readonly Coroutine[] bloodHandRoutines = new Coroutine[BloodHandPoolSize];
        private Sprite[] bloodHandFrames = System.Array.Empty<Sprite>();

        private float baseTelegraphDuration;
        private float baseStepDuration;
        private bool vanishNotes;
        private BossPattern lastBossPattern;
        private PlayerPattern lastPlayerPattern;

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
            public readonly bool IsUltimate;      // 궁극기만 시전 모션 전에 눈을 번쩍 뜨는 연출(EyeFlash)이 먼저 재생된다.
            // 시전 모션 프레임(같은 크기 캔버스, 발이 캔버스 맨 아래) 속 캐릭터 키(px). 0보다 크면 초상화 칸에 맞춰
            // 줄이지 않고, 캐릭터가 대기 모션과 같은 키로 보이게 칸 밖으로 크게 그린다(낫/검기가 큰 필살기용).
            public readonly float CastCharacterHeightPx;
            public BattleSkill(string key, string name, float gaugeCost, float power, string chart, string effectPath,
                string castPath, float finisherDuration, bool isUltimate = false, float castCharacterHeightPx = 0f)
            {
                CastCharacterHeightPx = castCharacterHeightPx;
                Key = key;
                Name = name;
                GaugeCost = gaugeCost;
                Power = power;
                Chart = chart;
                EffectPath = effectPath;
                CastPath = castPath;
                FinisherDuration = finisherDuration;
                IsUltimate = isUltimate;
            }
        }

        // 스킬 위력(Power)은 너무 세다는 피드백으로 모두 예전 값의 0.8배로 낮췄다(Q 2.0, W 1.5, D 1.75, E 2.75, R 3.0에서).
        private static readonly BattleSkill[] Skills =
        {
            // 묵직한 강타 몇 방 — 노드는 적지만 하나하나가 세다. 월드 Q는 시전 모션이 없어 기본 공격 모션으로 내지른다.
            new BattleSkill("Q", "어둠의 주먹", 40f, 1.6f, "U---U---U---D-------DD--U-------", "Art/Effects/DarkFist",
                "Art/Necrosia/Attack", 1.0f),
            // 빠른 4연타 묶음을 위아래로 몰아친다.
            new BattleSkill("W", "악마의 손", 20f, 1.2f, "UUUU-DDDD-UUUU-DDDD-------", "Art/Effects/DemonHand",
                "Art/Necrosia/WSkill", 1.2f),
            // 던질 때 패턴을 돌아올 때 거꾸로 한 번 더 — 갔다가 돌아오는 부메랑.
            new BattleSkill("D", "낫 부메랑", 30f, 1.4f, "UUD-D-------D-DUU-------UDU-------", "Art/Effects/ScytheSpin",
                "Art/Necrosia/DSkill", 1.1f),
            // 위아래로 쉴 새 없이 번갈아 휘감는 연타.
            new BattleSkill("E", "사슬 폭풍", 65f, 2.2f, "UDUDUD--DUDUDU--UUDDUUDD-------", "Art/Effects/ChainStormLoop",
                "Art/Necrosia/ESkill", 1.4f),
            // 궁극기 — 길고 화려하다. 전용 필살기 모션(Necrosia_Ult 12프레임: 낫을 치켜들어 내려찍고 수평으로 벤다).
            new BattleSkill("R", "죽음의 소용돌이", 100f, 2.4f, "UUU-DDD-UDUD----UUUU-DDDD-UDU-DUD-UUDD-UUDD--UUUUDDDD-------", "Art/Effects/DeathVortex",
                "Art/Necrosia/Ult", 2.0f, isUltimate: true, castCharacterHeightPx: 295f),
        };
        private readonly Sprite[][] skillCastFrames = new Sprite[Skills.Length][];
        // 궁극기 전용: 채보를 다 친 뒤 시전 모션이 나오기 직전에 한 번 재생되는, 눈을 번쩍 뜨는 연출.
        private const string EyeFlashPath = "Art/Necrosia/EyeFlash";
        private Sprite[] eyeFlashFrames;
        // 마무리 연출: 시전 모션이 시작되고 이만큼 뒤에 이펙트가 뜨고 첫 타가 들어간다. 마지막 타 뒤 잠깐 여운을 둔다.
        private const float SkillFinisherLeadIn = 0.2f;
        private const float SkillFinisherTail = 0.35f;
        private const float SkillFailPause = 0.8f;
        private const float SkillEffectFrameInterval = 0.06f;

        // 필살 게이지. 기본 공격 채보에서 노드를 맞힐 때만 찬다(막기·스킬 채보로는 안 찬다).
        // 일반 공격 채보 하나(노드 약 9개)를 전부 Perfect로 치면 약 22 — W는 1턴, R은 전부 Perfect여도 5턴쯤 모아야 쓴다
        // (3턴이면 차서 너무 빠르다는 피드백으로 낮췄다).
        // 전투가 시작될 때마다 0부터 다시 쌓는다.
        private const float MaxSkillGauge = 100f;
        private const float GaugePerPerfect = 2.5f;
        private const float GaugePerGood = 1.5f;
        private const float GaugePerOk = 0.75f;

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
        private RawImage battleBackground;
        private const string LunabelBattleBackgroundPath = "Backgrounds/HillOfDespair";
        private static readonly Color BattleBackgroundTint = new Color(0.55f, 0.5f, 0.5f, 1f);
        // 레인 뒤 어두운 띠: 배경 그림(1672x941) 픽셀 좌표. 위 레인 판정줄(y 633~665)부터 아래 레인(y 773~806)까지 넉넉히 덮는다.
        private const float LaneBackdropX0 = 330f, LaneBackdropY0 = 585f, LaneBackdropX1 = 1470f, LaneBackdropY1 = 885f;
        private const float LaneBackdropAlpha = 0.7f;
        private RawImage laneBackdrop;
        private static readonly Color NoteOutlineColor = new Color(0f, 0f, 0f, 0.85f);

        // 가운데는 검고 위아래 끝 15%에서 투명해지는 세로 그라데이션(1x64).
        private static Texture2D CreateLaneBackdropTexture()
        {
            const int height = 64;
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                float edge = Mathf.Min(v, 1f - v) / 0.15f;
                texture.SetPixel(0, y, new Color(0f, 0f, 0f, Mathf.Clamp01(edge)));
            }
            texture.Apply();
            return texture;
        }
        private Image battleSplashImage;
        private Sprite[] battleSplashFrames = System.Array.Empty<Sprite>();
        // 루나벨전 전용 FINAL BATTLE 연출(고딕 판타지 파이널 배틀 스프라이트, 10프레임): 핏빛 낫 두 자루가 내려와 X자로 교차하고
        // 그 위에 "FINAL BATTLE" 글자가 박힌다. 시트 칸 간격이 고르지 않아 프레임마다 X자 교차점(글자 프레임은 그림 가운데)을
        // 같은 500x400 캔버스 가운데에 맞춰 잘라 두었다. 글자가 가로로 넓어서 기본 연출(세로로 긴 칸)과 따로 화면 가운데의
        // 넓은 칸에 그리고, 마지막 프레임에서 잠깐 머문다.
        private const string LunabelSplashPath = "Art/Effects/LunabelBattleSplash";
        private const float LunabelSplashFrameInterval = 0.09f;
        private const float LunabelSplashHold = 0.5f;
        private Sprite[] lunabelSplashFrames = System.Array.Empty<Sprite>();
        private Image lunabelSplashImage;
        // 궁극기 각성 연출(암전 → 눈 뜨며 깜빡임) 동안 화면 전체를 덮는 검은 판. contentRoot의 맨 위라
        // 체력바/초상화/레인까지 다 가린다.
        private Image ultimateFlashOverlay;
        // 같은 연출 동안 EyeFlash 프레임을 화면 가득 보여주는 이미지(암전 판 바로 위에 그려진다).
        private Image ultimateEyeFullscreenImage;
        private Image playerPortrait;
        private Image monsterPortrait;
        private Image monsterHpFill;
        private Text monsterNameLabel;
        private Text monsterHpText;
        private Image playerHpFill;
        private Text playerHpText;
        private Text phaseBannerText;
        private Text resultText;

        // 턴이 바뀔 때 잠깐 뜨는 "My Turn"/"Enemy Turn" 로고 배너. 예전엔 phaseBannerText에
        // "나의 턴!"/"적의 턴!" 글자를 그냥 띄웠는데, 글자 상자가 작아 Overflow 모드 때문에
        // 긴 글자가 상자 밖으로 삐져나와 보였다. 전용 그림(UI/MyTurnBanner, UI/EnemyTurnBanner)이
        // 생긴 뒤로는 그 자리에 그림을 띄운다 — 그림 자체에 글자가 그려져 있어 더 이상 넘칠 일이 없다.
        private Image turnBannerImage;
        private Sprite myTurnBannerSprite;
        private Sprite enemyTurnBannerSprite;

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
            // 페이크 노드: SwitchTime 전까지는 ShownLane에 보이다가 그 뒤 Lane으로 미끄러진다. 보통 노드는 ShownLane == Lane.
            public int ShownLane;
            public float SwitchTime = float.MinValue;
            public Image Image;     // 화면에 떠 있는 동안만 풀에서 빌려온다.
            public bool Judged;
            public bool Announced; // RunChart의 onIncoming을 이미 불렀는지(루나벨의 피의 손 발사용)
            // 롱노트: EndTime(꼬리가 판정점에 닿는 시각) > HitTime. 머리를 제때 누르면 Holding이 되고, 꼬리까지 버티면 성공.
            public float EndTime;
            public bool IsHold => EndTime > HitTime;
            public bool Holding;
            public float HeadDiff;
            public float NextHoldSpark;
            public Image TailImage;
            public Image BarImage;
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

        // 절망의 언덕(루나벨전)에는 돌아갈 포탈이 없어서 도주를 쓸 수 없다. 행동 선택 창의 도주 칸 위에 붉은 사슬이 감기는
        // 잠금 그림(UI/FleeLock, Sprite/UI/Run icon lock 8프레임)을 겹쳐 그린다. 그 전투에서 처음 창이 열릴 때 한 번 사슬이
        // 감기는 애니메이션을 틀고, 그 뒤로는 마지막 프레임(붉은 눈)으로 잠긴 채 둔다.
        // 덮을 자리는 UI/BattleActionSelect(1448x1086) 속 도주 칸 테두리(x 1012~1265, y 420~686)에 맞췄다.
        private const string FleeLockPath = "UI/Battle/FleeLock/lock_";
        private const int FleeLockFrameCount = 8;
        private const float FleeLockFrameInterval = 0.09f;
        private const float FleeLockX0 = 1012f, FleeLockY0 = 420f, FleeLockX1 = 1265f, FleeLockY1 = 686f;
        private const float ActionSelectImageWidth = 1448f, ActionSelectImageHeight = 1086f;
        private RawImage fleeLockImage;
        private readonly Texture2D[] fleeLockFrames = new Texture2D[FleeLockFrameCount];
        private bool fleeLocked;
        private bool fleeLockAnimated;
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
        // 루나벨이 플레이어에게 준 피해를 플레이어 머리 위에 띄우는 핏빛 숫자.
        private readonly Sprite[] lunabelDigits = new Sprite[10];
        private const float LunabelDigitHeight = 72f;
        private RectTransform playerDamageNumberLayer;
        private Sprite[] lunabelPhaseTwoAttackFrames = System.Array.Empty<Sprite>();
        private RectTransform damageNumberLayer;
        private bool nextHitIsMiss;
        private Sprite[] monsterOneShot; // 재생 중인 공격/피격 모션. null이면 대기 반복.
        private float monsterOneShotInterval;
        private int monsterFrameIndex;
        private float monsterFrameTimer;
        private Vector2 monsterBaseWorldSize = Vector2.one; // 대기 첫 프레임의 월드 크기. 이게 칸에 꽉 차게 맞춘다.
        // 전용 대기 모션이 있는 몬스터(보스 기사)는 칸보다 조금 크게 그린다 — 대기 모션은 칸에 맞춰 줄면 공격 모션보다 작아 보였다.
        private const float DedicatedIdleDisplayScale = 1.1f;
        // 황금 모래룡은 원화가 가로로 길어(약 2:1) 칸 폭에 맞추면 키가 플레이어 초상화의 절반쯤밖에 안 됐다.
        // 몸통이 플레이어보다 커 보이게 크게 그린다(칸 밖 왼쪽으로 꼬리/모래가 넘쳐 보이는 게 맞다).
        private const float SandDragonDisplayScale = 2.2f;
        private float monsterDisplayScale = 1f;

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
        // 릴리의 리듬 대결(미니게임). 상대는 GameBootstrap.CreateMinigameOpponent가 만든 화면 밖의 몬스터라 경험치/드랍이 없다.
        // 플레이어는 체력을 가득 채우고 시작하고, 쓰러질 만큼 맞으면 체력 1로 버티며 진 것으로 끝난다(Character.SurviveLethalHit).
        // 끝나면 대결 전 체력으로 되돌리고 상대를 지운 뒤 결과(이겼는지)를 알려 준다. 루나벨전이어도 도주할 수 있고, 아이템은 쓸 수 없다.
        private bool isMinigame;
        private float minigameSavedHp;
        private System.Action<bool> minigameFinished;
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
            lunabelSplashFrames = Resources.LoadAll<Sprite>(LunabelSplashPath);
            System.Array.Sort(lunabelSplashFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            nodeBurstFrames = Resources.LoadAll<Sprite>("Art/Effects/NodeBurst");
            System.Array.Sort(nodeBurstFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            judgmentPerfect = Resources.Load<Sprite>("UI/Battle/Judgment/perfect");
            judgmentExcellent = Resources.Load<Sprite>("UI/Battle/Judgment/excellent");
            judgmentGood = Resources.Load<Sprite>("UI/Battle/Judgment/good");
            judgmentMiss = Resources.Load<Sprite>("UI/Battle/Judgment/miss");

            basicSlashFrames = Resources.LoadAll<Sprite>("Art/Effects/BasicSlash");
            System.Array.Sort(basicSlashFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            for (int d = 0; d < 10; d++)
            {
                normalDigits[d] = Resources.Load<Sprite>("UI/HUD/DamageDigits/normal_" + d);
                critDigits[d] = Resources.Load<Sprite>("UI/HUD/DamageDigits/crit_" + d);
            }

            for (int i = 0; i < Skills.Length; i++)
            {
                skillEffectFrames[i] = Resources.LoadAll<Sprite>(Skills[i].EffectPath);
                System.Array.Sort(skillEffectFrames[i], (a, b) => string.CompareOrdinal(a.name, b.name));
                skillCastFrames[i] = Resources.LoadAll<Sprite>(Skills[i].CastPath);
                System.Array.Sort(skillCastFrames[i], (a, b) => string.CompareOrdinal(a.name, b.name));
            }

            bossSpecialRaiseFrames = LoadSortedSprites(BossAI.SpecialRaisePath);
            bossSpecialChargeFrames = LoadSortedSprites(BossAI.SpecialChargePath);
            swordWaveFrames = LoadSortedSprites(BossAI.SwordWavePath);
            bloodHandFrames = LoadSortedSprites(BloodHandProjectile.FramesPath);
            lunabelPhaseTwoFrames = LoadSortedSprites(FinalBossAI.PhaseTwoPath);
            lunabelPhaseTwoIdleFrames = LoadSortedSprites(FinalBossAI.PhaseTwoIdlePath);
            var lunabelPhaseOneIdle = LoadSortedSprites(FinalBossAI.IdlePath);
            lunabelBaseIdleSprite = lunabelPhaseOneIdle.Length > 0 ? lunabelPhaseOneIdle[0] : null;
            lunabelSpecialFrames = LoadSortedSprites(FinalBossAI.SpecialPath);
            lunabelSpecialChargeFrames = LoadSortedSprites(FinalBossAI.SpecialChargePath);
            bloodOrbSprite = Resources.Load<Sprite>(FinalBossAI.BloodOrbPath + "/orb_1");
            lunabelThrowFrames = LoadSortedSprites(FinalBossAI.ThrowPath);
            lunabelPhaseTwoAttackFrames = LoadSortedSprites(FinalBossAI.PhaseTwoAttackPath);
            lunabelAbsorbFrames = LoadSortedSprites(FinalBossAI.AbsorbPath);
            for (int d = 0; d < 10; d++) lunabelDigits[d] = Resources.Load<Sprite>("UI/HUD/DamageDigits/lunabel_" + d);
            bloodPillarRiseFrames = LoadSortedSprites(FinalBossAI.BloodPillarRisePath);
            bloodPillarTornadoFrames = LoadSortedSprites(FinalBossAI.BloodPillarTornadoPath);
            sandTailFrames = LoadSortedSprites(SandTailPath);
            sandChargeFrames = LoadSortedSprites(SandChargePath);
            sandBeamFrames = LoadSortedSprites(SandBeamPath);
            bloodVortexFrames = LoadSortedSprites(FinalBossAI.BloodVortexPath);
            lunabelHitFrames = LoadSortedSprites(FinalBossAI.HitPath);
            lunabelTapSprite = Resources.Load<Sprite>(LunabelNodePath + "/tap");
            lunabelBarSprite = Resources.Load<Sprite>(LunabelNodePath + "/bar");
            lunabelTailSprite = Resources.Load<Sprite>(LunabelNodePath + "/tail");

            eyeFlashFrames = Resources.LoadAll<Sprite>(EyeFlashPath);
            System.Array.Sort(eyeFlashFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

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

            isMinigame = false;
            Begin(targetMonster, targetPlayer);
            return true;
        }

        // 릴리의 리듬 대결을 연다. onFinished(won)은 대결 창이 닫힌 뒤 불린다(도주하거나 지면 false).
        public bool BeginMinigame(Monster opponent, Character targetPlayer, System.Action<bool> onFinished)
        {
            if (IsOpen) return false;
            if (opponent == null || opponent.IsDead) return false;
            if (targetPlayer == null || targetPlayer.IsDead) return false;

            isMinigame = true;
            minigameFinished = onFinished;
            minigameSavedHp = targetPlayer.CurrentHp;
            targetPlayer.SurviveLethalHit = true;
            targetPlayer.Heal(targetPlayer.MaxHp);
            Begin(opponent, targetPlayer);
            return true;
        }

        private void Begin(Monster targetMonster, Character targetPlayer)
        {
            monster = targetMonster;
            player = targetPlayer;
            playerMovement = player.GetComponent<CharacterMovement2D>();

            isBossBattle = monster.GetComponent<BossAI>() != null || monster.GetComponent<BossSlimeAI>() != null || monster.GetComponent<SandDragonAI>() != null
                || monster.GetComponent<FinalBossAI>() != null;
            isLunabelBattle = monster.GetComponent<FinalBossAI>() != null;
            isSandDragonBattle = monster.GetComponent<SandDragonAI>() != null;
            sandDragonEnemyTurns = 0;
            battleBackground.gameObject.SetActive(isLunabelBattle && battleBackground.texture != null);
            laneBackdrop.gameObject.SetActive(battleBackground.gameObject.activeSelf);
            fleeLocked = isLunabelBattle && !isMinigame;
            fleeLockAnimated = false;
            lunabelPhaseOneTurns = 0;
            useLunabelNotes = false;
            StopBloodHands();
            isPractice = monster.GetComponent<TrainingDummy>() != null;
            practiceDamageTotal = 0f;
            telegraphDuration = isBossBattle ? BossTelegraphDuration : NormalTelegraphDuration;
            blockWindow = isBossBattle ? BossBlockWindow : NormalBlockWindow;
            perfectWindow = isBossBattle ? BossPerfectWindow : NormalPerfectWindow;
            goodWindow = isBossBattle ? BossGoodWindow : NormalGoodWindow;
            okWindow = isBossBattle ? BossOkWindow : NormalOkWindow;
            stepDuration = isBossBattle ? BossStepDuration : NormalStepDuration;
            baseTelegraphDuration = telegraphDuration;
            baseStepDuration = stepDuration;
            vanishNotes = false;
            lastBossPattern = null;
            lastPlayerPattern = null;
            battleSerial++;

            monsterNameLabel.text = monster.Data != null ? monster.Data.monsterName : "몬스터";
            if (isPractice) monsterNameLabel.text += " (연습)";
            if (isMinigame) monsterNameLabel.text += " (리듬 대결)";
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
            foreach (var note in notePool)
            {
                note.rectTransform.localScale = Vector3.one;
                note.gameObject.SetActive(false);
            }
            foreach (var bar in holdBarPool) bar.gameObject.SetActive(false);
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
            RestorePlayerPortraitLayout();
            StopSwordWave();
            StopBloodOrb();
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

            turnRoutine = StartCoroutine(IntroThenFirstTurn());
        }

        // 암전(알파 0→1) → "BATTLE!!" 슬램 애니메이션 재생 → 전투 UI 공개 → 첫 턴(보스는 적 턴, 그 외는 내 턴) 순서로 진행한다.
        private IEnumerator IntroThenFirstTurn()
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

            if (isLunabelBattle)
            {
                yield return LunabelNameCard();
                if (BattleEnded()) yield break;
            }

            if (isLunabelBattle && lunabelSplashFrames.Length > 0)
            {
                yield return PlayLunabelSplash();
            }
            else if (battleSplashFrames.Length > 0)
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
            if (isLunabelBattle)
            {
                yield return LunabelBattleOpening();
                if (BattleEnded()) yield break;
            }
            // 보스전은 보스가 먼저 치고, 일반 몬스터(와 연습장 허수아비)는 플레이어가 선공을 잡는다.
            turnRoutine = StartCoroutine(isBossBattle ? EnemyTurn() : PlayerTurn());
        }

        // 관리자 모드(AdminUI): 지금 싸우는 상대를 바로 쓰러뜨린다. 루나벨처럼 한 방에 안 죽게 버티는 상대도 그대로 쓰러진다.
        // 쓰러지면 평소처럼 OnDied → EndBattle 로 전투가 끝나고 보상/사망 연출이 이어진다. 연습장 허수아비는 쓰러지지 않는다.
        public bool AdminKillOpponent()
        {
            if (!IsOpen || monster == null || monster.IsDead || isPractice) return false;
            monster.SurviveLethalHit = false;
            monster.TakeDamage(monster.CurrentHp + monster.MaxHp);
            return true;
        }

        // 관리자 모드: 필살 게이지를 가득 채운다.
        public bool AdminFillSkillGauge()
        {
            if (!IsOpen) return false;
            skillGauge = MaxSkillGauge;
            RefreshHpBars();
            return true;
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
            float height = critical ? CritDigitHeight : miss ? MissDigitHeight : DamageDigitHeight;
            ShowDamageNumber(damageNumberLayer, critical ? critDigits : normalDigits, height, damage, critical, miss ? 0.6f : 1f);
        }

        // layer(칸) 위쪽 가장자리에서 digits 숫자 그림으로 데미지를 띄운다. 플레이어가 맞을 때(루나벨 숫자)도 쓴다.
        private void ShowDamageNumber(RectTransform layer, Sprite[] digits, float height, float damage, bool critical, float alpha = 1f)
        {
            string text = Mathf.Max(0, Mathf.RoundToInt(damage)).ToString();

            var go = new GameObject("DamageNumber", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(layer, false);
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

            StartCoroutine(DamageNumberRoutine(rect, images, alpha, critical));
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
            if (monsterStaggerRoutine != null)
            {
                StopCoroutine(monsterStaggerRoutine);
                monsterStaggerRoutine = null;
            }
            if (monsterPortrait != null)
            {
                monsterPortrait.color = Color.white;
                monsterPortrait.rectTransform.anchoredPosition = Vector2.zero;
            }
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

            bool wasMinigame = isMinigame;
            bool wonMinigame = monster != null && monster.IsDead;
            Monster minigameOpponent = monster;
            Character minigamePlayer = player;

            if (fled && monster != null && player != null && !isMinigame)
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

            if (wasMinigame)
            {
                isMinigame = false;
                if (minigamePlayer != null)
                {
                    minigamePlayer.SurviveLethalHit = false;
                    minigamePlayer.RestoreHp(minigameSavedHp);
                }
                if (minigameOpponent != null) Destroy(minigameOpponent.gameObject);
                var finished = minigameFinished;
                minigameFinished = null;
                finished?.Invoke(wonMinigame);
            }
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
        // 전투 화면(고정 크기)을 지금 캔버스 안에 비율 그대로 꽉 차게 맞춘다. 남는 위아래/양옆은 암전 판이 가린다.
        private void FitContentToScreen()
        {
            if (contentRoot == null || panel == null) return;
            var parentRect = ((RectTransform)panel.transform).rect;
            var contentRect = (RectTransform)contentRoot.transform;
            Vector2 size = contentRect.sizeDelta;
            if (size.x <= 0f || size.y <= 0f || parentRect.width <= 0f || parentRect.height <= 0f) return;
            float scale = Mathf.Min(parentRect.width / size.x, parentRect.height / size.y);
            contentRect.localScale = new Vector3(scale, scale, 1f);
        }

        private void Update()
        {
            if (IsOpen) FitContentToScreen();
            if (!IsOpen || playingFieldIntro) return;

            if (MinigameLost)
            {
                EndBattle(fled: false);
                return;
            }

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
            else if (walk != null && walk.HasIdleFrames) monsterIdleFrames = walk.IdleFrames;
            var finalBossAI = monster.GetComponent<FinalBossAI>();
            if (finalBossAI != null && finalBossAI.PhaseTwoStarted && lunabelPhaseTwoAttackFrames.Length > 0)
                monsterAttackFrames = lunabelPhaseTwoAttackFrames;
            if (finalBossAI != null && !finalBossAI.PhaseTwoStarted) monsterHitFrames = lunabelHitFrames;
            else if (walk != null && walk.HasFrames) monsterIdleFrames = walk.Frames;
            else if (worldSprite != null && worldSprite.sprite != null) monsterIdleFrames = new[] { worldSprite.sprite };
            else monsterIdleFrames = System.Array.Empty<Sprite>();

            monsterOneShot = null;
            monsterHoldFrames = null;
            monsterFrameIndex = 0;
            monsterFrameTimer = 0f;
            monsterPortrait.enabled = monsterIdleFrames.Length > 0;
            if (!monsterPortrait.enabled) return;

            // 루나벨은 2페이즈에서 다시 만나도 1페이즈 대기 크기를 기준으로 그린다. 2페이즈 대기를 기준으로 삼으면 칸에 맞춰
            // 줄어들어서, 크게 그린 2페이즈 모습이 1페이즈와 같은 크기로 보여 버린다.
            monsterBaseWorldSize = WorldSize(finalBossAI != null && lunabelBaseIdleSprite != null ? lunabelBaseIdleSprite : monsterIdleFrames[0]);
            monsterDisplayScale = dummy == null && walk != null && walk.HasIdleFrames ? DedicatedIdleDisplayScale : 1f;
            if (isSandDragonBattle) monsterDisplayScale = SandDragonDisplayScale;
            SetMonsterFrame(monsterIdleFrames[0]);
        }

        private static Vector2 WorldSize(Sprite sprite) => sprite.rect.size / sprite.pixelsPerUnit;

        private void SetMonsterFrame(Sprite sprite)
        {
            // 대기 첫 프레임이 칸 안에 꽉 차게 들어가는 배율을 기준으로, 공격 프레임도 같은 배율로 그린다
            // (공격 모션이 칸보다 크면 옆으로 삐져나오는 게 맞다 — 월드에서도 그만큼 크게 휘두르니까).
            // 전투 시작 시점엔 패널이 꺼져 있을 수 있어서 칸 크기는 그릴 때마다 다시 읽는다.
            Vector2 stageSize = monsterStage.rect.size;
            float unitsToCanvas = Mathf.Min(stageSize.x / monsterBaseWorldSize.x, stageSize.y / monsterBaseWorldSize.y) * monsterDisplayScale;
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

        // 보스 필살기의 기운 모으기처럼, 대기 대신 잠깐 반복할 프레임. null이면 평소 대기 모션.
        private Sprite[] monsterHoldFrames;
        private float monsterHoldInterval;

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
                SetMonsterFrame(monsterHoldFrames != null && monsterHoldFrames.Length > 0 ? monsterHoldFrames[0] : monsterIdleFrames[0]);
                return;
            }

            if (monsterHoldFrames != null && monsterHoldFrames.Length > 0)
            {
                if (monsterFrameTimer < monsterHoldInterval) return;
                monsterFrameTimer -= monsterHoldInterval;
                monsterFrameIndex = (monsterFrameIndex + 1) % monsterHoldFrames.Length;
                SetMonsterFrame(monsterHoldFrames[monsterFrameIndex]);
                return;
            }

            if (monsterIdleFrames.Length < 2 || monsterFrameTimer < MonsterIdleFrameInterval) return;
            monsterFrameTimer -= MonsterIdleFrameInterval;
            monsterFrameIndex = (monsterFrameIndex + 1) % monsterIdleFrames.Length;
            SetMonsterFrame(monsterIdleFrames[monsterFrameIndex]);
        }

        private bool BattleEnded() => !IsOpen || monster == null || monster.IsDead || player == null || player.IsDead || MinigameLost;

        // 리듬 대결에서는 쓰러지지 않고 체력 1로 버티므로, 체력이 1까지 떨어지면 진 것이다.
        private bool MinigameLost => isMinigame && player != null && player.CurrentHp <= 1f;
    }
}
