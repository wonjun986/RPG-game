using System;
using UnityEngine;
using Aethoria.Characters;
using Aethoria.Monsters;

namespace Aethoria.Quests
{
    public enum DailyQuestStatus
    {
        Available,      // 오늘 아직 받지 않음
        InProgress,     // 수락함, 처치 수 세는 중
        ReadyToTurnIn,  // 목표 달성, 노아에게 보고하면 보상
        Claimed,        // 오늘 치를 이미 끝냄(다음 날 자정 지나면 새로 뽑힌다)
    }

    // QuestManager(메인 퀘스트)와 별개로 동시에 진행되는 일일 의뢰 하나를 들고 있는 관리자.
    // 기기 날짜가 바뀌면 풀(DailyQuestDatabase)에서 무작위로 하나를 새로 뽑아 초기화한다.
    // 날짜 전환은 일정 주기로 스스로 확인하고, 노아와 대화를 시작할 때도 한 번 더 확인한다.
    public class DailyQuestManager : MonoBehaviour
    {
        private const float ResetCheckInterval = 30f;

        private Character player;
        private QuestData[] pool;
        private int questId = -1;
        private int progress;
        private DailyQuestStatus status;
        private string resetDate; // "yyyy-MM-dd", 지금 의뢰가 뽑힌 날짜
        private float resetCheckTimer;

        // 수락/진행/완료/보고, 날짜 전환 등 상태가 바뀔 때마다 발생한다. 추적 UI와 NPC 머리 위 표시가 구독한다.
        public event Action OnChanged;

        public QuestData Current => pool != null && questId >= 0 && questId < pool.Length ? pool[questId] : null;
        public DailyQuestStatus Status => status;
        public int Progress => progress;
        public int QuestIndex => questId;
        public string ResetDate => resetDate;

        public void Initialize(Character playerCharacter, QuestData[] questPool)
        {
            player = playerCharacter;
            pool = questPool;
            PickNewQuest();
        }

        // 저장 파일에서 이어하기. 저장된 날짜가 오늘이 아니면 새 날로 보고 새 의뢰를 뽑는다.
        public void RestoreState(string savedResetDate, int savedIndex, DailyQuestStatus savedStatus, int savedProgress)
        {
            if (pool == null || pool.Length == 0 || string.IsNullOrEmpty(savedResetDate) || savedResetDate != TodayString())
            {
                PickNewQuest();
                return;
            }

            questId = Mathf.Clamp(savedIndex, 0, pool.Length - 1);
            status = savedStatus;
            progress = Mathf.Clamp(savedProgress, 0, Current.requiredCount);
            resetDate = savedResetDate;
            OnChanged?.Invoke();
        }

        private void OnEnable()
        {
            Monster.AnyDied += HandleMonsterDied;
        }

        private void OnDisable()
        {
            Monster.AnyDied -= HandleMonsterDied;
        }

        private void Update()
        {
            resetCheckTimer += Time.deltaTime;
            if (resetCheckTimer < ResetCheckInterval) return;

            resetCheckTimer = 0f;
            EnsureFreshToday();
        }

        // 날짜가 바뀌었으면 새 의뢰로 초기화한다. 노아에게 말을 걸 때도 미리 호출해 둔다.
        public void EnsureFreshToday()
        {
            if (resetDate == TodayString()) return;
            PickNewQuest();
        }

        private void PickNewQuest()
        {
            questId = pool != null && pool.Length > 0 ? UnityEngine.Random.Range(0, pool.Length) : -1;
            progress = 0;
            status = DailyQuestStatus.Available;
            resetDate = TodayString();
            OnChanged?.Invoke();
        }

        private static string TodayString() => DateTime.Now.ToString("yyyy-MM-dd");

        public void Accept()
        {
            if (status != DailyQuestStatus.Available) return;

            progress = 0;
            status = DailyQuestStatus.InProgress;
            OnChanged?.Invoke();
        }

        // 보상을 지급하고 오늘 치를 끝낸 것으로 표시한다. 보고한 의뢰를 돌려준다(대사에 쓰기 위해).
        public QuestData TurnIn()
        {
            if (status != DailyQuestStatus.ReadyToTurnIn) return null;

            var finished = Current;
            if (player != null)
            {
                player.AddExp(ExperienceMath.GetQuestExp(player.Level, finished.expReward));
                if (finished.goldReward > 0) player.AddGold(finished.goldReward);
            }

            status = DailyQuestStatus.Claimed;
            OnChanged?.Invoke();
            return finished;
        }

        private void HandleMonsterDied(Monster monster)
        {
            if (status != DailyQuestStatus.InProgress) return;
            if (monster.Data == null || Current == null || monster.Data.monsterName != Current.targetMonsterName) return;

            progress = Mathf.Min(progress + 1, Current.requiredCount);
            if (progress >= Current.requiredCount) status = DailyQuestStatus.ReadyToTurnIn;
            OnChanged?.Invoke();
        }
    }
}
