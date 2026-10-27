using System;
using UnityEngine;
using Aethoria.Combat;
using Aethoria.Data;

namespace Aethoria.Monsters
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class Monster : MonoBehaviour
    {
        [SerializeField] private MonsterData data;

        private float currentHp;

        public MonsterData Data => data;
        public float CurrentHp => currentHp;
        public float MaxHp => data != null ? data.maxHp : 0f;
        public float Attack => data != null ? data.attack : 0f;
        public float Defense => data != null ? data.defense : 0f;
        public bool IsDead => currentHp <= 0f;

        // 사슬 갈고리 등에 꽂혀 있는 동안 켜진다. AI는 이 동안 이동/새 공격 시작을 멈춘다
        // (이미 시작된 공격 모션까지 끊지는 않는다 - 피격 경직과 같은 방식).
        public bool IsImmobilized { get; private set; }
        public void SetImmobilized(bool immobilized) => IsImmobilized = immobilized;

        // false면 쓰러져도 오브젝트를 바로 지우지 않는다(보스 기사처럼 쓰러진 뒤 월드에서 유언/사망 모션을 보여 줄 때).
        // 그 경우 연출을 맡은 쪽(BossDeathCutscene)이 끝난 뒤 직접 지운다.
        public bool DestroyOnDeath { get; set; } = true;

        // true면 쓰러질 만큼의 피해를 받아도 체력 1로 버틴다(루나벨이 1페이즈에서 한 방에 죽지 않고 2페이즈로 넘어가게).
        public bool SurviveLethalHit { get; set; }

        // true면 스킬이 아닌 공격(기본 공격)으로는 체력 1에서 버티고, 스킬 피해로만 쓰러진다(튜토리얼 숲의 정령).
        public bool SkillFinishOnly { get; set; }

        // 릴리의 리듬 대결(미니게임)에 쓰려고 잠깐 만든 상대. 쓰러져도 AnyDied를 알리지 않아서
        // 경험치/골드/드랍/퀘스트/도감에 잡히지 않고, 각 AI도 필드에서 움직이거나 공격하지 않는다.
        public bool IsMinigameOpponent { get; set; }

        // true면 피해를 전혀 받지 않는다(알리아스가 종막 의식으로 무릎 꿇고 있는 동안처럼 다른 걸 부숴야 하는 패턴).
        public bool Invulnerable { get; set; }

        // false면 쓰러져도 AnyDied를 알리지 않는다 — 패턴이 만든 부술 대상(알리아스의 마검)이 경험치/드랍/퀘스트/도감에 잡히지 않게.
        public bool CountsAsKill { get; set; } = true;

        public event Action<Monster> OnDied;
        // 어느 몬스터든 죽으면 발생한다. 퀘스트처럼 스테이지와 상관없이 처치 수를 세야 하는 쪽에서 구독한다.
        public static event Action<Monster> AnyDied;
        public event Action<Monster, float> OnDamaged;

        private void Awake()
        {
            if (data == null)
            {
                Debug.LogWarning($"{name}: MonsterData가 할당되지 않았습니다.", this);
            }

            currentHp = MaxHp;
        }

        public void AssignData(MonsterData newData)
        {
            data = newData;
            currentHp = MaxHp;
        }

        private static readonly Color CriticalPopupColor = new Color(1f, 0.55f, 0.15f);

        // 방금 받은 피해가 치명타였는지. OnDamaged를 받는 쪽(리듬 전투의 데미지 숫자)이 치명타 숫자를 고를 때 쓴다.
        public bool LastHitWasCritical { get; private set; }

        // 치명타면 데미지 숫자를 주황색으로 크게 띄운다.
        public void TakeDamage(float finalDamage, bool isCritical = false, bool isSkill = false)
        {
            if (IsDead || Invulnerable || finalDamage <= 0f) return;

            bool survive = SurviveLethalHit || (SkillFinishOnly && !isSkill);
            currentHp = Mathf.Max(survive ? Mathf.Min(1f, currentHp) : 0f, currentHp - finalDamage);
            LastHitWasCritical = isCritical;
            OnDamaged?.Invoke(this, finalDamage);
            if (isCritical) DamagePopup.Create(transform.position, finalDamage, CriticalPopupColor, 1.4f);
            else DamagePopup.Create(transform.position, finalDamage, Color.white);

            if (currentHp <= 0f)
            {
                OnDied?.Invoke(this);
                if (!IsMinigameOpponent && CountsAsKill) AnyDied?.Invoke(this);
                if (DestroyOnDeath) Destroy(gameObject);
            }
        }

        // 연습장 허수아비처럼 절대 쓰러지지 않아야 하는 대상이 맞을 때마다 체력을 되돌린다.
        // OnDamaged 안에서 부르면 TakeDamage의 사망 판정보다 먼저 체력이 차므로 한 방에 0이 되어도 쓰러지지 않는다.
        public void RestoreFullHp()
        {
            currentHp = MaxHp;
        }

        // 보스의 흡혈 패턴처럼 체력을 일부 되찾는다(최대 체력을 넘지 않음).
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            currentHp = Mathf.Min(MaxHp, currentHp + amount);
        }

        [ContextMenu("Test/Take 5 Damage")]
        private void DebugTakeDamage() => TakeDamage(5f);
    }
}
