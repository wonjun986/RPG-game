using System;
using UnityEngine;
using Aethoria.Characters;
using Aethoria.Monsters;

namespace Aethoria.Quests
{
    public enum QuestStatus
    {
        Available,      // 노아에게 받을 수 있음
        InProgress,     // 수락함, 처치 수 세는 중
        ReadyToTurnIn,  // 목표 달성, 노아에게 보고하면 보상
        AllDone,        // 준비된 퀘스트를 전부 끝냄
    }

    // 퀘스트 진행 상태를 들고 있는 관리자. HUD 캔버스에 붙어서 한 판(RETRY/TITLE 전까지) 동안 유지된다.
    // 어느 스테이지에서 잡든 Monster.AnyDied로 처치 수를 센다.
    public class QuestManager : MonoBehaviour
    {
        private Character player;
        private QuestData[] quests;
        private int questIndex;
        private int progress;
        private QuestStatus status;

        // 수락/진행/완료/보고 등 상태가 바뀔 때마다 발생한다. 추적 UI와 NPC 머리 위 표시가 구독한다.
        public event Action OnChanged;

        public QuestData Current => quests != null && questIndex < quests.Length ? quests[questIndex] : null;
        public QuestStatus Status => status;
        public int Progress => progress;
        public int QuestIndex => questIndex;

        public void Initialize(Character playerCharacter, QuestData[] questLine)
        {
            player = playerCharacter;
            quests = questLine;
            questIndex = 0;
            progress = 0;
            status = Current != null ? QuestStatus.Available : QuestStatus.AllDone;
        }

        // 저장 파일에서 이어하기. 퀘스트 목록이 바뀌어 인덱스가 범위를 벗어나면 전부 끝낸 것으로 본다.
        public void RestoreState(int savedIndex, QuestStatus savedStatus, int savedProgress)
        {
            questIndex = Mathf.Max(0, savedIndex);
            if (Current == null)
            {
                progress = 0;
                status = QuestStatus.AllDone;
            }
            else
            {
                status = savedStatus == QuestStatus.AllDone ? QuestStatus.Available : savedStatus;
                progress = Mathf.Clamp(savedProgress, 0, Current.requiredCount);
            }
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

        public void Accept()
        {
            if (status != QuestStatus.Available) return;

            progress = 0;
            status = QuestStatus.InProgress;
            OnChanged?.Invoke();
        }

        // 보상을 지급하고 다음 퀘스트를 연다. 보고한 퀘스트를 돌려준다(대사에 쓰기 위해).
        public QuestData TurnIn()
        {
            if (status != QuestStatus.ReadyToTurnIn) return null;

            var finished = Current;
            if (player != null) player.AddExp(ExperienceMath.GetQuestExp(player.Level, finished.expReward));

            questIndex++;
            progress = 0;
            status = Current != null ? QuestStatus.Available : QuestStatus.AllDone;
            OnChanged?.Invoke();
            return finished;
        }

        // 이 NPC에게 말을 걸면 퀘스트 장면이 나와야 하는지(의뢰할 퀘스트, 찾아온 Talk 퀘스트, 보고받을 퀘스트). NPC 머리 위 표시에도 쓴다.
        public bool HasBusinessWith(string npc)
        {
            var quest = Current;
            if (quest == null) return false;
            switch (status)
            {
                case QuestStatus.Available: return quest.giverNpc == npc;
                case QuestStatus.InProgress: return quest.type == QuestType.Talk && quest.targetNpc == npc;
                case QuestStatus.ReadyToTurnIn: return quest.turnInNpc == npc;
                default: return false;
            }
        }

        // Talk 퀘스트의 목표 NPC에게 말을 걸었을 때 부른다. 목표가 맞으면 보고 가능 상태로 바꾸고 true.
        public bool CompleteTalk(string npc)
        {
            var quest = Current;
            if (status != QuestStatus.InProgress || quest == null || quest.type != QuestType.Talk || quest.targetNpc != npc) return false;

            status = QuestStatus.ReadyToTurnIn;
            OnChanged?.Invoke();
            return true;
        }

        // 관리자 모드(AdminUI): 지금 퀘스트를 한 단계 진행한다(수락 → 목표 달성 → 보고).
        public void AdminAdvance()
        {
            switch (status)
            {
                case QuestStatus.Available:
                    Accept();
                    break;
                case QuestStatus.InProgress:
                    progress = Current.requiredCount;
                    status = QuestStatus.ReadyToTurnIn;
                    OnChanged?.Invoke();
                    break;
                case QuestStatus.ReadyToTurnIn:
                    TurnIn();
                    break;
            }
        }

        private void HandleMonsterDied(Monster monster)
        {
            if (status != QuestStatus.InProgress || Current.type != QuestType.Kill) return;
            if (monster.Data == null || monster.Data.monsterName != Current.targetMonsterName) return;

            progress = Mathf.Min(progress + 1, Current.requiredCount);
            if (progress >= Current.requiredCount) status = QuestStatus.ReadyToTurnIn;
            OnChanged?.Invoke();
        }
    }
}
