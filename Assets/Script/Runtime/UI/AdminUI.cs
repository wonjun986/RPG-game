using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Data;
using Aethoria.Monsters;
using Aethoria.Quests;

namespace Aethoria.UI
{
    // 테스트용 관리자 모드. F1로 화면 오른쪽에 명령 목록을 여닫고, 열려 있는 동안 F2~F12로 바로 실행한다.
    // 숫자/Z/X 키는 NPC 대화·리듬 전투와 겹치므로 기능 키만 쓴다. 순간이동(F8)은 목록을 펼친 뒤
    // PageUp/PageDown 으로 고르고 F8을 한 번 더 눌러 이동한다. 창이 떠 있어도 게임은 멈추지 않는다.
    public class AdminUI : MonoBehaviour
    {
        private const int GoldAmount = 10000;
        private const float MessageDuration = 2f;

        private Character player;
        private RhythmBattleUI rhythmBattleUI;
        private QuestManager questManager;

        private GameObject panel;
        private Text listText;
        private Text messageText;
        private float messageUntil;

        private bool warpListOpen;
        private int warpIndex;

        public void Initialize(Character playerCharacter)
        {
            player = playerCharacter;
            rhythmBattleUI = GetComponent<RhythmBattleUI>();
            questManager = GetComponent<QuestManager>();
            BuildPanel();
        }

        private void BuildPanel()
        {
            // 리듬 전투 화면(SetAsLastSibling)보다도 위에 그려지도록 따로 정렬 순서를 높인 캔버스를 둔다.
            var canvasGO = new GameObject("AdminCanvas", typeof(RectTransform), typeof(Canvas));
            var canvasRect = (RectTransform)canvasGO.transform;
            canvasRect.SetParent(transform, false);
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;

            panel = new GameObject("AdminPanel", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(canvasRect, false);
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-12f, 0f);
            rect.sizeDelta = new Vector2(360f, 660f);
            var image = panel.GetComponent<Image>();
            image.color = new Color(0.05f, 0.03f, 0.08f, 0.88f);
            image.raycastTarget = false;

            listText = BuildText(rect, "List", new Vector2(0f, 0.09f), Vector2.one, 17, Color.white, TextAnchor.UpperLeft);
            messageText = BuildText(rect, "Message", Vector2.zero, new Vector2(1f, 0.09f), 16, new Color(0.45f, 0.95f, 0.5f), TextAnchor.MiddleLeft);

            panel.SetActive(false);
        }

        private static Text BuildText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(16f, 10f);
            rect.offsetMax = new Vector2(-16f, -12f);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || player == null) return;

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                panel.SetActive(!panel.activeSelf);
                warpListOpen = false;
                Refresh();
            }
            if (!panel.activeSelf) return;

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                player.AddGold(GoldAmount);
                ShowMessage($"골드 +{GoldAmount:N0}");
            }
            else if (keyboard.f3Key.wasPressedThisFrame)
            {
                player.LevelUp();
                ShowMessage($"레벨 {player.Level}");
            }
            else if (keyboard.f4Key.wasPressedThisFrame)
            {
                player.Heal(player.MaxHp);
                player.RestoreMana(player.MaxMana);
                ShowMessage("체력/마나 회복");
            }
            else if (keyboard.f5Key.wasPressedThisFrame)
            {
                player.GodMode = !player.GodMode;
                ShowMessage(player.GodMode ? "무적 켜짐" : "무적 꺼짐");
            }
            else if (keyboard.f6Key.wasPressedThisFrame)
            {
                KillEnemies();
            }
            else if (keyboard.f7Key.wasPressedThisFrame)
            {
                bool filled = rhythmBattleUI != null && rhythmBattleUI.AdminFillSkillGauge();
                ShowMessage(filled ? "필살 게이지 가득" : "전투 중에만 쓸 수 있어요", filled);
            }
            else if (keyboard.f8Key.wasPressedThisFrame)
            {
                if (!warpListOpen)
                {
                    warpListOpen = true;
                }
                else if (GameBootstrap.AdminWarp(warpIndex))
                {
                    warpListOpen = false;
                    ShowMessage($"{GameBootstrap.AdminWarpName(warpIndex)}(으)로 이동");
                }
                else
                {
                    ShowMessage("전투·대화·메뉴 중에는 이동할 수 없어요", false);
                }
            }
            else if (keyboard.f9Key.wasPressedThisFrame)
            {
                WorldMapUI.AdminUnlockAll = !WorldMapUI.AdminUnlockAll;
                ShowMessage(WorldMapUI.AdminUnlockAll ? "월드맵 전 지역 열림" : "월드맵 잠금을 스토리 진행대로 되돌림");
            }
            else if (keyboard.f10Key.wasPressedThisFrame)
            {
                if (questManager == null || questManager.Current == null)
                {
                    ShowMessage("진행할 메인 퀘스트가 없어요", false);
                }
                else
                {
                    questManager.AdminAdvance();
                    ShowMessage(questManager.Current != null ? $"퀘스트: {questManager.Current.title} ({StatusName(questManager.Status)})" : "메인 퀘스트 전부 완료");
                }
            }
            else if (keyboard.f11Key.wasPressedThisFrame)
            {
                Bestiary.AdminUnlockAll = !Bestiary.AdminUnlockAll;
                ShowMessage(Bestiary.AdminUnlockAll ? "도감 전부 열림(비밀 메모 포함)" : "도감을 처치 기록대로 되돌림");
            }
            else if (keyboard.f12Key.wasPressedThisFrame)
            {
                JumpBossToPhaseTwo();
            }
            else if (warpListOpen && keyboard.pageDownKey.wasPressedThisFrame)
            {
                warpIndex = (warpIndex + 1) % GameBootstrap.AdminWarpCount;
            }
            else if (warpListOpen && keyboard.pageUpKey.wasPressedThisFrame)
            {
                warpIndex = (warpIndex - 1 + GameBootstrap.AdminWarpCount) % GameBootstrap.AdminWarpCount;
            }

            if (messageText.text.Length > 0 && Time.unscaledTime > messageUntil) messageText.text = string.Empty;
            Refresh();
        }

        // 전투 중이면 지금 상대를, 아니면 맵에 있는 몬스터를 전부 쓰러뜨린다(연습장 허수아비 제외).
        // 쓰러뜨린 몬스터의 경험치/골드/드롭과 보스 사망 연출은 평소처럼 들어온다.
        private void KillEnemies()
        {
            if (rhythmBattleUI != null && rhythmBattleUI.IsOpen)
            {
                bool killedOpponent = rhythmBattleUI.AdminKillOpponent();
                ShowMessage(killedOpponent ? "상대 처치" : "처치할 수 없는 상대예요", killedOpponent);
                return;
            }

            int killed = 0;
            foreach (var monster in FindObjectsByType<Monster>(FindObjectsSortMode.None))
            {
                if (monster.IsDead || monster.GetComponent<TrainingDummy>() != null) continue;
                monster.SurviveLethalHit = false;
                monster.TakeDamage(monster.CurrentHp + monster.MaxHp);
                killed++;
            }
            ShowMessage(killed > 0 ? $"몬스터 {killed}마리 처치" : "이 맵에는 몬스터가 없어요", killed > 0);
        }

        // 이 맵의 보스를 2페이즈 직전까지 깎아 곧바로 2페이즈 연출을 보게 한다(보스방은 [F8] 순간이동으로 간다).
        // 알리아스: 체력을 절반 아래로 깎으면 하던 패턴 뒤에 검을 꽂고 분노한다.
        // 루나벨: 1페이즈 체력을 다 깎으면(체력 1로 버틴다) 그 자리에서 변신한다.
        // 등장 연출 중이면 연출이 끝나자마자 이어진다.
        private void JumpBossToPhaseTwo()
        {
            foreach (var monster in FindObjectsByType<Monster>(FindObjectsSortMode.None))
            {
                if (monster.IsDead || monster.IsMinigameOpponent) continue;

                var knight = monster.GetComponent<BossAI>();
                if (knight != null)
                {
                    if (knight.IsPhaseTwo)
                    {
                        ShowMessage("알리아스는 이미 2페이즈예요", false);
                        return;
                    }
                    float goal = monster.MaxHp * 0.5f - 1f;
                    if (monster.CurrentHp > goal) monster.TakeDamage(monster.CurrentHp - goal);
                    ShowMessage("알리아스 2페이즈(분노)로");
                    return;
                }

                var lunabel = monster.GetComponent<FinalBossAI>();
                if (lunabel != null)
                {
                    if (lunabel.PhaseTwoStarted)
                    {
                        ShowMessage("루나벨은 이미 2페이즈예요", false);
                        return;
                    }
                    monster.TakeDamage(monster.CurrentHp);
                    ShowMessage("루나벨 2페이즈(변신)로");
                    return;
                }
            }
            ShowMessage("이 맵에는 2페이즈가 있는 보스가 없어요 — [F8]로 보스방에 가 주세요", false);
        }

        private static string StatusName(QuestStatus status)
        {
            switch (status)
            {
                case QuestStatus.Available: return "받기 전";
                case QuestStatus.InProgress: return "진행 중";
                case QuestStatus.ReadyToTurnIn: return "보고 대기";
                default: return "끝";
            }
        }

        private void ShowMessage(string message, bool success = true)
        {
            messageText.text = message;
            messageText.color = success ? new Color(0.45f, 0.95f, 0.5f) : new Color(1f, 0.5f, 0.45f);
            messageUntil = Time.unscaledTime + MessageDuration;
        }

        private void Refresh()
        {
            if (!panel.activeSelf) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b><color=#E8C94A>관리자 모드</color></b>   <size=13>[F1] 닫기</size>");
            sb.AppendLine();
            sb.AppendLine($"[F2] 골드 +{GoldAmount:N0}   <size=13>(보유 {player.Gold:N0})</size>");
            sb.AppendLine($"[F3] 레벨 +1   <size=13>(현재 {player.Level})</size>");
            sb.AppendLine("[F4] 체력/마나 회복");
            sb.AppendLine($"[F5] 무적   {(player.GodMode ? "<color=#73F27F>ON</color>" : "<color=#888888>OFF</color>")}");
            sb.AppendLine("[F6] 적 즉사 <size=13>(전투 중: 상대 / 밖: 맵 전체)</size>");
            sb.AppendLine("[F7] 필살 게이지 가득 <size=13>(전투 중)</size>");
            sb.AppendLine($"[F9] 월드맵 전 지역 열기   {(WorldMapUI.AdminUnlockAll ? "<color=#73F27F>ON</color>" : "<color=#888888>OFF</color>")}");
            sb.AppendLine("[F10] 메인 퀘스트 한 단계 진행 <size=13>(수락→달성→보고)</size>");
            sb.AppendLine($"[F11] 도감 전부 열기   {(Bestiary.AdminUnlockAll ? "<color=#73F27F>ON</color>" : "<color=#888888>OFF</color>")}");
            sb.AppendLine("[F12] 보스 2페이즈로 <size=13>(알리아스 분노 / 루나벨 변신)</size>");
            sb.AppendLine(warpListOpen ? "[F8] 선택한 곳으로 이동   <size=13>PgUp/PgDn 고르기</size>" : "[F8] 순간이동 목록 열기");

            if (warpListOpen)
            {
                for (int i = 0; i < GameBootstrap.AdminWarpCount; i++)
                {
                    string targetName = GameBootstrap.AdminWarpName(i);
                    sb.AppendLine(i == warpIndex ? $"  <color=#E8C94A>▶ {targetName}</color>" : $"     <size=15>{targetName}</size>");
                }
            }
            listText.text = sb.ToString();
        }
    }
}
