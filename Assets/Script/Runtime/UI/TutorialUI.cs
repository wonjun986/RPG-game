using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 새 게임 튜토리얼(ForestTutorial)에서 쓰는 화면 요소들을 모아 둔다.
    // 1) 안내창: 여러 장으로 나눠 보여주고, Z/Enter로 다음 장, X/Esc로 전부 건너뛴다(열려 있는 동안 시간 정지).
    // 2) 단계 안내: 화면 위쪽 가운데에 "지금 할 일"을 띄우고, 해내면 초록색으로 바뀌며 사라진다.
    // 3) 눈꺼풀: 위/아래 검은 막이 화면을 덮었다가 두어 번 깜빡이며 열린다(숲에서 눈을 뜨는 연출).
    public class TutorialUI : MonoBehaviour
    {
        public readonly struct Page
        {
            public readonly string Title;
            public readonly string Body;
            public Page(string title, string body) { Title = title; Body = body; }
        }

        // 숲에서 눈을 뜬 직후, 이동 연습을 시작하기 전에 보여준다.
        public static readonly Page[] ControlPages =
        {
            new Page("조작법",
                "← →  이동\n↑  점프 (공중에서 한 번 더 누르면 이단 점프)\nShift  대시 (몬스터를 관통하며 피해, 대시 중에는 무적)\nZ  대화 · 확인     X  취소"),
        };

        // 숲에서 정령이 나타났을 때 — 첫 전투 직전에 보여준다.
        public static readonly Page[] BattlePages =
        {
            new Page("공격",
                "Z  기본 공격\nX  특수 공격 (사슬을 회전시켜 넓게 벰)\n\n몬스터는 가까이 붙으면 직접 공격해 와요\n점프와 대시로 피하면서 싸우세요"),
            new Page("스킬과 마나",
                "스킬은 마나를 쓰고, 쓰고 나면 쿨타임이 돌아요\n왼쪽 위 MP 바와 스킬 칸에서 확인할 수 있어요\n마나는 시간이 지나면 조금씩 차올라요\nF  회복 포션 마시기 (체력 +40)\n\n적이 약해지면 스킬로 마무리해 보세요!"),
        };

        // 숲을 빠져나와 마을에 처음 들어왔을 때 보여준다.
        public static readonly Page[] VillagePages =
        {
            new Page("스킬  Q · W · E · R",
                "Q  어둠의 주먹 - 전방으로 거대한 주먹을 날린다\nW  악마의 손 - 전방에 포탈을 열어 악마의 손으로 연타\nE  사슬 폭풍 - 적 발밑에 회오리를 일으켜 묶고 연타\nR  죽음의 소용돌이 (궁극기) - 맵 전체에 지속 피해"),
            new Page("스킬  A · S · D",
                "A  낫 회전 베기 - 최대 3충전, 충전이 남으면 연달아 재시전\nS  사슬 갈고리 - 적을 락온 (Z: 당겨오기 / X: 돌진 관통)\nD  낫 부메랑 - 낫을 던졌다 회수하며 왕복 경로에 피해"),
            new Page("패배",
                "체력이 0이 되면 쓰러져 마을로 돌아가요\n레벨 · 아이템 · 퀘스트는 그대로 남아요"),
            new Page("마을에서",
                "I  인벤토리 열기/닫기\nC  상태창 열기/닫기\n\n노아  상점 · 퀘스트 · 일일 의뢰 · 저장\n엘리  장비 강화\n이시스  연습장 — 허수아비로 스킬 연습\n엔스룸의 릴리  몬스터 도감 · 리듬 대결 미니게임\n\n마을 오른쪽 끝 포탈에서 갈 곳을 고를 수 있어요"),
        };

        private static readonly Color GuideColor = new Color(0.95f, 0.85f, 0.2f);
        private static readonly Color GuideDoneColor = new Color(0.45f, 0.95f, 0.5f);
        private const float GuideDoneHold = 0.7f;

        private GameObject panel;
        private Text titleText;
        private Text bodyText;
        private Text pageText;
        private Text hintText;
        private CharacterMovement2D playerMovement;
        private Page[] pages = System.Array.Empty<Page>();
        private int pageIndex;
        private System.Action onClosed;

        private GameObject guideBox;
        private Text guideText;
        private bool guideHidden;
        private bool guideActive;

        private RectTransform topLid;
        private RectTransform bottomLid;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize(CharacterMovement2D movement)
        {
            playerMovement = movement;
            BuildEyelids();
            BuildGuide();
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

            // 안내창 틀(UI/TutorialPanel, Sprite/UI/Tutorial_UI 1983x793): 왼쪽에 네크로시아 초상화, 위쪽 작은 칸에 제목,
            // 오른쪽 큰 어두운 칸에 본문을 쓴다. 틀 그림이 없으면 예전처럼 어두운 판에 글자만 쓴다.
            var frameTexture = Resources.Load<Texture2D>(PanelFramePath);
            var boxGO = new GameObject("Box", typeof(RectTransform), typeof(RawImage));
            var boxRect = (RectTransform)boxGO.transform;
            boxRect.SetParent(rect, false);
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            var box = boxGO.GetComponent<RawImage>();
            box.raycastTarget = false;

            if (frameTexture != null)
            {
                boxRect.sizeDelta = new Vector2(PanelWidth, PanelWidth * PanelImageHeight / PanelImageWidth);
                box.texture = frameTexture;
                titleText = BuildText(boxRect, "Title", PanelAnchor(655f, 222f), PanelAnchor(900f, 152f), 26, GuideColor, FontStyle.Bold, TextAnchor.MiddleCenter, 8f);
                titleText.resizeTextForBestFit = true;
                titleText.resizeTextMinSize = 12;
                titleText.resizeTextMaxSize = 26;
                bodyText = BuildText(boxRect, "Body", PanelAnchor(650f, 520f), PanelAnchor(1800f, 240f), 22, Color.white, FontStyle.Normal, TextAnchor.MiddleLeft, 0f);
                bodyText.resizeTextForBestFit = true;
                bodyText.resizeTextMinSize = 12;
                bodyText.resizeTextMaxSize = 22;
                pageText = BuildText(boxRect, "PageIndex", PanelAnchor(650f, 585f), PanelAnchor(1000f, 530f), 16, new Color(0.7f, 0.65f, 0.8f), FontStyle.Normal, TextAnchor.MiddleLeft, 0f);
                hintText = BuildText(boxRect, "Hint", PanelAnchor(1100f, 585f), PanelAnchor(1800f, 530f), 18, new Color(0.85f, 0.8f, 0.9f), FontStyle.Bold, TextAnchor.MiddleRight, 0f);
            }
            else
            {
                boxRect.sizeDelta = new Vector2(760f, 420f);
                box.color = new Color(0.06f, 0.04f, 0.09f, 0.97f);
                titleText = BuildText(boxRect, "Title", new Vector2(0f, 0.82f), new Vector2(1f, 1f), 30, GuideColor, FontStyle.Bold, TextAnchor.MiddleCenter);
                bodyText = BuildText(boxRect, "Body", new Vector2(0f, 0.22f), new Vector2(1f, 0.8f), 24, Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
                pageText = BuildText(boxRect, "PageIndex", new Vector2(0f, 0.1f), new Vector2(1f, 0.2f), 18, new Color(0.7f, 0.65f, 0.8f), FontStyle.Normal, TextAnchor.MiddleCenter);
                hintText = BuildText(boxRect, "Hint", new Vector2(0f, 0f), new Vector2(1f, 0.12f), 20, new Color(0.8f, 0.8f, 0.8f), FontStyle.Bold, TextAnchor.MiddleCenter);
            }

            panel.SetActive(false);
        }

        private void BuildGuide()
        {
            guideBox = new GameObject("TutorialGuide", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)guideBox.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            var image = guideBox.GetComponent<RawImage>();
            image.raycastTarget = false;

            // 안내 틀(UI/TutorialGuide, Sprite/UI/Tutorial Controls 를 1672x630 으로 자른 것): 위쪽 장식판에 TUTORIAL,
            // 가운데 어두운 칸에 지금 할 일을 쓴다. 틀 그림이 없으면 예전처럼 어두운 띠에 글자만 쓴다.
            var frameTexture = Resources.Load<Texture2D>(GuideFramePath);
            if (frameTexture != null)
            {
                image.texture = frameTexture;
                rect.anchoredPosition = new Vector2(0f, -4f);
                rect.sizeDelta = new Vector2(GuideWidth, GuideWidth * GuideImageHeight / GuideImageWidth);
                guideText = BuildText(rect, "Text", GuideAnchor(230f, 470f), GuideAnchor(1450f, 295f), 24, GuideColor, FontStyle.Bold, TextAnchor.MiddleCenter, 0f);
                guideText.resizeTextForBestFit = true;
                guideText.resizeTextMinSize = 12;
                guideText.resizeTextMaxSize = 24;
            }
            else
            {
                image.color = new Color(0.06f, 0.04f, 0.09f, 0.85f);
                rect.anchoredPosition = new Vector2(0f, -96f);
                rect.sizeDelta = new Vector2(620f, 64f);
                guideText = BuildText(rect, "Text", Vector2.zero, Vector2.one, 24, GuideColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            }
            guideBox.SetActive(false);
        }

        private void BuildEyelids()
        {
            topLid = BuildLid("EyelidTop", new Vector2(0.5f, 1f));
            bottomLid = BuildLid("EyelidBottom", new Vector2(0.5f, 0f));
            SetEyesClosed(false);
        }

        private RectTransform BuildLid(string name, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0f, pivot.y);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = pivot;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            return rect;
        }

        private const string PanelFramePath = "UI/TutorialPanel";
        private const float PanelImageWidth = 1983f, PanelImageHeight = 793f;
        private const float PanelWidth = 1120f; // 캔버스(1280x720) 기준 안내창 폭

        // 안내창 틀 그림 픽셀 좌표(왼쪽 위 원점) → 앵커.
        private static Vector2 PanelAnchor(float x, float y) => new Vector2(x / PanelImageWidth, 1f - y / PanelImageHeight);

        private const string GuideFramePath = "UI/TutorialGuide";
        private const float GuideImageWidth = 1672f, GuideImageHeight = 630f;
        private const float GuideWidth = 700f; // 캔버스(1280x720) 기준 단계 안내 폭

        // 단계 안내 틀 그림 픽셀 좌표(왼쪽 위 원점) → 앵커.
        private static Vector2 GuideAnchor(float x, float y) => new Vector2(x / GuideImageWidth, 1f - y / GuideImageHeight);

        private static Text BuildText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize, Color color, FontStyle style, TextAnchor anchor, float sidePadding = 32f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(sidePadding, 0f);
            rect.offsetMax = new Vector2(-sidePadding, 0f);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        // ---- 안내창 ----

        public void Show(Page[] pagesToShow, System.Action closed = null)
        {
            if (pagesToShow == null || pagesToShow.Length == 0)
            {
                closed?.Invoke();
                return;
            }
            pages = pagesToShow;
            onClosed = closed;
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
            var closed = onClosed;
            onClosed = null;
            closed?.Invoke();
        }

        private void RefreshPage()
        {
            var page = pages[pageIndex];
            titleText.text = page.Title;
            bodyText.text = page.Body;
            pageText.text = $"{pageIndex + 1} / {pages.Length}";
            hintText.text = pageIndex < pages.Length - 1 ? "[Z] 다음   [X] 건너뛰기" : "[Z] 확인";
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
                if (pageIndex < pages.Length - 1)
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

        // ---- 단계 안내 ----

        public void ShowGuide(string text)
        {
            guideText.text = text;
            guideText.color = GuideColor;
            guideBox.SetActive(!guideHidden);
            guideActive = true;
        }

        // 해낸 단계를 초록색 체크로 잠깐 보여준 뒤 안내를 내린다.
        public IEnumerator CompleteGuide()
        {
            guideText.text = guideText.text + "  완료!";
            guideText.color = GuideDoneColor;
            yield return new WaitForSecondsRealtime(GuideDoneHold);
            HideGuide();
        }

        public void HideGuide()
        {
            guideActive = false;
            if (guideBox != null) guideBox.SetActive(false);
        }

        // 리듬 전투처럼 화면을 덮는 창이 떠 있는 동안 안내를 잠깐 숨긴다(내용은 그대로 남는다).
        public void SetGuideHidden(bool hidden)
        {
            guideHidden = hidden;
            if (guideBox != null) guideBox.SetActive(guideActive && !hidden);
        }

        // ---- 눈꺼풀 ----

        public void SetEyesClosed(bool closed) => SetLidCover(closed ? 1f : 0f);

        // cover 1 = 완전히 감음, 0 = 완전히 뜸. 위/아래 막이 각각 화면 높이의 절반까지 덮는다.
        private void SetLidCover(float cover)
        {
            if (topLid == null) return;
            // 가운데에서 두 막이 살짝 겹치게 해서 완전히 감았을 때 틈이 보이지 않게 한다.
            float reach = Mathf.Min(0.51f, cover * 0.51f);
            topLid.anchorMin = new Vector2(0f, 1f - reach);
            bottomLid.anchorMax = new Vector2(1f, reach);
            bool visible = cover > 0.001f;
            topLid.gameObject.SetActive(visible);
            bottomLid.gameObject.SetActive(visible);
        }

        // 감긴 눈이 반쯤 떴다가 다시 감기고, 두 번째에 완전히 열린다.
        public IEnumerator OpenEyes()
        {
            SetLidCover(1f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return AnimateLids(1f, 0.6f, 0.9f);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return AnimateLids(0.6f, 1f, 0.25f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return AnimateLids(1f, 0.3f, 0.6f);
            yield return AnimateLids(0.3f, 0.45f, 0.15f);
            yield return AnimateLids(0.45f, 0f, 0.7f);
            SetLidCover(0f);
        }

        private IEnumerator AnimateLids(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetLidCover(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)));
                yield return null;
            }
            SetLidCover(to);
        }
    }
}
