using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 새 게임을 처음 시작했을 때만 한 번 뜨는 조작법 안내창(불러오기로 이어할 때는 뜨지 않는다).
    // 여러 장으로 나눠 보여주고, Z/Enter로 다음 장, X/Esc로 전부 건너뛴다.
    public class TutorialUI : MonoBehaviour
    {
        private readonly struct Page
        {
            public readonly string Title;
            public readonly string Body;
            public Page(string title, string body) { Title = title; Body = body; }
        }

        private static readonly Page[] Pages =
        {
            new Page("이동",
                "← →  이동\n↑  점프 (공중에서 한 번 더 누르면 이단 점프)\nShift  대시 (몬스터를 통과, 대시 중 무적)\n\n몬스터를 뛰어넘거나 대시로 지나가면 싸우지 않아요"),
            new Page("리듬 전투",
                "몬스터와 부딪히면 리듬 전투가 시작돼요\n노드가 판정점에 닿는 순간 키를 누르세요\n\n위 레인 = Z     아래 레인 = X\n\n너무 일찍 누르거나 놓치면 Miss!"),
            new Page("적의 턴 · 나의 턴",
                "적의 턴  노드를 맞혀 공격을 막아요\n(놓친 노드마다 피해를 입어요)\n\n나의 턴  노드 하나가 한 번의 공격이에요\nPerfect 1.2배 · Good 1배 · OK 0.7배 · Miss 0.3배\n\n둘 중 하나가 쓰러질 때까지 번갈아 진행돼요"),
            new Page("행동 선택",
                "나의 턴이 오면 ← →로 고르고 Z로 확정해요\n\n공격  리듬에 맞춰 공격, 필살 게이지가 차요\n스킬  필살 게이지로 강력한 스킬 사용\n아이템  회복 포션 (체력 +40, 턴 종료)\n도주  전투에서 빠져나와요 (언제든 Esc)"),
            new Page("스킬",
                "필살 게이지는 공격 턴에 노드를 맞히면 차고\n전투마다 0부터 다시 모아요\n\n악마의 손 20  ·  낫 부메랑 30\n어둠의 주먹 40  ·  사슬 폭풍 65\n죽음의 소용돌이 100 (궁극기)"),
            new Page("도주와 패배",
                "도망친 적은 3초 동안 깜빡여요\n깜빡이는 동안은 닿아도 싸우지 않고 지나갈 수 있어요\n\n체력이 0이 되면 쓰러져 마을로 돌아가요\n레벨 · 아이템 · 퀘스트는 그대로 남아요"),
            new Page("마을에서",
                "I  인벤토리 열기/닫기\nC  상태창 열기/닫기\n\n노아  상점(회복 포션) · 퀘스트 · 일일 의뢰 · 저장\n엘리  장비 강화\n이시스  연습장 — 허수아비와 리듬 전투 연습"),
        };

        private GameObject panel;
        private Text titleText;
        private Text bodyText;
        private Text pageText;
        private Text hintText;
        private CharacterMovement2D playerMovement;
        private int pageIndex;

        public void Initialize(CharacterMovement2D movement)
        {
            playerMovement = movement;
            BuildPanel();
        }

        private void BuildPanel()
        {
            panel = new GameObject("TutorialPanel", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

            var boxGO = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var boxRect = (RectTransform)boxGO.transform;
            boxRect.SetParent(rect, false);
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(760f, 420f);
            boxGO.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.09f, 0.97f);

            titleText = BuildText(boxRect, "Title", new Vector2(0f, 0.82f), new Vector2(1f, 1f), 30, new Color(0.95f, 0.85f, 0.2f), FontStyle.Bold, TextAnchor.MiddleCenter);
            bodyText = BuildText(boxRect, "Body", new Vector2(0f, 0.22f), new Vector2(1f, 0.8f), 24, Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
            pageText = BuildText(boxRect, "PageIndex", new Vector2(0f, 0.1f), new Vector2(1f, 0.2f), 18, new Color(0.7f, 0.65f, 0.8f), FontStyle.Normal, TextAnchor.MiddleCenter);
            hintText = BuildText(boxRect, "Hint", new Vector2(0f, 0f), new Vector2(1f, 0.12f), 20, new Color(0.8f, 0.8f, 0.8f), FontStyle.Bold, TextAnchor.MiddleCenter);

            panel.SetActive(false);
        }

        private static Text BuildText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize, Color color, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(32f, 0f);
            rect.offsetMax = new Vector2(-32f, 0f);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        public void Show()
        {
            pageIndex = 0;
            panel.SetActive(true);
            RefreshPage();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        private void Hide()
        {
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private void RefreshPage()
        {
            var page = Pages[pageIndex];
            titleText.text = page.Title;
            bodyText.text = page.Body;
            pageText.text = $"{pageIndex + 1} / {Pages.Length}";
            hintText.text = pageIndex < Pages.Length - 1 ? "[Z] 다음   [X] 건너뛰기" : "[Z] 시작하기";
        }

        private void Update()
        {
            if (panel == null || !panel.activeSelf) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                Hide();
                return;
            }

            if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
            {
                if (pageIndex < Pages.Length - 1)
                {
                    pageIndex++;
                    RefreshPage();
                }
                else
                {
                    Hide();
                }
            }
        }
    }
}
