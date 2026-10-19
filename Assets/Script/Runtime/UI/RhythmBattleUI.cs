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
        private const string LunabelNodePath = "UI/LunabelNode";
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
        private Image battleSplashImage;
        private Sprite[] battleSplashFrames = System.Array.Empty<Sprite>();
        // 루나벨전 전용 BATTLE 연출(Final_Battle 8프레임): 핏빛 글자에서 피가 점점 흘러내린다. 글자가 가로로 넓어서
        // 기본 연출(세로로 긴 칸)과 따로 화면 가운데의 넓은 칸에 그리고, 피가 다 흘러내린 마지막 프레임에서 잠깐 머문다.
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
        private const string FleeLockPath = "UI/FleeLock/lock_";
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
            for (int d = 0; d < 10; d++) lunabelDigits[d] = Resources.Load<Sprite>("UI/DamageDigits/lunabel_" + d);
            bloodPillarRiseFrames = LoadSortedSprites(FinalBossAI.BloodPillarRisePath);
            bloodPillarTornadoFrames = LoadSortedSprites(FinalBossAI.BloodPillarTornadoPath);
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

            Begin(targetMonster, targetPlayer);
            return true;
        }

        private void Begin(Monster targetMonster, Character targetPlayer)
        {
            monster = targetMonster;
            player = targetPlayer;
            playerMovement = player.GetComponent<CharacterMovement2D>();

            isBossBattle = monster.GetComponent<BossAI>() != null || monster.GetComponent<BossSlimeAI>() != null
                || monster.GetComponent<FinalBossAI>() != null;
            isLunabelBattle = monster.GetComponent<FinalBossAI>() != null;
            battleBackground.gameObject.SetActive(isLunabelBattle && battleBackground.texture != null);
            fleeLocked = isLunabelBattle;
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
                lunabelSplashImage.gameObject.SetActive(true);
                foreach (var frame in lunabelSplashFrames)
                {
                    lunabelSplashImage.sprite = frame;
                    yield return new WaitForSecondsRealtime(LunabelSplashFrameInterval);
                }
                yield return new WaitForSecondsRealtime(LunabelSplashHold);
                lunabelSplashImage.gameObject.SetActive(false);
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

        private bool BattleEnded() => !IsOpen || monster == null || monster.IsDead || player == null || player.IsDead;

        // 턴 시작마다 그 턴의 로고 배너를 잠깐 띄웠다 지운다.
        private IEnumerator ShowTurnBanner(Sprite sprite)
        {
            turnBannerImage.sprite = sprite;
            turnBannerImage.gameObject.SetActive(sprite != null);
            yield return new WaitForSecondsRealtime(PhaseBannerDuration);
            turnBannerImage.gameObject.SetActive(false);
        }

        private IEnumerator EnemyTurn()
        {
            yield return ShowTurnBanner(enemyTurnBannerSprite);

            if (BattleEnded()) yield break;

            var bossAI = monster.GetComponent<BossAI>();
            if (bossAI != null && bossAI.ShouldUseSpecialMove && bossSpecialRaiseFrames.Length > 0 && bossSpecialChargeFrames.Length > 0)
            {
                int specialIndex = bossAI.SpecialMovesUsed;
                bossAI.MarkSpecialMoveUsed();
                yield return BossSpecialMove(specialIndex);
                if (BattleEnded()) yield break;
                yield return new WaitForSecondsRealtime(AfterChartPause);
                if (BattleEnded()) yield break;
                turnRoutine = StartCoroutine(PlayerTurn());
                yield break;
            }

            var finalBoss = monster.GetComponent<FinalBossAI>();
            if (finalBoss != null && finalBoss.ShouldUseSpecialMove
                && lunabelSpecialFrames.Length > 0 && lunabelSpecialChargeFrames.Length > 0)
            {
                int specialIndex = finalBoss.SpecialMovesUsed;
                finalBoss.MarkSpecialMoveUsed();
                yield return LunabelSpecialMove(specialIndex);
                if (BattleEnded()) yield break;
                yield return new WaitForSecondsRealtime(AfterChartPause);
                if (BattleEnded()) yield break;
                turnRoutine = StartCoroutine(PlayerTurn());
                yield break;
            }

            if (finalBoss != null && finalBoss.ShouldStartPhaseTwo)
            {
                // 2페이즈 변신 연출 뒤, 같은 적 턴에 곧바로 2페이즈 패턴을 친다.
                yield return LunabelPhaseTwo(finalBoss);
                if (BattleEnded()) yield break;
            }
            else if (finalBoss != null && !finalBoss.PhaseTwoStarted && ShouldLunabelAbsorb())
            {
                // 1페이즈 전용: 3번째 적 턴마다 피를 흡수해 체력을 채우는 턴. 이후 루나벨의 노드를 일정 개수 놓치면 피의 회오리가 터진다.
                yield return LunabelBloodAbsorb(finalBoss);
                if (BattleEnded()) yield break;
                yield return new WaitForSecondsRealtime(AfterChartPause);
                if (BattleEnded()) yield break;
                turnRoutine = StartCoroutine(PlayerTurn());
                yield break;
            }

            if (isBossBattle)
            {
                yield return BossPatternTurn();
                if (BattleEnded()) yield break;
                // 피가 들끓는 동안 놓친 노드가 문턱에 닿았으면 이 적 턴 끝에 곧바로 피의 회오리가 터진다.
                if (finalBoss != null && finalBoss.BloodCharged && finalBoss.BloodMisses >= LunabelBloodTornadoMissThreshold)
                {
                    finalBoss.BloodCharged = false;
                    finalBoss.BloodMisses = 0;
                    yield return new WaitForSecondsRealtime(AfterChartPause);
                    if (BattleEnded()) yield break;
                    yield return LunabelBloodTornado();
                    if (BattleEnded()) yield break;
                }
                yield return new WaitForSecondsRealtime(AfterChartPause);
                if (BattleEnded()) yield break;
                turnRoutine = StartCoroutine(PlayerTurn());
                yield break;
            }

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

        // 루나벨은 2페이즈로 각성한 뒤로 계속 분노 상태다(흡혈로 체력이 절반 위로 다시 차도 풀리지 않는다).
        private bool BossEnraged
        {
            get
            {
                if (monster == null) return false;
                var finalBoss = monster.GetComponent<FinalBossAI>();
                if (finalBoss != null) return finalBoss.PhaseTwoStarted;
                return monster.CurrentHp <= monster.MaxHp * BossEnrageHealthFraction;
            }
        }

        // ---- 루나벨 2페이즈 ----
        // 체력이 절반 이하로 처음 내려간 뒤의 적 턴: 대사 → 변신(Phase2 9프레임, 마지막 프레임에서 잠깐 멈춤) → 체력을 모두
        // 채우고 "2페이즈" 배너. 이후로는 공중에 뜬 2페이즈 대기 모션(Phase2Idle)으로 서 있고, 분노 상태(노드가 더 빠르고 아프며, 분노 전용 패턴과
        // 연계 공격)로 싸운다.
        private const float LunabelTransformFrameInterval = 0.15f;
        private const float LunabelPhaseLinePause = 1.3f;
        private const float LunabelTransformHold = 0.5f;
        private const float LunabelPhaseBannerPause = 1.4f;
        private Sprite[] lunabelPhaseTwoFrames = System.Array.Empty<Sprite>();
        private Sprite[] lunabelPhaseTwoIdleFrames = System.Array.Empty<Sprite>();
        private Sprite lunabelBaseIdleSprite;
        // 맞는 모션은 1페이즈(변신 전) 모습으로 그려져 있어서, 변신 전에만 쓴다.
        private Sprite[] lunabelHitFrames = System.Array.Empty<Sprite>();

        private IEnumerator LunabelPhaseTwo(FinalBossAI finalBoss)
        {
            phaseBannerText.color = EnemyNoteColor;
            phaseBannerText.text = "루나벨: \"후후... 그날 밤보다는 제법이네. 하지만 결말은 똑같아. 네 피, 전부 받아 갈게.\"";
            yield return new WaitForSecondsRealtime(LunabelPhaseLinePause);
            if (BattleEnded()) yield break;

            // 2페이즈 대기 모션이 없으면 변신 마지막 프레임(뿔과 날개가 돋은 모습)으로 서 있는다.
            Sprite[] phaseTwoIdle = lunabelPhaseTwoIdleFrames.Length > 0 ? lunabelPhaseTwoIdleFrames
                : lunabelPhaseTwoFrames.Length > 0 ? new[] { lunabelPhaseTwoFrames[lunabelPhaseTwoFrames.Length - 1] }
                : System.Array.Empty<Sprite>();
            if (lunabelPhaseTwoFrames.Length > 0)
            {
                phaseBannerText.text = "루나벨의 몸에서 붉은 기운이 터져 나온다...";
                // 변신 프레임을 한 번 재생한 뒤, 끝나면 2페이즈 대기 모션으로 넘어간다.
                monsterIdleFrames = phaseTwoIdle;
                monsterHitFrames = System.Array.Empty<Sprite>();
                PlayMonsterOneShot(lunabelPhaseTwoFrames, LunabelTransformFrameInterval, restart: true);
                yield return new WaitForSecondsRealtime(LunabelTransformFrameInterval * lunabelPhaseTwoFrames.Length + LunabelTransformHold);
                if (BattleEnded()) yield break;
            }

            finalBoss.EnterPhaseTwo(phaseTwoIdle);
            if (lunabelPhaseTwoAttackFrames.Length > 0) monsterAttackFrames = lunabelPhaseTwoAttackFrames;
            RefreshHpBars();
            phaseBannerText.color = EnragedNoteColor;
            phaseBannerText.text = "2페이즈 - 피의 여왕 루나벨이 각성했다! 체력을 모두 회복했다!";
            ShowResult("2페이즈!", EnragedNoteColor);
            yield return new WaitForSecondsRealtime(LunabelPhaseBannerPause);
            phaseBannerText.text = "";
        }

        // 패턴 하나를 치고, 분노 상태면 확률적으로 다른 패턴 하나를 곧바로 이어 친다.
        private IEnumerator BossPatternTurn()
        {
            yield return RunBossPattern(PickBossPattern(), combo: false);
            if (BattleEnded()) yield break;

            if (BossEnraged && Random.value < BossComboChance)
            {
                yield return new WaitForSecondsRealtime(BossComboPause);
                if (BattleEnded()) yield break;
                yield return RunBossPattern(PickBossPattern(), combo: true);
            }
        }

        private BossPattern PickBossPattern()
        {
            bool enraged = BossEnraged;
            var candidates = new List<BossPattern>();
            foreach (var pattern in BossPatterns)
            {
                if (pattern.EnragedOnly && !enraged) continue;
                // 루나벨은 노드가 붉은 안개 속으로 사라지는 패턴(그림자 손아귀)을 쓰지 않는다.
                if (pattern.Vanish && isLunabelBattle) continue;
                if (pattern == lastBossPattern) continue;
                candidates.Add(pattern);
            }
            var picked = candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : BossPatterns[0];
            lastBossPattern = picked;
            return picked;
        }

        private IEnumerator RunBossPattern(BossPattern pattern, bool combo)
        {
            bool enraged = BossEnraged;
            string prefix = combo ? "연계! " : enraged ? "[분노] " : "";
            phaseBannerText.color = enraged ? EnragedNoteColor : EnemyNoteColor;
            bool blood = isLunabelBattle && pattern.BloodName != null;
            phaseBannerText.text = $"{prefix}{(blood ? pattern.BloodName : pattern.Name)} - {(blood ? pattern.BloodHint : pattern.Hint)}";
            var chargedBoss = isLunabelBattle ? monster.GetComponent<FinalBossAI>() : null;
            if (chargedBoss != null && chargedBoss.BloodCharged)
                phaseBannerText.text += $"  [피의 공명 {chargedBoss.BloodMisses}/{LunabelBloodTornadoMissThreshold}]";
            yield return new WaitForSecondsRealtime(BossPatternAnnounceDuration);
            if (BattleEnded()) yield break;

            float baseDamage = isLunabelBattle && pattern.BloodDamageMultiplier > 0f ? pattern.BloodDamageMultiplier : pattern.DamageMultiplier;
            float damageScale = baseDamage * (enraged ? BossEnrageDamageScale : 1f);
            float approachScale = isLunabelBattle && pattern.BloodApproachScale > 0f ? pattern.BloodApproachScale : pattern.ApproachScale;
            float stepScale = isLunabelBattle && pattern.BloodStepScale > 0f ? pattern.BloodStepScale : pattern.StepScale;
            telegraphDuration = baseTelegraphDuration * approachScale * (enraged ? BossEnrageApproachScale : 1f);
            stepDuration = baseStepDuration * stepScale;
            vanishNotes = pattern.Vanish;
            useLunabelNotes = isLunabelBattle && lunabelTapSprite != null;

            var notes = BuildChartNotes(pattern.Charts[Random.Range(0, pattern.Charts.Length)]);
            // 루나벨이 피를 흡수해 들끓는 동안에는 놓친 노드를 센다(문턱에 닿으면 적 턴 끝에 피의 회오리).
            var bloodBoss = chargedBoss;
            yield return RunChart(notes, enraged ? EnragedNoteColor : EnemyNoteColor, blockWindow, (note, hit, diff) =>
            {
                if (!isLunabelBattle) PlayMonsterOneShot(monsterAttackFrames, MonsterAttackFrameInterval, restart: false);
                if (hit)
                {
                    TriggerPlayerGuardAnim();
                    ShowResult("막기 성공!", GoodColor);
                }
                else
                {
                    float damage = CombatMath.PhysicalDamage(monster.Attack, player.Stats.defense) * damageScale;
                    HurtPlayer(damage);
                    string bloodGauge = "";
                    if (bloodBoss != null && bloodBoss.BloodCharged)
                    {
                        bloodBoss.BloodMisses = Mathf.Min(LunabelBloodTornadoMissThreshold, bloodBoss.BloodMisses + 1);
                        bloodGauge = bloodBoss.BloodMisses >= LunabelBloodTornadoMissThreshold
                            ? " 피의 공명 가득!"
                            : $" 피의 공명 {bloodBoss.BloodMisses}/{LunabelBloodTornadoMissThreshold}";
                    }
                    if (pattern.Drain)
                    {
                        monster.Heal(damage * BossDrainRatio);
                        ShowResult($"피격! -{damage:0} (보스 흡혈 +{damage * BossDrainRatio:0}){bloodGauge}", MissColor);
                    }
                    else
                    {
                        ShowResult($"피격! -{damage:0}{bloodGauge}", MissColor);
                    }
                }
                RefreshHpBars();
            }, isLunabelBattle ? LaunchBloodHand : null, BloodHandTravelTime);

            telegraphDuration = baseTelegraphDuration;
            stepDuration = baseStepDuration;
            vanishNotes = false;
            useLunabelNotes = false;
            phaseBannerText.text = "";
        }

        // 루나벨이 손을 뻗고(공격 모션), 피의 손 하나가 BloodHandTravelTime에 걸쳐 플레이어에게 날아간다.
        private void LaunchBloodHand(ChartNote note)
        {
            PlayMonsterOneShot(monsterAttackFrames, MonsterAttackFrameInterval, restart: true);
            if (bloodHandFrames.Length == 0) return;
            for (int i = 0; i < BloodHandPoolSize; i++)
            {
                if (bloodHandPool[i].gameObject.activeSelf) continue;
                bloodHandRoutines[i] = StartCoroutine(BloodHandRoutine(i));
                return;
            }
        }

        private IEnumerator BloodHandRoutine(int index)
        {
            var image = bloodHandPool[index];
            var rect = image.rectTransform;
            image.color = Color.white;
            image.sprite = bloodHandFrames[0];
            image.gameObject.SetActive(true);

            float t = 0f;
            float frameTimer = 0f;
            int frame = 0;
            while (t < BloodHandTravelTime)
            {
                rect.anchoredPosition = ImageToCanvasPos(Mathf.Lerp(BloodHandStartX, BloodHandEndX, t / BloodHandTravelTime), BloodHandCenterY);
                frameTimer += Time.unscaledDeltaTime;
                if (frameTimer >= BloodHandFrameInterval)
                {
                    frameTimer -= BloodHandFrameInterval;
                    frame = (frame + 1) % bloodHandFrames.Length;
                    image.sprite = bloodHandFrames[frame];
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // 플레이어에게 닿으면 짧게 사라진다(막았든 맞았든 결과는 판정 쪽에서 보여 준다).
            for (float f = 0f; f < BloodHandFadeTime; f += Time.unscaledDeltaTime)
            {
                image.color = new Color(1f, 1f, 1f, 1f - f / BloodHandFadeTime);
                yield return null;
            }
            image.gameObject.SetActive(false);
            bloodHandRoutines[index] = null;
        }

        private void StopBloodHands()
        {
            for (int i = 0; i < BloodHandPoolSize; i++)
            {
                if (bloodHandRoutines[i] != null)
                {
                    StopCoroutine(bloodHandRoutines[i]);
                    bloodHandRoutines[i] = null;
                }
                if (bloodHandPool[i] != null) bloodHandPool[i].gameObject.SetActive(false);
            }
        }

        // ---- 보스 필살기 ----
        // 검을 들어올림(한 번) → 기운 모으기(반복 유지)하는 동안 "기운 흩트리기" 채보가 흐른다. 플레이어가 노드를 쳐서
        // 기운을 흩트리는데, 노드의 BossSpecialCancelRatio 이상을 맞히면 보스가 주춤하며 필살기가 캔슬되고,
        // 그보다 많이 놓치면 모은 기운을 기본 공격 모션과 함께 검기로 날려 큰 피해를 준다.
        private const string BossSpecialName = "심연의 검기";
        // 두 번째 필살기(체력 20% 이하)는 페이크 노드가 섞인 더 어려운 채보로 나온다.
        private static readonly string[] BossSpecialChargeCharts =
        {
            "UDUD-u====-DDUU-UDU-d====--UUDD-DUDU---",
            "UDUD-A-u====-BDUU-UDAU-d====-A-UUDD-DBDU-UDUD---",
        };
        private const float BossSpecialCancelRatio = 0.7f;
        // 검기 피해 = 평소 노드 한 타(몬스터 공격력 기준) x 이 배수.
        private const float BossSpecialWaveDamageMultiplier = 5f;
        private const float BossSpecialRaiseInterval = 0.14f;
        private const float BossSpecialChargeInterval = 0.1f;
        // 채보가 시작되기 전, 기운을 모으는 모습을 잠깐 먼저 보여 준다.
        private const float BossSpecialChargeLeadIn = 0.6f;
        private const float BossSpecialStaggerDuration = 1.0f;
        private const float BossSpecialStaggerShake = 14f; // 캔버스 단위
        private const float BossSpecialWaveTravelTime = 0.55f;
        private const float BossSpecialAfterWavePause = 0.6f;
        // 공격 모션이 휘두르는 프레임(3번째)쯤에 검기가 출발한다.
        private const float BossSpecialWaveLaunchDelay = MonsterAttackFrameInterval * 2f;
        private const float SwordWaveBurstInterval = 0.08f;
        private const float SwordWaveFrameInterval = 0.06f;
        // 검기 크기/경로(배경 그림 픽셀 좌표). 몬스터 칸(1080~1380) 앞에서 출발해 플레이어 초상화(270~530)까지 바닥을 타고 간다.
        private const float SwordWaveWidth = 300f;
        private const float SwordWaveHeight = 250f;
        private const float SwordWaveStartX = 1150f;
        private const float SwordWaveEndX = 420f;
        private const float SwordWaveBottomY = 525f;

        private Sprite[] bossSpecialRaiseFrames = System.Array.Empty<Sprite>();
        private Sprite[] bossSpecialChargeFrames = System.Array.Empty<Sprite>();
        private Sprite[] swordWaveFrames = System.Array.Empty<Sprite>();
        private Image swordWaveImage;
        private Coroutine swordWaveRoutine;
        private Coroutine monsterStaggerRoutine;

        private IEnumerator BossSpecialMove(int specialIndex)
        {
            phaseBannerText.color = EnemyNoteColor;
            phaseBannerText.text = "보스 기사가 검을 들어올린다...";
            PlayMonsterOneShot(bossSpecialRaiseFrames, BossSpecialRaiseInterval, restart: true);
            yield return new WaitForSecondsRealtime(BossSpecialRaiseInterval * bossSpecialRaiseFrames.Length);
            if (BattleEnded()) yield break;

            phaseBannerText.text = "어둠의 기운이 모여든다! 노드를 맞혀 흩트려라!";
            monsterHoldFrames = bossSpecialChargeFrames;
            monsterHoldInterval = BossSpecialChargeInterval;
            monsterOneShot = null;
            monsterFrameIndex = 0;
            monsterFrameTimer = 0f;
            SetMonsterFrame(monsterHoldFrames[0]);
            yield return new WaitForSecondsRealtime(BossSpecialChargeLeadIn);
            if (BattleEnded()) yield break;

            int hits = 0;
            var notes = BuildChartNotes(BossSpecialChargeCharts[Mathf.Clamp(specialIndex, 0, BossSpecialChargeCharts.Length - 1)]);
            yield return RunChart(notes, SkillNoteColor, okWindow, (note, hit, diff) =>
            {
                if (hit)
                {
                    hits++;
                    TriggerPlayerAttackAnim();
                    ShowResult("기운을 흩트렸다!", GoodColor);
                }
                else
                {
                    ShowResult("놓쳤다...", MissColor);
                }
            });
            if (BattleEnded()) yield break;

            float hitRatio = notes.Count > 0 ? (float)hits / notes.Count : 1f;
            if (hitRatio >= BossSpecialCancelRatio)
            {
                // 캔슬: 기운이 흩어지고 보스가 휘청인다.
                monsterHoldFrames = null;
                monsterOneShot = null;
                monsterFrameIndex = 0;
                monsterFrameTimer = 0f;
                SetMonsterFrame(monsterIdleFrames[0]);
                phaseBannerText.text = $"보스 기사가 주춤했다! 필살기 캔슬! ({hits}/{notes.Count})";
                ShowResult("캔슬 성공!", GoodColor);
                PlayMonsterStagger();
                yield return new WaitForSecondsRealtime(BossSpecialStaggerDuration);
                phaseBannerText.text = "";
                yield break;
            }

            // 실패: 모은 기운을 검기로 날린다.
            phaseBannerText.text = $"{BossSpecialName}! ({hits}/{notes.Count})";
            monsterHoldFrames = null;
            yield return FireSwordWaveAtPlayer(BossSpecialWaveDamageMultiplier, $"{BossSpecialName}에 베였다!");
        }

        // 기본 공격 모션과 함께 검기를 날려, 닿는 순간 막을 기회 없이 (노드 한 타 x damageMultiplier) 피해를 준다.
        // 필살기 실패와 보스전 도주 차단이 같이 쓴다. 끝나면 배너를 지운다.
        private IEnumerator FireSwordWaveAtPlayer(float damageMultiplier, string hitMessage)
        {
            PlayMonsterOneShot(monsterAttackFrames, MonsterAttackFrameInterval, restart: true);
            yield return new WaitForSecondsRealtime(BossSpecialWaveLaunchDelay);
            if (BattleEnded()) yield break;

            LaunchSwordWave(BossSpecialWaveTravelTime);
            yield return new WaitForSecondsRealtime(BossSpecialWaveTravelTime);
            if (BattleEnded()) yield break;

            float damage = CombatMath.PhysicalDamage(monster.Attack, player.Stats.defense) * damageMultiplier;
            player.TakeDamage(damage, monster.transform.position, hitReaction: false);
            TriggerPlayerHitAnim();
            ShowResult($"{hitMessage} -{damage:0}", MissColor);
            RefreshHpBars();
            yield return new WaitForSecondsRealtime(BossSpecialAfterWavePause);
            phaseBannerText.text = "";
        }

        // ---- 루나벨 2페이즈 필살기 ----
        // 하늘로 손을 뻗어 마법진을 여는 모션(Special 7프레임)을 한 번 재생 → 머리 위에 핏빛 구체를 모으는 모션(Charge
        // 8프레임)을 반복하는 동안 "기운 흩트리기" 채보가 흐른다. 캔슬은 없고, 놓친 노드 비율만큼 위력이 실린다:
        // 하나라도 놓치면 구체를 던지는 모션(Throw 5프레임)을 끝까지 재생한 뒤 구체를 던진다. 구체는 플레이어 발밑에
        // 떨어져 스며들고, 그 자리에서 피의 기둥이 솟구친 뒤(BloodPillarRise 한 번) 회오리치며(BloodPillarTornado 반복)
        // 여러 번 나눠 피해를 준다. 총 피해 = 최대 위력(LunabelSpecialPillarDamageMultiplier) x 놓친 노드 / 전체 노드.
        // 전부 맞히면 구체가 완전히 흩어져 피해 없이 끝난다.
        private const string LunabelSpecialName = "진홍의 심장";
        // 두 번째 필살기(2페이즈 체력 25% 이하)는 페이크 노드가 섞인 더 어려운 채보로 나온다.
        private static readonly string[] LunabelSpecialChargeCharts =
        {
            "DUDU-u====-UUDD-DUD-d====--DDUU-UDUD---",
            "DUDU-B-d====-AUDD-DUBD-u====-B-DDUU-UADU-DUDU---",
        };
        private const float LunabelSpecialRaiseInterval = 0.14f;
        private const float LunabelSpecialChargeInterval = 0.1f;
        private const float LunabelSpecialChargeLeadIn = 0.6f;
        private const float LunabelThrowFrameInterval = 0.1f;

        // 구체 경로(배경 그림 픽셀 좌표). 던지기 마지막 프레임에서 루나벨이 내뻗은 손끝에서 출발해, 낮은 포물선을 그리며
        // 플레이어 초상화(270~530) 발밑에 떨어진다. 떨어지면 작아지며 땅으로 스며든다.
        private const float BloodOrbStartX = 1100f;
        private const float BloodOrbStartY = 375f;
        private const float BloodOrbEndX = 400f;
        private const float BloodOrbEndY = 500f;
        private const float BloodOrbArcHeight = 70f;
        private const float BloodOrbStartSize = 80f;
        private const float BloodOrbEndSize = 150f;
        private const float BloodOrbTravelTime = 0.55f;
        private const float BloodOrbSinkTime = 0.2f;
        private const float BloodOrbSpinSpeed = 300f; // 도/초

        // 피의 기둥: 플레이어 발밑(배경 그림 픽셀 좌표)을 바닥 가운데로 솟구친다. 원화 1px을 그림 0.65px로 그려서
        // 다 솟구친 기둥(원화 키 약 575px)이 플레이어 초상화(키 260)를 넉넉히 집어삼키게 한다.
        private const float BloodPillarX = 400f;
        private const float BloodPillarBottomY = 515f;
        private const float BloodPillarScale = 0.65f;
        private const float BloodPillarRiseInterval = 0.07f;
        private const float BloodPillarTornadoInterval = 0.07f;
        private const float BloodPillarTornadoDuration = 1.6f;
        private const float BloodPillarFadeTime = 0.25f;
        // 회오리 피해: 노드 한 타(몬스터 공격력 기준) x LunabelSpecialPillarDamageMultiplier를 LunabelSpecialPillarHits번에 나눠 준다.
        private const int LunabelSpecialPillarHits = 8;
        private const float LunabelSpecialPillarDamageMultiplier = 6f;
        private const float LunabelSpecialAfterPause = 0.5f;

        private Sprite[] lunabelSpecialFrames = System.Array.Empty<Sprite>();
        private Sprite[] lunabelSpecialChargeFrames = System.Array.Empty<Sprite>();
        private Sprite[] lunabelThrowFrames = System.Array.Empty<Sprite>();
        private Sprite[] bloodPillarRiseFrames = System.Array.Empty<Sprite>();
        private Sprite[] bloodPillarTornadoFrames = System.Array.Empty<Sprite>();
        private Sprite bloodOrbSprite;
        private Image bloodOrbImage;
        private Image bloodPillarImage;
        private Coroutine bloodOrbRoutine;

        private IEnumerator LunabelSpecialMove(int specialIndex)
        {
            phaseBannerText.color = EnragedNoteColor;
            phaseBannerText.text = "루나벨이 하늘로 손을 뻗는다...";
            PlayMonsterOneShot(lunabelSpecialFrames, LunabelSpecialRaiseInterval, restart: true);
            yield return new WaitForSecondsRealtime(LunabelSpecialRaiseInterval * lunabelSpecialFrames.Length);
            if (BattleEnded()) yield break;

            phaseBannerText.text = "핏빛 구체가 부풀어 오른다! 노드를 맞혀 흩트려라!";
            monsterHoldFrames = lunabelSpecialChargeFrames;
            monsterHoldInterval = LunabelSpecialChargeInterval;
            monsterOneShot = null;
            monsterFrameIndex = 0;
            monsterFrameTimer = 0f;
            SetMonsterFrame(monsterHoldFrames[0]);
            yield return new WaitForSecondsRealtime(LunabelSpecialChargeLeadIn);
            if (BattleEnded()) yield break;

            int hits = 0;
            var notes = BuildChartNotes(LunabelSpecialChargeCharts[Mathf.Clamp(specialIndex, 0, LunabelSpecialChargeCharts.Length - 1)]);
            yield return RunChart(notes, SkillNoteColor, okWindow, (note, hit, diff) =>
            {
                if (hit)
                {
                    hits++;
                    TriggerPlayerAttackAnim();
                    ShowResult("기운을 흩트렸다!", GoodColor);
                }
                else
                {
                    ShowResult("놓쳤다...", MissColor);
                }
            });
            if (BattleEnded()) yield break;

            int misses = notes.Count - hits;
            if (misses <= 0)
            {
                ReturnMonsterToIdle();
                phaseBannerText.text = $"구체가 완전히 흩어졌다! ({hits}/{notes.Count})";
                ShowResult("완벽 방어!", GoodColor);
                PlayMonsterStagger();
                yield return new WaitForSecondsRealtime(BossSpecialStaggerDuration);
                phaseBannerText.text = "";
                yield break;
            }

            // 놓친 만큼 남은 구체를 던진다: 던지기 모션을 끝까지 재생하고, 마지막 자세(손을 내뻗은 모습)로 멈춘 채 던진다.
            float missRatio = (float)misses / notes.Count;
            phaseBannerText.text = $"{LunabelSpecialName}! 놓친 기운 {misses}/{notes.Count} — 위력 {missRatio * 100f:0}%";
            if (lunabelThrowFrames.Length > 0)
            {
                monsterHoldFrames = new[] { lunabelThrowFrames[lunabelThrowFrames.Length - 1] };
                monsterHoldInterval = 1f;
                PlayMonsterOneShot(lunabelThrowFrames, LunabelThrowFrameInterval, restart: true);
                yield return new WaitForSecondsRealtime(LunabelThrowFrameInterval * lunabelThrowFrames.Length);
                if (BattleEnded()) yield break;
            }

            LaunchBloodOrb();
            yield return new WaitForSecondsRealtime(BloodOrbTravelTime + BloodOrbSinkTime);
            ReturnMonsterToIdle();
            if (BattleEnded()) yield break;

            yield return BloodPillar(LunabelSpecialPillarHits, LunabelSpecialPillarDamageMultiplier * missRatio, LunabelSpecialName,
                bloodPillarRiseFrames, bloodPillarTornadoFrames, BloodPillarTornadoInterval);
            if (BattleEnded()) yield break;
            yield return new WaitForSecondsRealtime(LunabelSpecialAfterPause);
            phaseBannerText.text = "";
        }

        private void ReturnMonsterToIdle()
        {
            monsterHoldFrames = null;
            monsterOneShot = null;
            monsterFrameIndex = 0;
            monsterFrameTimer = 0f;
            SetMonsterFrame(monsterIdleFrames[0]);
        }

        // 피의 기둥이 솟구친 뒤 회오리치는 동안, 일정한 간격으로 나눠 피해를 준다. 끝나면 서서히 사라진다.
        // 피해 = 노드 한 타(몬스터 공격력 기준) x totalMultiplier를 hits번에 나눈 값. 필살기(피의 기둥: rise 한 번 → loop 반복)와
        // 1페이즈 피의 회오리(rise 없이 회오리 loop만, 처음에 살짝 페이드 인)가 같이 쓴다.
        private IEnumerator BloodPillar(int hits, float totalMultiplier, string attackName,
            Sprite[] riseFrames, Sprite[] loopFrames, float loopInterval)
        {
            float hitDamage = CombatMath.PhysicalDamage(monster.Attack, player.Stats.defense) * totalMultiplier / hits;
            if (riseFrames.Length == 0 && loopFrames.Length == 0)
            {
                // 이펙트가 없으면 피해만 한 번에 준다.
                DealBloodPillarHit(hitDamage * hits, attackName);
                yield break;
            }

            bloodPillarImage.color = riseFrames.Length == 0 ? new Color(1f, 1f, 1f, 0f) : Color.white;
            bloodPillarImage.gameObject.SetActive(true);
            foreach (var riseFrame in riseFrames)
            {
                SetBloodPillarFrame(riseFrame);
                yield return new WaitForSecondsRealtime(BloodPillarRiseInterval);
                if (BattleEnded()) yield break;
            }

            var loop = loopFrames.Length > 0 ? loopFrames : new[] { riseFrames[riseFrames.Length - 1] };
            float hitInterval = BloodPillarTornadoDuration / hits;
            int dealt = 0;
            int frame = 0;
            float frameTimer = 0f;
            SetBloodPillarFrame(loop[0]);
            for (float t = 0f; t < BloodPillarTornadoDuration || dealt < hits; t += Time.unscaledDeltaTime)
            {
                if (riseFrames.Length == 0) bloodPillarImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / BloodVortexFadeInTime));
                if (dealt < hits && t >= dealt * hitInterval)
                {
                    dealt++;
                    DealBloodPillarHit(hitDamage, attackName);
                    if (BattleEnded()) yield break;
                }
                frameTimer += Time.unscaledDeltaTime;
                if (frameTimer >= loopInterval)
                {
                    frameTimer -= loopInterval;
                    frame = (frame + 1) % loop.Length;
                    SetBloodPillarFrame(loop[frame]);
                }
                yield return null;
            }

            for (float f = 0f; f < BloodPillarFadeTime; f += Time.unscaledDeltaTime)
            {
                bloodPillarImage.color = new Color(1f, 1f, 1f, 1f - f / BloodPillarFadeTime);
                yield return null;
            }
            bloodPillarImage.gameObject.SetActive(false);
        }

        private void DealBloodPillarHit(float damage, string attackName)
        {
            HurtPlayer(damage);
            ShowResult($"{attackName}! -{damage:0}", MissColor);
            RefreshHpBars();
        }

        // 몬스터가 플레이어에게 피해를 준다(막을 수 없는 연타라 피격 무적 없이). 루나벨전이면 플레이어 머리 위에
        // 루나벨 전용 핏빛 숫자(UI/DamageDigits/lunabel_N)를 띄운다.
        private void HurtPlayer(float damage)
        {
            player.TakeDamage(damage, monster.transform.position, hitReaction: false);
            TriggerPlayerHitAnim();
            if (isLunabelBattle && lunabelDigits[0] != null)
                ShowDamageNumber(playerDamageNumberLayer, lunabelDigits, LunabelDigitHeight, damage, false);
        }

        // ---- 루나벨 1페이즈: 혈류 흡수 → 피의 회오리 ----
        // 1페이즈 적 턴을 세어 LunabelAbsorbEveryNthTurn번째 턴마다(3, 6, 9...) 피를 빨아들이는 모션(Absorb 8프레임)과 함께
        // 최대 체력의 LunabelAbsorbHealRatio만큼 회복하고 피가 들끓는 상태(BloodCharged)가 된다(그 턴은 노드 패턴 없음).
        // 그 뒤 루나벨의 노드를 LunabelBloodTornadoMissThreshold개 놓치면(여러 턴에 걸쳐 쌓여도 된다) 그 적 턴 끝에
        // 플레이어 발밑에서 피의 회오리(Lubel_Skill1)가 휘몰아치며 여러 번 나눠 피해를 준다(막을 수 없음).
        // 아직 들끓는 중에 다시 흡수하면 회복만 하고, 그때까지 쌓인 놓친 노드 수는 그대로 이어진다.
        private const string LunabelBloodTornadoName = "피의 회오리";
        private const int LunabelBloodTornadoMissThreshold = 5;
        private const float BloodVortexFrameInterval = 0.08f;
        private const float BloodVortexFadeInTime = 0.2f;
        private Sprite[] bloodVortexFrames = System.Array.Empty<Sprite>();
        private const int LunabelAbsorbEveryNthTurn = 3;
        private int lunabelPhaseOneTurns; // 이번 전투에서 루나벨이 1페이즈로 친 적 턴 수(흡수 턴 포함)
        private const float LunabelAbsorbHealRatio = 0.12f;
        private const float LunabelAbsorbFrameInterval = 0.15f;
        private const int LunabelAbsorbHealFrame = 5; // 핏줄기가 가장 크게 휘감기는 6번째 프레임에서 회복한다
        private const float LunabelAbsorbAfterPause = 0.6f;
        private const float LunabelBloodTornadoWarnPause = 1.0f;
        private const int LunabelBloodTornadoHits = 6;
        private const float LunabelBloodTornadoDamageMultiplier = 3f;
        private Sprite[] lunabelAbsorbFrames = System.Array.Empty<Sprite>();

        // 1페이즈 적 턴마다 한 번 부른다(턴 수를 센다). 3번째 턴마다 true.
        private bool ShouldLunabelAbsorb()
        {
            lunabelPhaseOneTurns++;
            return lunabelAbsorbFrames.Length > 0 && lunabelPhaseOneTurns % LunabelAbsorbEveryNthTurn == 0;
        }

        private IEnumerator LunabelBloodAbsorb(FinalBossAI finalBoss)
        {
            phaseBannerText.color = EnemyNoteColor;
            phaseBannerText.text = "루나벨이 주위의 피를 빨아들인다...";
            PlayMonsterOneShot(lunabelAbsorbFrames, LunabelAbsorbFrameInterval, restart: true);
            int healFrame = Mathf.Min(LunabelAbsorbHealFrame, lunabelAbsorbFrames.Length - 1);
            yield return new WaitForSecondsRealtime(LunabelAbsorbFrameInterval * healFrame);
            if (BattleEnded()) yield break;

            float before = monster.CurrentHp;
            monster.Heal(monster.MaxHp * LunabelAbsorbHealRatio);
            RefreshHpBars();
            ShowResult($"혈류 흡수! 체력 +{monster.CurrentHp - before:0}", EnemyNoteColor);
            // 이미 들끓는 중이면 쌓인 놓친 노드 수는 그대로 이어 간다.
            if (!finalBoss.BloodCharged) finalBoss.BloodMisses = 0;
            finalBoss.BloodCharged = true;
            yield return new WaitForSecondsRealtime(LunabelAbsorbFrameInterval * (lunabelAbsorbFrames.Length - healFrame));
            if (BattleEnded()) yield break;

            phaseBannerText.text = $"루나벨의 몸에 피가 들끓는다! 노드를 {LunabelBloodTornadoMissThreshold}개 놓치면 피의 회오리가 터진다!";
            yield return new WaitForSecondsRealtime(LunabelAbsorbAfterPause);
            phaseBannerText.text = "";
        }

        private IEnumerator LunabelBloodTornado()
        {
            phaseBannerText.color = EnemyNoteColor;
            phaseBannerText.text = "흘린 피가 공명한다... 빨아들인 피가 발밑에서 끓어오른다...!";
            PlayMonsterOneShot(monsterAttackFrames, MonsterAttackFrameInterval, restart: true);
            yield return new WaitForSecondsRealtime(LunabelBloodTornadoWarnPause);
            if (BattleEnded()) yield break;

            phaseBannerText.text = $"{LunabelBloodTornadoName}!";
            yield return BloodPillar(LunabelBloodTornadoHits, LunabelBloodTornadoDamageMultiplier, LunabelBloodTornadoName,
                System.Array.Empty<Sprite>(), bloodVortexFrames.Length > 0 ? bloodVortexFrames : bloodPillarTornadoFrames,
                bloodVortexFrames.Length > 0 ? BloodVortexFrameInterval : BloodPillarTornadoInterval);
            phaseBannerText.text = "";
        }

        private void SetBloodPillarFrame(Sprite sprite)
        {
            bloodPillarImage.sprite = sprite;
            bloodPillarImage.rectTransform.sizeDelta = sprite.rect.size * BloodPillarScale * PxToCanvas;
        }

        private void LaunchBloodOrb()
        {
            if (bloodOrbSprite == null) return;
            StopBloodOrb();
            bloodOrbRoutine = StartCoroutine(BloodOrbRoutine());
        }

        // 새 전투가 시작될 때 지난 전투의 구체/피의 기둥이 남아 있지 않게 치운다.
        private void StopBloodOrb()
        {
            if (bloodOrbRoutine != null)
            {
                StopCoroutine(bloodOrbRoutine);
                bloodOrbRoutine = null;
            }
            if (bloodOrbImage != null) bloodOrbImage.gameObject.SetActive(false);
            if (bloodPillarImage != null) bloodPillarImage.gameObject.SetActive(false);
        }

        private IEnumerator BloodOrbRoutine()
        {
            var rect = bloodOrbImage.rectTransform;
            bloodOrbImage.sprite = bloodOrbSprite;
            bloodOrbImage.color = Color.white;
            bloodOrbImage.gameObject.SetActive(true);

            for (float t = 0f; t < BloodOrbTravelTime; t += Time.unscaledDeltaTime)
            {
                float k = t / BloodOrbTravelTime;
                float arc = 4f * k * (1f - k) * BloodOrbArcHeight;
                rect.anchoredPosition = ImageToCanvasPos(Mathf.Lerp(BloodOrbStartX, BloodOrbEndX, k),
                    Mathf.Lerp(BloodOrbStartY, BloodOrbEndY, k) - arc);
                float size = Mathf.Lerp(BloodOrbStartSize, BloodOrbEndSize, k) * PxToCanvas;
                rect.sizeDelta = new Vector2(size, size);
                rect.localRotation = Quaternion.Euler(0f, 0f, t * BloodOrbSpinSpeed);
                yield return null;
            }

            // 발밑에 닿으면 납작하게 눌리며 땅으로 스며든다.
            rect.anchoredPosition = ImageToCanvasPos(BloodOrbEndX, BloodOrbEndY);
            rect.localRotation = Quaternion.identity;
            float endSize = BloodOrbEndSize * PxToCanvas;
            for (float f = 0f; f < BloodOrbSinkTime; f += Time.unscaledDeltaTime)
            {
                float k = f / BloodOrbSinkTime;
                rect.sizeDelta = new Vector2(endSize * (1f + 0.3f * k), endSize * (1f - 0.8f * k));
                bloodOrbImage.color = new Color(1f, 1f, 1f, 1f - k);
                yield return null;
            }
            bloodOrbImage.gameObject.SetActive(false);
            bloodOrbRoutine = null;
        }

        // ---- 보스전 도주 차단 ----
        // 보스 기사전에서 도주를 고르면 도망치지 못하고, 보스가 "도망칠 수 없다"며 검기를 날려 바로 피해를 준다.
        // 그 턴은 그대로 끝나고 적 턴으로 넘어간다.
        private const float FleeBlockedLinePause = 0.9f;
        private const float FleeBlockedWaveDamageMultiplier = 2f;

        private bool BossBlocksFlee => isBossBattle && monster.GetComponent<BossAI>() != null && swordWaveFrames.Length > 0;

        private IEnumerator BossBlockFlee()
        {
            phaseBannerText.color = EnemyNoteColor;
            phaseBannerText.text = "보스 기사: \"어딜 도망치려 하느냐! 도망칠 수 없다!\"";
            ShowResult("도망칠 수 없다!", MissColor);
            yield return new WaitForSecondsRealtime(FleeBlockedLinePause);
            if (BattleEnded()) yield break;

            yield return FireSwordWaveAtPlayer(FleeBlockedWaveDamageMultiplier, "검기에 베였다!");
        }

        // 필살기를 캔슬당한 보스가 휘청인다: 좌우로 흔들리며 몇 번 번쩍인다.
        private void PlayMonsterStagger()
        {
            if (!monsterPortrait.enabled) return;
            if (monsterStaggerRoutine != null) StopCoroutine(monsterStaggerRoutine);
            monsterStaggerRoutine = StartCoroutine(MonsterStaggerRoutine());
        }

        private IEnumerator MonsterStaggerRoutine()
        {
            var rect = monsterPortrait.rectTransform;
            const int shakes = 8;
            float step = BossSpecialStaggerDuration * 0.6f / shakes;
            for (int i = 0; i < shakes; i++)
            {
                float falloff = 1f - (float)i / shakes;
                rect.anchoredPosition = new Vector2((i % 2 == 0 ? 1f : -1f) * BossSpecialStaggerShake * falloff, 0f);
                monsterPortrait.color = i % 2 == 0 ? MonsterHitFlashColor : Color.white;
                yield return new WaitForSecondsRealtime(step);
            }
            rect.anchoredPosition = Vector2.zero;
            monsterPortrait.color = Color.white;
            monsterStaggerRoutine = null;
        }

        private void LaunchSwordWave(float travelTime)
        {
            if (swordWaveFrames.Length == 0) return;
            StopSwordWave();
            swordWaveRoutine = StartCoroutine(SwordWaveRoutine(travelTime));
        }

        private void StopSwordWave()
        {
            if (swordWaveRoutine != null)
            {
                StopCoroutine(swordWaveRoutine);
                swordWaveRoutine = null;
            }
            if (swordWaveImage != null) swordWaveImage.gameObject.SetActive(false);
        }

        private IEnumerator SwordWaveRoutine(float travelTime)
        {
            var rect = swordWaveImage.rectTransform;
            float centerY = SwordWaveBottomY - SwordWaveHeight * 0.5f;
            swordWaveImage.color = Color.white;
            swordWaveImage.sprite = swordWaveFrames[0];
            swordWaveImage.gameObject.SetActive(true);

            float t = 0f;
            float frameTimer = 0f;
            int frame = 0;
            while (t < travelTime)
            {
                rect.anchoredPosition = ImageToCanvasPos(Mathf.Lerp(SwordWaveStartX, SwordWaveEndX, t / travelTime), centerY);

                // 첫 프레임(솟구침)을 잠깐 보여 준 뒤, 나머지 프레임(굴러가는 검기)을 반복한다.
                frameTimer += Time.unscaledDeltaTime;
                float interval = frame == 0 ? SwordWaveBurstInterval : SwordWaveFrameInterval;
                if (frameTimer >= interval)
                {
                    frameTimer -= interval;
                    frame = swordWaveFrames.Length > 1 ? 1 + (frame % (swordWaveFrames.Length - 1)) : 0;
                    swordWaveImage.sprite = swordWaveFrames[frame];
                }

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // 플레이어에게 닿으면 짧게 사라진다.
            const float fade = 0.15f;
            for (float f = 0f; f < fade; f += Time.unscaledDeltaTime)
            {
                swordWaveImage.color = new Color(1f, 1f, 1f, 1f - f / fade);
                yield return null;
            }
            swordWaveImage.gameObject.SetActive(false);
            swordWaveRoutine = null;
        }

        private static float AverageNoteCount(string[] charts)
        {
            int total = 0;
            foreach (var chart in charts)
            {
                foreach (char c in chart)
                {
                    if (c == 'U' || c == 'D' || c == 'u' || c == 'd' || c == 'A' || c == 'B') total++;
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
                char c = chart[i];
                bool feint = c == 'A' || c == 'B';
                if (c == 'U' || c == 'u' || c == 'B') lane = LaneUp;
                else if (c == 'D' || c == 'd' || c == 'A') lane = LaneDown;
                else continue;
                if (mirror) lane = 1 - lane;
                float hitTime = telegraphDuration + i * stepDuration;
                if (feint)
                {
                    notes.Add(new ChartNote
                    {
                        Lane = lane, ShownLane = 1 - lane, HitTime = hitTime, EndTime = hitTime,
                        SwitchTime = hitTime - telegraphDuration * FeintSwitchLeadFraction,
                    });
                    continue;
                }
                int holdSteps = 0;
                if (c == 'u' || c == 'd')
                {
                    while (i + holdSteps + 1 < chart.Length && chart[i + holdSteps + 1] == '=') holdSteps++;
                }
                notes.Add(new ChartNote { Lane = lane, ShownLane = lane, HitTime = hitTime, EndTime = hitTime + holdSteps * stepDuration });
            }
            return notes;
        }

        private IEnumerator PlayerTurn()
        {
            yield return ShowTurnBanner(myTurnBannerSprite);

            if (BattleEnded()) yield break;
            if (isPractice)
            {
                skillGauge = MaxSkillGauge;
                RefreshHpBars();
            }
            // 튜토리얼 몬스터: 체력이 절반 이하면 필살 게이지를 채워 주고 스킬로 마무리하라고 알려 준다.
            bool tutorialFinish = monster.GetComponent<TutorialSkillFinish>() != null
                && monster.CurrentHp <= monster.MaxHp * TutorialSkillFinish.HintHealthFraction;
            if (tutorialFinish)
            {
                skillGauge = MaxSkillGauge;
                RefreshHpBars();
                phaseBannerText.color = SkillNoteColor;
                phaseBannerText.text = "필살 게이지가 가득 찼다! [스킬]을 골라 마무리하자!";
            }
            yield return ShowActionSelect();
            if (tutorialFinish) phaseBannerText.text = "";
            if (BattleEnded()) yield break;

            if (actionSelectFlee)
            {
                if (BossBlocksFlee)
                {
                    yield return BossBlockFlee();
                    if (BattleEnded()) yield break;
                    yield return new WaitForSecondsRealtime(AfterChartPause);
                    if (BattleEnded()) yield break;
                    turnRoutine = StartCoroutine(EnemyTurn());
                    yield break;
                }
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

            var pattern = PickPlayerPattern();
            phaseBannerText.color = PlayerNoteColor;
            phaseBannerText.text = $"{pattern.Name} - {pattern.Hint}";
            yield return new WaitForSecondsRealtime(PlayerPatternAnnounceDuration);
            if (BattleEnded()) yield break;

            var baseCharts = isBossBattle ? BossCharts : NormalCharts;
            var patternCharts = pattern.Charts ?? baseCharts;
            float perNote = pattern.Power * AverageNoteCount(baseCharts) / AverageNoteCount(patternCharts);
            telegraphDuration = baseTelegraphDuration * pattern.ApproachScale;
            stepDuration = baseStepDuration * pattern.StepScale;
            var notes = BuildChartNotes(patternCharts[Random.Range(0, patternCharts.Length)]);
            int combo = 0;
            bool fullCombo = true;

            yield return RunChart(notes, PlayerNoteColor, okWindow, (note, hit, diff) =>
            {
                if (hit)
                {
                    combo++;
                    float comboScale = 1f + Mathf.Min(MaxComboBonus, (combo - 1) * ComboBonusPerHit);
                    phaseBannerText.text = combo >= 2 ? $"{pattern.Name}  {combo} COMBO" : pattern.Name;
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
                    StartCoroutine(DealDamageAfterImpact(rhythm * perNote * comboScale, false, battleSerial));
                    skillGauge = Mathf.Min(MaxSkillGauge, skillGauge + gauge);
                    RefreshHpBars();
                }
                else
                {
                    // 놓쳐도 MISS 배수(0.3)만큼은 들어간다. 모션 없이 바로 넣는다. 콤보는 끊긴다.
                    combo = 0;
                    fullCombo = false;
                    phaseBannerText.text = pattern.Name;
                    ShowJudgment(judgmentMiss);
                    nextHitIsMiss = true;
                    player.DealDamage(monster, MissMultiplier * perNote, false);
                    nextHitIsMiss = false;
                    RefreshHpBars();
                }
            });

            telegraphDuration = baseTelegraphDuration;
            stepDuration = baseStepDuration;
            if (BattleEnded()) yield break;
            // 마지막 노드의 지연 데미지까지 들어간 뒤에 넘어간다.
            yield return new WaitForSecondsRealtime(Mathf.Max(AfterChartPause, AttackImpactDelay));
            if (BattleEnded()) yield break;

            if (fullCombo && notes.Count > 0)
            {
                phaseBannerText.text = $"풀 콤보! 마무리 일격 ({notes.Count} COMBO)";
                TriggerPlayerAttackAnim();
                yield return DealDamageAfterImpact(PerfectMultiplier * perNote * FullComboFinisherHits * (1f + MaxComboBonus), false, battleSerial);
                if (BattleEnded()) yield break;
                yield return new WaitForSecondsRealtime(AfterChartPause);
                if (BattleEnded()) yield break;
            }
            phaseBannerText.text = "";
            turnRoutine = StartCoroutine(EnemyTurn());
        }

        private PlayerPattern PickPlayerPattern()
        {
            var candidates = new List<PlayerPattern>();
            foreach (var pattern in PlayerPatterns)
            {
                if (pattern != lastPlayerPattern) candidates.Add(pattern);
            }
            var picked = candidates[Random.Range(0, candidates.Count)];
            lastPlayerPattern = picked;
            return picked;
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

            // 튜토리얼 몬스터는 스킬을 쓰면 반드시 맞고 쓰러진다: 노드를 다 놓쳐도 실패 없이 한 타는 들어간다.
            bool tutorialFinish = monster.GetComponent<TutorialSkillFinish>() != null;
            if (tutorialFinish && hitMultipliers.Count == 0) hitMultipliers.Add(OkMultiplier * perNodeMultiplier);

            if (hitMultipliers.Count == 0)
            {
                phaseBannerText.text = "";
                ShowResult(skill.Name + " 실패...", MissColor);
                yield return new WaitForSecondsRealtime(SkillFailPause);
                yield break;
            }

            // 궁극기는 채보를 다 친 뒤, 시전 모션이 나오기 전에 화면이 암전됐다가 눈을 번쩍 뜨며
            // 깜빡이는 연출을 한 번 먼저 보여준다.
            if (skill.IsUltimate) yield return PlayUltimateAwakening();
            if (BattleEnded()) yield break;

            // 마무리 연출
            phaseBannerText.text = $"{skill.Name}! ({hitMultipliers.Count}/{notes.Count})";
            PlayPlayerOneShot(skillCastFrames[skillIndex], skill.FinisherDuration, Color.white, skill.CastCharacterHeightPx);
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

            // 튜토리얼 몬스터: 연타를 다 맞는 동안은 체력 1로 버티게 두었다가(SurviveLethalHit), 마지막 타가 끝나면
            // 남은 체력을 한 번에 깎아 확정으로 쓰러뜨린다 — 피해량이 모자라도, 첫 타에 연출이 끊기지도 않게.
            if (tutorialFinish && !monster.IsDead)
            {
                monster.SurviveLethalHit = false;
                monster.TakeDamage(monster.CurrentHp, true);
                RefreshHpBars();
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
            myTurnTitleText.text = "";
            myTurnFrame.SetActive(true);
            actionSelectPanel.SetActive(true);
            ShowFleeLock();

            bool confirmed = false;
            while (!confirmed)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.leftArrowKey.wasPressedThisFrame)
                    {
                        actionSelectIndex = (actionSelectIndex + 3) % 4;
                        actionMessageText.text = ActionHint();
                        RefreshActionCursor();
                    }
                    else if (keyboard.rightArrowKey.wasPressedThisFrame)
                    {
                        actionSelectIndex = (actionSelectIndex + 1) % 4;
                        actionMessageText.text = ActionHint();
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
                        else if (actionSelectIndex == ActionFlee && fleeLocked)
                        {
                            actionMessageText.text = "루나벨의 핏빛 사슬이 도주를 막고 있다!";
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
                                myTurnTitleText.text = "";
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

        // 커서가 옮겨 간 칸의 안내 문구(아이템 = 포션 개수, 잠긴 도주 = 사용 불가).
        private string ActionHint()
        {
            if (actionSelectIndex == ActionItem) return PotionHint();
            if (actionSelectIndex == ActionFlee && fleeLocked) return "이 싸움에서는 도망칠 수 없다.";
            return "";
        }

        // 도주가 잠긴 전투면 도주 칸 위에 잠금 그림을 띄운다. 그 전투의 첫 행동 선택에서만 사슬이 감기는 애니메이션을 튼다.
        private void ShowFleeLock()
        {
            bool show = fleeLocked && fleeLockFrames[FleeLockFrameCount - 1] != null;
            fleeLockImage.gameObject.SetActive(show);
            if (!show) return;
            if (fleeLockAnimated)
            {
                fleeLockImage.texture = fleeLockFrames[FleeLockFrameCount - 1];
                return;
            }
            fleeLockAnimated = true;
            StartCoroutine(FleeLockRoutine());
        }

        // ---- 루나벨 이름 카드 ----
        // 루나벨전이 열리면 암전 직후, BATTLE 연출 전에 나온다: 화면 가득 루나벨 일러스트(UI/LunabelIntro, 16:9)가 서서히
        // 떠오르고, 아래쪽 어두운 띠 위 가운데에 핏빛 풀네임 "Lunabel Cruora"(Sprite/UI/Lunabel Curora를 글자별로 자른 UI/LunabelName/letter_01~13)가
        // 한 글자씩 튀어나오듯 찍힌다. 찍힌 글자는 저마다 따로 떨리듯 진동한다. 다 찍히면 잠깐 머문 뒤 사라진다. Z/Enter로 건너뛴다.
        // 글자 자리는 원래 이름 그림(2172x724) 속 픽셀 사각형(x0, y0, x1, y1) 그대로 놓는다.
        private const string LunabelIllustrationPath = "UI/LunabelIntro";
        private const string LunabelNameLetterPath = "UI/LunabelName/letter_";
        private const float NameImageWidth = 2172f, NameImageHeight = 724f;
        private static readonly Vector4[] LunabelNameLetterRects =
        {
            // Lunabel
            new Vector4(28f, 0f, 409f, 378f),
            new Vector4(391f, 73f, 693f, 385f),
            new Vector4(703f, 77f, 983f, 385f),
            new Vector4(1006f, 73f, 1277f, 378f),
            new Vector4(1299f, 0f, 1581f, 392f),
            new Vector4(1606f, 76f, 1870f, 387f),
            new Vector4(1932f, 0f, 2142f, 403f),
            // Cruora
            new Vector4(97f, 378f, 426f, 724f),
            new Vector4(467f, 421f, 727f, 724f),
            new Vector4(754f, 414f, 1068f, 724f),
            new Vector4(1073f, 409f, 1358f, 724f),
            new Vector4(1423f, 426f, 1682f, 724f),
            new Vector4(1733f, 412f, 2017f, 724f),
        };
        private const float NameCardFadeIn = 0.5f;
        private const float NameLetterInterval = 0.13f;
        private const float NameLetterPopTime = 0.12f;
        private const float NameLetterPopScale = 1.6f;
        private const float NameCardHold = 1.3f;
        private const float NameCardFadeOut = 0.4f;
        private const float NameShakeAmount = 4f;   // 글자 떨림 폭(캔버스 px)
        private const float NameShakeAngle = 2.5f;  // 글자 떨림 기울기(도)
        private const float NameShakeInterval = 0.03f;
        private CanvasGroup nameCardGroup;
        private RawImage nameCardIllustration;
        private AspectRatioFitter nameCardIllustrationFitter;
        private Image nameCardBand;
        private const float NameBandAlpha = 0.6f;
        private readonly List<RawImage> nameLetters = new List<RawImage>();

        private void BuildLunabelNameCard(RectTransform parent)
        {
            var cardGO = new GameObject("LunabelNameCard", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            var cardRect = (RectTransform)cardGO.transform;
            cardRect.SetParent(parent, false);
            cardRect.anchorMin = Vector2.zero;
            cardRect.anchorMax = Vector2.one;
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;
            var backdrop = cardGO.GetComponent<Image>();
            backdrop.color = new Color(0.08f, 0f, 0.02f, 1f);
            backdrop.raycastTarget = false;
            nameCardGroup = cardGO.GetComponent<CanvasGroup>();

            // 화면 가득: 16:9 일러스트(루나벨이 가운데 서 있다). 비율이 조금 달라도 빈틈 없이 덮게 EnvelopeParent.
            var illustGO = new GameObject("Illustration", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            var illustRect = (RectTransform)illustGO.transform;
            illustRect.SetParent(cardRect, false);
            nameCardIllustration = illustGO.GetComponent<RawImage>();
            nameCardIllustration.raycastTarget = false;
            nameCardIllustration.texture = Resources.Load<Texture2D>(LunabelIllustrationPath);
            nameCardIllustrationFitter = illustGO.GetComponent<AspectRatioFitter>();
            nameCardIllustrationFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            var tex = nameCardIllustration.texture;
            if (tex != null) nameCardIllustrationFitter.aspectRatio = (float)tex.width / tex.height;

            // 배경도 붉어서 핏빛 글자가 묻히지 않게, 이름 뒤 화면 아래쪽에 어두운 띠를 깐다(일러스트와 같이 떠오른다).
            var bandGO = new GameObject("NameBand", typeof(RectTransform), typeof(Image));
            var bandRect = (RectTransform)bandGO.transform;
            bandRect.SetParent(cardRect, false);
            bandRect.anchorMin = new Vector2(0f, 0f);
            bandRect.anchorMax = new Vector2(1f, 0.42f);
            bandRect.offsetMin = Vector2.zero;
            bandRect.offsetMax = Vector2.zero;
            nameCardBand = bandGO.GetComponent<Image>();
            nameCardBand.raycastTarget = false;

            // 아래쪽 가운데: 이름 글자를 놓을 3:1 칸(FitInParent는 부모 전체에 맞추므로, 자리 잡을 칸을 하나 두고 그 안에 맞춘다).
            var nameAreaGO = new GameObject("NameArea", typeof(RectTransform));
            var nameAreaRect = (RectTransform)nameAreaGO.transform;
            nameAreaRect.SetParent(cardRect, false);
            nameAreaRect.anchorMin = new Vector2(0.15f, 0.04f);
            nameAreaRect.anchorMax = new Vector2(0.85f, 0.4f);
            nameAreaRect.offsetMin = Vector2.zero;
            nameAreaRect.offsetMax = Vector2.zero;
            var nameGO = new GameObject("Name", typeof(RectTransform), typeof(AspectRatioFitter));
            var nameRect = (RectTransform)nameGO.transform;
            nameRect.SetParent(nameAreaRect, false);
            var nameFitter = nameGO.GetComponent<AspectRatioFitter>();
            nameFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            nameFitter.aspectRatio = NameImageWidth / NameImageHeight;

            for (int i = 0; i < LunabelNameLetterRects.Length; i++)
            {
                var texture = Resources.Load<Texture2D>(LunabelNameLetterPath + (i + 1).ToString("00"));
                if (texture == null) continue;
                var r = LunabelNameLetterRects[i];
                var letterGO = new GameObject("Letter" + (i + 1), typeof(RectTransform), typeof(RawImage));
                var letterRect = (RectTransform)letterGO.transform;
                letterRect.SetParent(nameRect, false);
                letterRect.anchorMin = new Vector2(r.x / NameImageWidth, 1f - r.w / NameImageHeight);
                letterRect.anchorMax = new Vector2(r.z / NameImageWidth, 1f - r.y / NameImageHeight);
                letterRect.offsetMin = Vector2.zero;
                letterRect.offsetMax = Vector2.zero;
                var letter = letterGO.GetComponent<RawImage>();
                letter.texture = texture;
                letter.raycastTarget = false;
                nameLetters.Add(letter);
            }

            cardGO.SetActive(false);
        }

        private IEnumerator LunabelNameCard()
        {
            if (nameCardGroup == null || nameLetters.Count == 0) yield break;

            var card = nameCardGroup.gameObject;
            card.SetActive(true);
            nameCardGroup.alpha = 1f;
            nameCardIllustration.color = new Color(1f, 1f, 1f, 0f);
            foreach (var letter in nameLetters)
            {
                letter.gameObject.SetActive(false);
                letter.rectTransform.localScale = Vector3.one;
                letter.rectTransform.anchoredPosition = Vector2.zero;
                letter.rectTransform.localRotation = Quaternion.identity;
            }

            var shake = StartCoroutine(ShakeNameLetters());
            bool skipped = false;

            // 일러스트가 어둠 속에서 떠오른다.
            for (float t = 0f; t < NameCardFadeIn && !skipped; t += Time.unscaledDeltaTime)
            {
                float k = t / NameCardFadeIn;
                nameCardIllustration.color = new Color(1f, 1f, 1f, k);
                nameCardBand.color = new Color(0f, 0f, 0f, NameBandAlpha * k);
                skipped = NameCardSkipPressed();
                yield return null;
            }
            nameCardIllustration.color = Color.white;
            nameCardBand.color = new Color(0f, 0f, 0f, NameBandAlpha);

            // 글자를 하나씩 찍는다(크게 튀어나왔다가 제자리로 박힌다).
            for (int i = 0; i < nameLetters.Count && !skipped; i++)
            {
                var letter = nameLetters[i];
                letter.gameObject.SetActive(true);
                for (float t = 0f; t < NameLetterInterval && !skipped; t += Time.unscaledDeltaTime)
                {
                    float pop = Mathf.Clamp01(t / NameLetterPopTime);
                    letter.rectTransform.localScale = Vector3.one * Mathf.Lerp(NameLetterPopScale, 1f, pop);
                    letter.color = new Color(1f, 1f, 1f, pop);
                    skipped = NameCardSkipPressed();
                    yield return null;
                }
                letter.rectTransform.localScale = Vector3.one;
                letter.color = Color.white;
            }
            foreach (var letter in nameLetters)
            {
                letter.gameObject.SetActive(true);
                letter.rectTransform.localScale = Vector3.one;
                letter.color = Color.white;
            }

            for (float t = 0f; t < NameCardHold && !skipped; t += Time.unscaledDeltaTime)
            {
                skipped = NameCardSkipPressed();
                yield return null;
            }

            for (float t = 0f; t < NameCardFadeOut; t += Time.unscaledDeltaTime)
            {
                nameCardGroup.alpha = 1f - t / NameCardFadeOut;
                yield return null;
            }
            StopCoroutine(shake);
            card.SetActive(false);
        }

        // 찍힌 글자마다 따로 무작위로 흔들어, 피가 끓어오르듯 떨리는 느낌을 준다.
        private IEnumerator ShakeNameLetters()
        {
            while (nameCardGroup.gameObject.activeSelf)
            {
                foreach (var letter in nameLetters)
                {
                    if (!letter.gameObject.activeSelf) continue;
                    letter.rectTransform.anchoredPosition = Random.insideUnitCircle * NameShakeAmount;
                    letter.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-NameShakeAngle, NameShakeAngle));
                }
                yield return new WaitForSecondsRealtime(NameShakeInterval);
            }
        }

        private static bool NameCardSkipPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
        }

        // 루나벨전 첫 턴 전 연출: 노드가 날아오기 전에 루나벨이 한마디 하고 → 행동 선택 창이 떠서 도주 칸에 핏빛 사슬이
        // 감기고(잠금 애니메이션) → 네크로시아가 독백한 뒤에 첫 적 턴이 시작된다. 대사는 [Z]/Enter로 넘긴다.
        // 내 턴 창이 화면 높이를 다 덮어 대화창과 겹치므로, 잠금 장면 동안은 대화창을 잠깐 내린다.
        private const string LunabelOpeningLine = "어딜 보는 거야? 이 언덕에 발을 들인 순간부터, 넌 내 사냥감이야. 도망칠 길 따위 남겨 두지 않았어.";
        private const string NecrosiaOpeningLine = "...도망칠 생각 따윈 없다. 그날 무너진 리듬, 여기서 되찾는다.";
        private const string OpeningNextHint = "   <color=#E07A8A>[Z]</color>";
        private const string OpeningPlayerNextHint = "   <color=#B98CE8>[Z]</color>";
        private const float FleeLockHoldTime = 0.9f;

        private IEnumerator LunabelBattleOpening()
        {
            var dialogue = FindFirstObjectByType<NpcDialogueUI>();
            if (dialogue == null) yield break;
            dialogue.BringToFront();

            dialogue.ShowLunabel(LunabelOpeningLine + OpeningNextHint);
            yield return WaitForAdvance();
            dialogue.Hide();
            if (BattleEnded()) yield break;

            // 도주 칸 잠금 장면. 선택 빛줄기는 숨기고 잠금 애니메이션만 보여준다.
            myTurnTitleText.text = "";
            actionMessageText.text = "";
            actionCursorImage.enabled = false;
            myTurnFrame.SetActive(true);
            actionSelectPanel.SetActive(true);
            ShowFleeLock();
            yield return new WaitForSecondsRealtime(FleeLockFrameCount * FleeLockFrameInterval);
            actionMessageText.text = "루나벨의 핏빛 사슬이 도주를 봉인했다!";
            yield return new WaitForSecondsRealtime(FleeLockHoldTime);
            actionSelectPanel.SetActive(false);
            myTurnFrame.SetActive(false);
            actionCursorImage.enabled = true;
            actionMessageText.text = "";
            if (BattleEnded()) yield break;

            dialogue.ShowNecrosia(NecrosiaOpeningLine + OpeningPlayerNextHint);
            yield return WaitForAdvance();
            dialogue.Hide();
        }

        // 대사가 바뀐 그 프레임의 입력으로 바로 넘어가지 않게 한 프레임 쉰 뒤, [Z]/Enter를 기다린다.
        private IEnumerator WaitForAdvance()
        {
            yield return null;
            while (!BattleEnded())
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) yield break;
                yield return null;
            }
        }

        private IEnumerator FleeLockRoutine()
        {
            foreach (var frame in fleeLockFrames)
            {
                if (frame != null) fleeLockImage.texture = frame;
                yield return new WaitForSecondsRealtime(FleeLockFrameInterval);
                if (!fleeLockImage.gameObject.activeInHierarchy) break;
            }
            fleeLockImage.texture = fleeLockFrames[FleeLockFrameCount - 1];
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

        private void PlayPlayerOneShot(Sprite[] frames, float duration, Color tint, float characterHeightPx = 0f)
        {
            if (frames.Length == 0) return;
            if (attackAnimRoutine != null) StopCoroutine(attackAnimRoutine);
            RestorePlayerPortraitLayout();
            attackAnimRoutine = StartCoroutine(PlayPlayerOneShotRoutine(frames, duration, tint, characterHeightPx));
        }

        private IEnumerator PlayPlayerOneShotRoutine(Sprite[] frames, float duration, Color tint, float characterHeightPx)
        {
            isPlayingAttackAnim = true;
            playerPortrait.color = tint;
            float frameDuration = duration / frames.Length;
            foreach (var frame in frames)
            {
                playerPortrait.sprite = frame;
                if (characterHeightPx > 0f) ApplyLargeCastLayout(frame, characterHeightPx);
                yield return new WaitForSecondsRealtime(frameDuration);
            }
            RestorePlayerPortraitLayout();
            playerPortrait.color = Color.white;
            isPlayingAttackAnim = false;
            idleFrameTimer = 0f; // 애니메이션이 끝나면 바로 다음 프레임부터 대기 애니메이션이 매끈하게 이어진다.
            attackAnimRoutine = null;
        }

        // 큰 시전 모션(필살기)용 배치: 초상화 칸 아래쪽 가운데에 발을 붙이고, 캐릭터 키(characterHeightPx)가
        // 칸 높이(= 대기 모션 키)가 되는 배율로 캔버스 전체를 그린다. 낫/검기는 칸 밖으로 넘쳐 보인다.
        private bool playerPortraitLayoutSaved;
        private bool playerPortraitLayoutChanged;
        private Vector2 playerPortraitAnchorMin, playerPortraitAnchorMax, playerPortraitPivot, playerPortraitOffsetMin, playerPortraitOffsetMax;

        private void ApplyLargeCastLayout(Sprite frame, float characterHeightPx)
        {
            var rect = playerPortrait.rectTransform;
            if (!playerPortraitLayoutSaved)
            {
                playerPortraitAnchorMin = rect.anchorMin;
                playerPortraitAnchorMax = rect.anchorMax;
                playerPortraitPivot = rect.pivot;
                playerPortraitOffsetMin = rect.offsetMin;
                playerPortraitOffsetMax = rect.offsetMax;
                playerPortraitLayoutSaved = true;
            }

            float boxHeight = playerPortraitLayoutChanged ? playerPortraitBoxHeight : rect.rect.height;
            playerPortraitBoxHeight = boxHeight;
            playerPortraitLayoutChanged = true;

            float scale = boxHeight / characterHeightPx;
            var bottomCenter = new Vector2((playerPortraitAnchorMin.x + playerPortraitAnchorMax.x) * 0.5f, playerPortraitAnchorMin.y);
            playerPortrait.preserveAspect = false;
            rect.anchorMin = bottomCenter;
            rect.anchorMax = bottomCenter;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = frame.rect.size * scale;
        }

        private float playerPortraitBoxHeight;

        private void RestorePlayerPortraitLayout()
        {
            if (!playerPortraitLayoutChanged) return;
            var rect = playerPortrait.rectTransform;
            rect.anchorMin = playerPortraitAnchorMin;
            rect.anchorMax = playerPortraitAnchorMax;
            rect.pivot = playerPortraitPivot;
            rect.offsetMin = playerPortraitOffsetMin;
            rect.offsetMax = playerPortraitOffsetMax;
            playerPortrait.preserveAspect = true;
            playerPortraitLayoutChanged = false;
        }

        // 궁극기 각성: 화면이 검게 암전된 뒤, EyeFlash 프레임을 화면 가득 보여주며 몇 번 깜빡인다
        // (검은 화면 ↔ 눈 뜨는 그림이 번갈아 뜨고, 깜빡일 때마다 더 떠진 눈이 드러나게). 마지막엔 다 뜬 눈을
        // 화면 가득 잠깐 붙잡아 보여준 뒤, 암전을 걷으며 원래 전투 화면(작은 초상화)으로 돌아간다.
        private const float UltimateBlackoutDuration = 0.2f;
        private const int UltimateFlickerPulses = 4;
        private const float UltimateFlickerPulseDuration = 0.1f;
        private const float UltimateAwakeningHold = 0.4f;

        private IEnumerator PlayUltimateAwakening()
        {
            yield return FadeUltimateOverlay(0f, 1f, UltimateBlackoutDuration);

            ultimateEyeFullscreenImage.sprite = eyeFlashFrames[0];
            ultimateEyeFullscreenImage.gameObject.SetActive(true);

            for (int i = 1; i <= UltimateFlickerPulses; i++)
            {
                int frameIndex = Mathf.Min(eyeFlashFrames.Length - 1, i * eyeFlashFrames.Length / UltimateFlickerPulses);
                ultimateEyeFullscreenImage.sprite = eyeFlashFrames[frameIndex];
                ultimateEyeFullscreenImage.gameObject.SetActive(true);
                yield return new WaitForSecondsRealtime(UltimateFlickerPulseDuration * 0.5f);
                if (i < UltimateFlickerPulses)
                {
                    ultimateEyeFullscreenImage.gameObject.SetActive(false);
                    yield return new WaitForSecondsRealtime(UltimateFlickerPulseDuration * 0.5f);
                }
            }

            yield return new WaitForSecondsRealtime(UltimateAwakeningHold);

            // 작은 초상화도 눈을 다 뜬 프레임으로 맞춰 둬서, 화면 가득 찬 그림을 치우고 이어지는
            // 시전 모션으로 넘어갈 때 눈이 도로 감긴 것처럼 보이지 않게 한다.
            ultimateEyeFullscreenImage.gameObject.SetActive(false);
            playerPortrait.color = Color.white;
            playerPortrait.sprite = eyeFlashFrames[eyeFlashFrames.Length - 1];

            yield return FadeUltimateOverlay(1f, 0f, UltimateBlackoutDuration);
        }

        private IEnumerator FadeUltimateOverlay(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                ultimateFlashOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            ultimateFlashOverlay.color = new Color(0f, 0f, 0f, to);
        }

        // 채보 하나를 처음부터 끝까지 재생한다. 각 노드는 자기 HitTime보다 telegraphDuration 먼저
        // 오른쪽 끝에서 출발해 일정한 속도로 날아오므로, 화면 위 노드 사이 간격이 곧 리듬 간격이다.
        // 여러 노드가 동시에 떠 있을 수 있고, 키(위=Z, 아래=X)를 누르면 그 레인에서 아직 판정 안 된
        // 노드 중 타이밍이 가장 가까운 것을 판정한다: ±acceptWindow 안이면 맞힘, 그보다 일찍인데
        // EarlyMissRange 안이면 그 노드를 놓친 것으로 소모한다(마구 누르기 방지). 그보다 더 멀면 무시.
        // 판정점을 acceptWindow 넘게 지나친 노드도 놓친 것이다. 판정될 때마다 onJudged(노드, 맞힘?, 오차)를 부른다.
        // onIncoming: 노드가 판정점에 닿기 incomingLead초 전에 노드마다 한 번 불린다(루나벨이 그 노드에 맞춰 피의 손을 날린다).
        private IEnumerator RunChart(List<ChartNote> notes, Color noteColor, float acceptWindow, System.Action<ChartNote, bool, float> onJudged,
            System.Action<ChartNote> onIncoming = null, float incomingLead = 0f)
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
                        spawned.Image.rectTransform.localScale = Vector3.one;
                        SkinNote(spawned.Image, useLunabelNotes ? lunabelTapSprite : null, LunabelNoteSize);
                        spawned.Image.gameObject.SetActive(true);
                    }
                    if (spawned.IsHold)
                    {
                        spawned.BarImage = RentHoldBar();
                        if (spawned.BarImage != null)
                        {
                            spawned.BarImage.color = new Color(noteColor.r, noteColor.g, noteColor.b, HoldBarAlpha);
                            spawned.BarImage.sprite = useLunabelNotes ? lunabelBarSprite : null;
                            if (useLunabelNotes) spawned.BarImage.color = Color.white;
                            spawned.BarImage.gameObject.SetActive(true);
                        }
                        spawned.TailImage = RentNoteImage();
                        if (spawned.TailImage != null)
                        {
                            spawned.TailImage.color = noteColor;
                            spawned.TailImage.rectTransform.localScale = Vector3.one * HoldTailScale;
                            if (useLunabelNotes)
                            {
                                SkinNote(spawned.TailImage, lunabelTailSprite, LunabelTailSize);
                                spawned.TailImage.rectTransform.localScale = Vector3.one;
                            }
                            else SkinNote(spawned.TailImage, null, 0f);
                            spawned.TailImage.gameObject.SetActive(true);
                        }
                    }
                }

                if (onIncoming != null)
                {
                    for (int i = 0; i < nextSpawn; i++)
                    {
                        var note = notes[i];
                        if (note.Announced || note.Judged || t < note.HitTime - incomingLead) continue;
                        note.Announced = true;
                        onIncoming(note);
                    }
                }

                // 지나쳐 버린 노드를 먼저 놓침 처리하고 위치를 옮긴 뒤에 입력을 본다 — 그래야 이미 늦은
                // 노드가 이번 입력의 판정 대상으로 잡히지 않는다.
                for (int i = 0; i < nextSpawn; i++)
                {
                    var note = notes[i];
                    if (note.Judged) continue;

                    if (note.Holding)
                    {
                        // 누르고 있는 롱노트: 꼬리가 판정점에 닿으면 성공, 그 전에 떼면 실패.
                        bool stillHeld = LaneKeyHeld(note.Lane);
                        if (t >= note.EndTime || (!stillHeld && t >= note.EndTime - HoldReleaseGrace))
                        {
                            Judge(note, true, note.HeadDiff, onJudged);
                            judgedCount++;
                            if (BattleEnded()) yield break;
                            continue;
                        }
                        if (!stillHeld)
                        {
                            Judge(note, false, float.MaxValue, onJudged);
                            judgedCount++;
                            if (BattleEnded()) yield break;
                            continue;
                        }
                        if (t >= note.NextHoldSpark)
                        {
                            note.NextHoldSpark = t + HoldSparkInterval;
                            PlayNodeBurst(note.Lane);
                        }
                    }
                    else if (t > note.HitTime + acceptWindow)
                    {
                        Judge(note, false, float.MaxValue, onJudged);
                        judgedCount++;
                        if (BattleEnded()) yield break;
                        continue;
                    }

                    float laneY = NoteY(note, t);
                    float headX = note.Holding ? NodeCenter[note.Lane].x : NoteX(note.Lane, note.HitTime, t);
                    if (note.Image != null)
                    {
                        note.Image.rectTransform.anchoredPosition = ImageToCanvasPos(headX, laneY);
                        if (vanishNotes && !note.Holding)
                        {
                            float travel = 1f - (note.HitTime - t) / telegraphDuration;
                            Color c = note.Image.color;
                            c.a = noteColor.a * (1f - Mathf.InverseLerp(VanishFadeStart, VanishFadeEnd, travel));
                            note.Image.color = c;
                        }
                    }
                    if (note.IsHold)
                    {
                        float tailX = Mathf.Max(headX, NoteX(note.Lane, note.EndTime, t));
                        if (note.TailImage != null) note.TailImage.rectTransform.anchoredPosition = ImageToCanvasPos(tailX, laneY);
                        if (note.BarImage != null)
                        {
                            var bar = note.BarImage.rectTransform;
                            bar.anchoredPosition = ImageToCanvasPos(headX, laneY);
                            bar.sizeDelta = new Vector2((tailX - headX) * PxToCanvas, useLunabelNotes ? LunabelBarThickness : NoteSize * HoldBarThickness);
                        }
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
                    if (diff <= acceptWindow && target.IsHold)
                    {
                        // 롱노트 머리: 판정은 꼬리까지 버틴 뒤에 한 번 한다. 지금은 잡기만 한다.
                        target.Holding = true;
                        target.HeadDiff = diff;
                        target.NextHoldSpark = t + HoldSparkInterval;
                        PlayNodeBurst(lane);
                        continue;
                    }
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
                    exclaim[lane].gameObject.SetActive(!vanishNotes && LaneHasIncoming(notes, nextSpawn, lane, t));
                }

                yield return null;
            }

            for (int lane = 0; lane < 2; lane++) exclaim[lane].gameObject.SetActive(false);
        }

        private void Judge(ChartNote note, bool hit, float diff, System.Action<ChartNote, bool, float> onJudged)
        {
            note.Judged = true;
            note.Holding = false;
            if (note.Image != null)
            {
                note.Image.gameObject.SetActive(false);
                note.Image = null;
            }
            if (note.TailImage != null)
            {
                note.TailImage.rectTransform.localScale = Vector3.one;
                note.TailImage.gameObject.SetActive(false);
                note.TailImage = null;
            }
            if (note.BarImage != null)
            {
                note.BarImage.gameObject.SetActive(false);
                note.BarImage = null;
            }
            if (hit) PlayNodeBurst(note.Lane);
            onJudged(note, hit, diff);
        }

        // ---- 롱노트 ----
        private const int HoldBarPoolSize = 6;
        private const float HoldBarAlpha = 0.55f;
        private const float HoldBarThickness = 0.42f; // 노트 크기 대비 막대 굵기
        private const float HoldTailScale = 0.7f;
        // 꼬리 직전 이만큼(초) 안에서 떼는 건 끝까지 버틴 것으로 쳐 준다.
        private const float HoldReleaseGrace = 0.1f;
        private const float HoldSparkInterval = 0.12f;
        private readonly Image[] holdBarPool = new Image[HoldBarPoolSize];

        private float NoteX(int lane, float hitTime, float t)
        {
            float travelT = 1f - (hitTime - t) / telegraphDuration;
            return Mathf.LerpUnclamped(NoteSpawnX, NodeCenter[lane].x, travelT);
        }

        private static bool LaneKeyHeld(int lane)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return lane == LaneUp ? keyboard.zKey.isPressed : keyboard.xKey.isPressed;
        }

        private Image RentHoldBar()
        {
            foreach (var image in holdBarPool)
            {
                if (!image.gameObject.activeSelf) return image;
            }
            return null;
        }

        // 페이크 노드는 SwitchTime부터 FeintSwitchDuration 동안 보이던 레인에서 진짜 레인으로 미끄러진다.
        private float NoteY(ChartNote note, float t)
        {
            float realY = NodeCenter[note.Lane].y;
            if (note.ShownLane == note.Lane) return realY;
            float shownY = NodeCenter[note.ShownLane].y;
            return Mathf.Lerp(shownY, realY, Mathf.Clamp01((t - note.SwitchTime) / FeintSwitchDuration));
        }

        // "!" 표시는 지금 화면에 보이는 레인 기준으로 띄운다(페이크 노드의 진짜 레인을 미리 알려 주지 않게).
        private static bool LaneHasIncoming(List<ChartNote> notes, int spawnedCount, int lane, float t)
        {
            for (int i = 0; i < spawnedCount; i++)
            {
                var note = notes[i];
                if (note.Judged) continue;
                int shown = t < note.SwitchTime ? note.ShownLane : note.Lane;
                if (shown == lane) return true;
            }
            return false;
        }

        // 이미 출발한 노드 중, 이 레인에서 아직 판정 안 됐고 지금 시각 t와 타이밍이 가장 가까운 노드.
        private static ChartNote ClosestNote(List<ChartNote> notes, int spawnedCount, int lane, float t)
        {
            ChartNote best = null;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < spawnedCount; i++)
            {
                var note = notes[i];
                if (note.Judged || note.Holding || note.Lane != lane) continue;
                float diff = Mathf.Abs(t - note.HitTime);
                if (diff < bestDiff)
                {
                    best = note;
                    bestDiff = diff;
                }
            }
            return best;
        }

        // 풀에서 빌린 노드 그림을 기본 다이아몬드(customSprite == null)나 루나벨 노드로 맞춘다. 루나벨 노드는 원래 색을 살리도록
        // 흰색으로 칠하고, 그림 비율대로 size(세로 기준) 크기로 그린다.
        private void SkinNote(Image image, Sprite customSprite, float size)
        {
            var rect = image.rectTransform;
            if (customSprite == null)
            {
                image.sprite = defaultNoteSprite;
                rect.sizeDelta = new Vector2(NoteSize, NoteSize);
                return;
            }
            image.sprite = customSprite;
            image.color = new Color(1f, 1f, 1f, image.color.a);
            Vector2 px = customSprite.rect.size;
            rect.sizeDelta = new Vector2(size * px.x / px.y, size);
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

            // 최종보스(루나벨)전에서만 전투 틀 뒤에 깔리는 배경(절망의 언덕). 틀 가운데가 투명해서 그 사이로 비친다.
            // 노드/초상화가 묻히지 않게 어둡게 깔고, 화면 비율이 달라도 빈틈 없이 덮는다(EnvelopeParent).
            var bgGO = new GameObject("BossBackground", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            var bgRect = (RectTransform)bgGO.transform;
            bgRect.SetParent(contentRect, false);
            battleBackground = bgGO.GetComponent<RawImage>();
            battleBackground.raycastTarget = false;
            battleBackground.color = BattleBackgroundTint;
            battleBackground.texture = Resources.Load<Texture2D>(LunabelBattleBackgroundPath);
            var bgFitter = bgGO.GetComponent<AspectRatioFitter>();
            bgFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            if (battleBackground.texture != null)
                bgFitter.aspectRatio = (float)battleBackground.texture.width / battleBackground.texture.height;
            bgGO.SetActive(false);

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
            // 루나벨에게 맞은 피해 숫자는 플레이어 초상화 칸 위쪽에서 떠오른다.
            playerDamageNumberLayer = CreateRect(contentRect, "PlayerDamageNumbers", 270f, 250f, 530f, 510f, typeof(RectTransform));

            phaseBannerText = CreateText(contentRect, "PhaseBanner", 600f, 160f, 1070f, 230f, 42f, Color.white, TextAnchor.MiddleCenter);
            phaseBannerText.fontStyle = FontStyle.Bold;

            // 그림마다 가로세로 비율이 달라(My Turn 2171x724, Enemy Turn 1862x845) 상자를 넉넉히 잡고
            // preserveAspect로 안쪽에 맞춰 띄운다 — 어느 쪽이 떠도 찌그러지거나 잘리지 않는다.
            turnBannerImage = CreateImage(contentRect, "TurnBanner", 336f, 77f, 1336f, 443f);
            turnBannerImage.preserveAspect = true;
            turnBannerImage.gameObject.SetActive(false);
            myTurnBannerSprite = Resources.Load<Sprite>("UI/MyTurnBanner");
            enemyTurnBannerSprite = Resources.Load<Sprite>("UI/EnemyTurnBanner");

            resultText = CreateText(contentRect, "ResultText", 550f, 380f, 1120f, 460f, 40f, Color.white, TextAnchor.MiddleCenter);
            resultText.fontStyle = FontStyle.Bold;

            judgmentImage = CreateImage(contentRect, "JudgmentLogo", 640f, 335f, 1030f, 505f);
            judgmentImage.preserveAspect = true;
            judgmentImage.gameObject.SetActive(false);

            for (int i = 0; i < HoldBarPoolSize; i++)
            {
                var bar = CreateMovableImage(contentRect, "HoldBar" + i, 1f);
                bar.rectTransform.pivot = new Vector2(0f, 0.5f);
                bar.gameObject.SetActive(false);
                holdBarPool[i] = bar;
            }

            var nodeSprite = Resources.Load<Sprite>("UI/NodeDiamond");
            defaultNoteSprite = nodeSprite;
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

            // 보스 필살기 검기. 원화는 오른쪽으로 굴러가는 모양이라, 왼쪽의 플레이어 쪽으로 날아가게 좌우로 뒤집는다.
            swordWaveImage = CreateMovableImage(contentRect, "BossSwordWave", 1f);
            swordWaveImage.rectTransform.sizeDelta = new Vector2(SwordWaveWidth, SwordWaveHeight) * PxToCanvas;
            swordWaveImage.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            swordWaveImage.gameObject.SetActive(false);

            // 루나벨의 피의 손. 원화는 갈퀴가 오른쪽을 향해 있어서, 왼쪽의 플레이어 쪽으로 날아가게 좌우로 뒤집는다.
            Vector2 bloodHandSize = bloodHandFrames.Length > 0
                ? new Vector2(BloodHandHeight * bloodHandFrames[0].rect.width / bloodHandFrames[0].rect.height, BloodHandHeight)
                : new Vector2(BloodHandHeight * 0.6f, BloodHandHeight);
            for (int i = 0; i < BloodHandPoolSize; i++)
            {
                var hand = CreateMovableImage(contentRect, "BloodHand" + i, 1f);
                hand.rectTransform.sizeDelta = bloodHandSize * PxToCanvas;
                hand.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                hand.gameObject.SetActive(false);
                bloodHandPool[i] = hand;
            }

            // 루나벨 2페이즈 필살기의 핏빛 구체. 크기는 날아가는 동안 BloodOrbRoutine이 정한다.
            bloodOrbImage = CreateMovableImage(contentRect, "BloodOrb", 1f);
            // 필살기의 피의 기둥. 플레이어 발밑에 바닥 가운데를 붙이고, 크기는 프레임마다 SetBloodPillarFrame이 정한다.
            bloodPillarImage = CreateMovableImage(contentRect, "BloodPillar", 1f);
            bloodPillarImage.rectTransform.pivot = new Vector2(0.5f, 0f);
            bloodPillarImage.rectTransform.anchoredPosition = ImageToCanvasPos(BloodPillarX, BloodPillarBottomY);
            bloodPillarImage.gameObject.SetActive(false);
            bloodOrbImage.gameObject.SetActive(false);

            BuildMyTurnFrame(contentRect);

            // "BATTLE!!" 슬램 연출. 암전 직후, 전투 UI(contentRoot)가 드러나기 전에 잠깐 재생된다.
            // 프레임 한 장이 세로로 긴 비율(307:512)이라 그에 맞춰 박스도 세로로 길게 잡는다.
            battleSplashImage = CreateImage(rect, "BattleSplash", 656f, 150f, 1016f, 750f);
            battleSplashImage.preserveAspect = true;
            battleSplashImage.gameObject.SetActive(false);
            lunabelSplashImage = CreateImage(rect, "LunabelBattleSplash", 336f, 230f, 1336f, 711f);
            lunabelSplashImage.preserveAspect = true;
            lunabelSplashImage.gameObject.SetActive(false);

            BuildLunabelNameCard(rect);

            // 궁극기 각성용 암전 판. 맨 마지막에 만들어 다른 모든 요소 위에 그려지고, 평소엔 완전히 투명.
            var flashGO = new GameObject("UltimateFlash", typeof(RectTransform), typeof(Image));
            var flashRect = (RectTransform)flashGO.transform;
            flashRect.SetParent(rect, false);
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            ultimateFlashOverlay = flashGO.GetComponent<Image>();
            ultimateFlashOverlay.color = new Color(0f, 0f, 0f, 0f);
            ultimateFlashOverlay.raycastTarget = false;

            // 궁극기 각성 때 EyeFlash 프레임을 화면 가득 보여줄 전용 이미지. 암전 판 바로 위라 암전된
            // 검은 배경 위에서 깜빡인다. 세로가 긴 그림이라 preserveAspect로 두면 화면보다 좁아 양옆에
            // 여백이 남는데, 그 여백도 뒤의 암전 판 덕분에 검게 메워져 자연스럽게 화면을 꽉 채운 것처럼 보인다.
            var eyeFullGO = new GameObject("UltimateEyeFullscreen", typeof(RectTransform), typeof(Image));
            var eyeFullRect = (RectTransform)eyeFullGO.transform;
            eyeFullRect.SetParent(rect, false);
            eyeFullRect.anchorMin = Vector2.zero;
            eyeFullRect.anchorMax = Vector2.one;
            eyeFullRect.offsetMin = Vector2.zero;
            eyeFullRect.offsetMax = Vector2.zero;
            ultimateEyeFullscreenImage = eyeFullGO.GetComponent<Image>();
            ultimateEyeFullscreenImage.preserveAspect = true;
            ultimateEyeFullscreenImage.raycastTarget = false;
            ultimateEyeFullscreenImage.gameObject.SetActive(false);

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

            var lockGO = new GameObject("FleeLock", typeof(RectTransform), typeof(RawImage));
            var lockRect = (RectTransform)lockGO.transform;
            lockRect.SetParent(panelRect, false);
            lockRect.anchorMin = new Vector2(FleeLockX0 / ActionSelectImageWidth, 1f - FleeLockY1 / ActionSelectImageHeight);
            lockRect.anchorMax = new Vector2(FleeLockX1 / ActionSelectImageWidth, 1f - FleeLockY0 / ActionSelectImageHeight);
            lockRect.offsetMin = Vector2.zero;
            lockRect.offsetMax = Vector2.zero;
            fleeLockImage = lockGO.GetComponent<RawImage>();
            fleeLockImage.raycastTarget = false;
            for (int i = 0; i < FleeLockFrameCount; i++) fleeLockFrames[i] = Resources.Load<Texture2D>(FleeLockPath + (i + 1));
            lockGO.SetActive(false);
            // 선택 빛줄기는 잠금 그림 위로 보이게 한다.
            actionCursor.SetAsLastSibling();

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
        private static Sprite[] LoadSortedSprites(string path)
        {
            var sprites = Resources.LoadAll<Sprite>(path);
            System.Array.Sort(sprites, (x, y) => string.CompareOrdinal(x.name, y.name));
            return sprites;
        }

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
