using UnityEngine;

namespace Aethoria.Data
{
    // 이리아의 축복(특성 트리). 레벨업할 때마다 축복 점수 1점(레벨 n이면 n-1점)이 생기고, 이리아에게 가서
    // 세 갈래(심판/수호/율동) 중 원하는 특성에 찍는다. 갈래마다 4단계이고, 그 갈래에 쓴 점수가 TierRequirement를
    // 넘어야 다음 단계가 열린다. 2단계는 둘 중 하나만 고르는 갈림길, 4단계는 갈래의 핵심 특성이다.
    // 갈래 하나를 다 채우는 데 11점, 셋 다 채우려면 33점(레벨 34)이라 어디에 몰아줄지가 빌드가 된다.
    // 찍은 점수는 골드를 내고 초기화할 수 있다(ResetCost).
    //
    // 스탯 특성 한 단계의 가치는 예전 축복 한 번(아이템 점수 약 5점)과 같게 맞췄다:
    //   공격력 +1 / 치명타 확률 +3% / 치명타 피해 +10% / 방어력 +2 / 최대 체력 +5% / 스킬 피해 +5%.
    public enum TalentBranch { Judgment, Guard, Rhythm }

    public enum TalentId
    {
        // 심판(공격)
        SharpScythe, ReaperEye, Executioner, FirstStrike, DeepWound, Execution,
        // 수호(생존)
        IronSkin, Vitality, Bloodthirst, HolyPotion, Steadfast, Undying,
        // 율동(리듬·스킬)
        Resonance, Pulse, Perfectionist, Tempo, SoulFlow, DeathMelody,
    }

    public sealed class TalentNode
    {
        public TalentId Id;
        public TalentBranch Branch;
        public int Tier;            // 0~3
        public int Slot;            // 같은 단계에 둘이면 0(왼쪽)/1(오른쪽), 하나면 -1(가운데)
        public int MaxRank;
        public string Name;
        public float PerRank;       // 스탯 특성의 한 단계당 수치(설명용)
        public string Suffix;
        public string Desc;         // {0} 자리에 지금 단계 수치가 들어간다(스탯 특성)
        public TalentId? Rival;     // 갈림길 상대(이쪽을 찍으면 상대는 잠긴다)
        public bool IsKeystone => Tier == 3;
    }

    public static class BlessingRules
    {
        public const int TalentCount = 18;
        // 갈래에 쓴 점수가 이만큼 이상이어야 그 단계 특성을 찍을 수 있다.
        public static readonly int[] TierRequirement = { 0, 3, 6, 9 };
        public static readonly string[] BranchNames = { "심판", "수호", "율동" };
        public static readonly string[] BranchDescs = { "공격 · 치명타", "방어 · 생존", "리듬 · 스킬" };
        public static readonly Color[] BranchColors =
        {
            new Color(0.93f, 0.36f, 0.42f),   // 심판: 핏빛 진홍
            new Color(0.95f, 0.78f, 0.38f),   // 수호: 성스러운 금빛
            new Color(0.62f, 0.48f, 0.98f),   // 율동: 보랏빛
        };

        // 특수 특성 수치(효과 코드와 설명이 같이 쓴다).
        public const float ExecutionerHpRatio = 0.5f, ExecutionerBonus = 0.15f;
        public const float FirstStrikeBonus = 0.3f;
        public const float ExecutionHpRatio = 0.25f;
        public const float BloodthirstRatio = 0.03f;
        public const float HolyPotionBonus = 0.4f;
        public const float UndyingCooldown = 120f;
        public const float PerfectionistMultiplier = 1.4f;
        public const float TempoComboCap = 0.5f;
        public const float DeathMelodyFinisherBonus = 0.5f, DeathMelodyGauge = 15f;

        public static readonly TalentNode[] Nodes =
        {
            // ---- 심판
            N(TalentId.SharpScythe, TalentBranch.Judgment, 0, 0, 3, "날 선 낫", 1f, "", "공격력 +{0}"),
            N(TalentId.ReaperEye, TalentBranch.Judgment, 0, 1, 3, "사신의 눈", 3f, "%", "치명타 확률 +{0}"),
            N(TalentId.Executioner, TalentBranch.Judgment, 1, 0, 1, "처형인", 0f, "", $"체력이 절반 이하인 적에게 주는 피해 +{ExecutionerBonus * 100f:0}%", TalentId.FirstStrike),
            N(TalentId.FirstStrike, TalentBranch.Judgment, 1, 1, 1, "첫 일격", 0f, "", $"체력이 가득 찬 적에게 주는 피해 +{FirstStrikeBonus * 100f:0}%", TalentId.Executioner),
            N(TalentId.DeepWound, TalentBranch.Judgment, 2, -1, 3, "깊은 상처", 10f, "%", "치명타 피해 +{0}"),
            N(TalentId.Execution, TalentBranch.Judgment, 3, -1, 1, "사신의 심판", 0f, "", $"체력이 {ExecutionHpRatio * 100f:0}% 이하인 적에게는 반드시 치명타가 터진다"),
            // ---- 수호
            N(TalentId.IronSkin, TalentBranch.Guard, 0, 0, 3, "강철 피부", 2f, "", "방어력 +{0}"),
            N(TalentId.Vitality, TalentBranch.Guard, 0, 1, 3, "생명의 가호", 5f, "%", "최대 체력 +{0}"),
            N(TalentId.Bloodthirst, TalentBranch.Guard, 1, 0, 1, "피의 갈증", 0f, "", $"적에게 준 피해의 {BloodthirstRatio * 100f:0}%만큼 체력 회복", TalentId.HolyPotion),
            N(TalentId.HolyPotion, TalentBranch.Guard, 1, 1, 1, "성수", 0f, "", $"포션 회복량 +{HolyPotionBonus * 100f:0}%", TalentId.Bloodthirst),
            N(TalentId.Steadfast, TalentBranch.Guard, 2, -1, 3, "굳건함", 4f, "%", "받는 피해 -{0}"),
            N(TalentId.Undying, TalentBranch.Guard, 3, -1, 1, "불사의 가호", 0f, "", $"쓰러질 피해를 받으면 체력 1로 버틴다 ({UndyingCooldown:0}초에 한 번)"),
            // ---- 율동
            N(TalentId.Resonance, TalentBranch.Rhythm, 0, 0, 3, "어둠의 공명", 5f, "%", "스킬 피해 +{0}"),
            N(TalentId.Pulse, TalentBranch.Rhythm, 0, 1, 3, "고동", 10f, "%", "필살 게이지 획득량 +{0}"),
            N(TalentId.Perfectionist, TalentBranch.Rhythm, 1, 0, 1, "완벽주의", 0f, "", $"PERFECT 판정 피해 배율 x1.2 → x{PerfectionistMultiplier:0.0}", TalentId.Tempo),
            N(TalentId.Tempo, TalentBranch.Rhythm, 1, 1, 1, "박자 유지", 0f, "", $"콤보 보너스 최대치 30% → {TempoComboCap * 100f:0}%", TalentId.Perfectionist),
            N(TalentId.SoulFlow, TalentBranch.Rhythm, 2, -1, 3, "영혼 순환", 20f, "%", "마나 회복 속도 +{0}"),
            N(TalentId.DeathMelody, TalentBranch.Rhythm, 3, -1, 1, "죽음의 선율", 0f, "", $"풀 콤보 마무리 일격 피해 +{DeathMelodyFinisherBonus * 100f:0}%, 필살 게이지 +{DeathMelodyGauge:0}"),
        };

        private static TalentNode N(TalentId id, TalentBranch branch, int tier, int slot, int maxRank, string name,
            float perRank, string suffix, string desc, TalentId? rival = null)
            => new TalentNode { Id = id, Branch = branch, Tier = tier, Slot = slot, MaxRank = maxRank, Name = name,
                PerRank = perRank, Suffix = suffix, Desc = desc, Rival = rival };

        public static TalentNode Node(TalentId id) => Nodes[(int)id];

        // 스탯 특성이면 그 단계의 수치를 넣은 설명, 특수 특성이면 설명 그대로.
        public static string Describe(TalentNode node, int rank)
            => node.PerRank > 0f ? string.Format(node.Desc, (node.PerRank * rank).ToString("0.#") + node.Suffix) : node.Desc;

        // 레벨 n이면 축복 점수 n-1점.
        public static int PointsForLevel(int level) => Mathf.Max(0, level - 1);

        // 초기화 비용: 찍은 점수 1점당 100골드.
        public static int ResetCost(int spentPoints) => 100 * Mathf.Max(0, spentPoints);

        // ---- 예전 축복(골드로 기도하던 방식)의 환불 계산. 그 세이브를 불러오면 쓴 골드를 전부 돌려준다.
        // 첫 방식(총 단계 하나): n번 기도에 200 + 400 + … + 200n.
        public static int TotalGoldSpent(int totalLevel)
        {
            int n = Mathf.Clamp(totalLevel, 0, 20);
            return 100 * n * (n + 1);
        }
    }
}
