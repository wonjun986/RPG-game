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
                "← →  이동\n↑  점프 (공중에서 한 번 더 누르면 이단 점프)\nShift  대시 (몬스터를 관통하며 피해, 대시 중 무적)"),
            new Page("공격",
                "Z  기본 공격\nX  특수 공격 (사슬을 회전시켜 넓게 벰, 마나 소모)"),
            new Page("스킬  Q · W · E · R",
                "Q  어둠의 주먹 - 전방으로 거대한 주먹을 날린다\nW  악마의 손 - 전방에 포탈을 열어 악마의 손으로 연타\nE  사슬 속박 - 적 발밑에 회오리를 일으켜 묶고 연타\nR  죽음의 소용돌이 (궁극기) - 맵 전체에 지속 피해"),
            new Page("스킬  A · S · D",
                "A  낫 회전 베기 - 최대 3충전, 충전이 남으면 연달아 재시전\nS  사슬 갈고리 - 적을 락온 (Z: 당겨오기 / X: 돌진 관통)\nD  낫 부메랑 - 낫을 던졌다 회수하며 왕복 경로에 피해"),
            new Page("마을에서",
                "I  인벤토리 열기/닫기\n노아에게 말을 걸면 상점 · 퀘스트 · 일일 의뢰 · 저장을 이용할 수 있어요\n이시스에게 말을 걸면 연습장에서 스킬을 연습할 수 있어요"),
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
