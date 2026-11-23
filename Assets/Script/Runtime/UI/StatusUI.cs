using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // C키로 여닫는 네크로시아의 상태창(UI/NecrosiaStatus). 인벤토리와 같은 방식으로 여는 동안
    // 시간이 멈추고 캐릭터 조작이 잠긴다. 그림 오른쪽 칸 다섯 개(공격력/방어력/치명타 확률/
    // 치명타 피해량/스킬 피해량)에 지금 능력치(레벨 + 장비)를 채워 보여준다. C/Esc/X로 닫는다.
    public class StatusUI : MonoBehaviour
    {
        // UI/NecrosiaStatus(네크로시아 판타지 캐릭터 스탯 UI, 1536x1024, 바깥 배경은 투명) 그림 속 칸 좌표를 픽셀로 잰 값.
        // 오른쪽 아래 다섯 줄(줄 사이 구분선 y 391/468/541/615/689/767)의 육각 값 칸, 위쪽 "Lv." 옆 밑줄(y 302, x 999~1280).
        private const float ImageWidth = 1536f;
        private const float ImageHeight = 1024f;
        private const float ValueBoxLeft = 1165f;
        private const float ValueBoxRight = 1365f;
        private const float ValueBoxHalfHeight = 22f;
        private static readonly float[] ValueRowCenterY = { 430f, 504f, 578f, 652f, 728f };
        private static readonly Rect LevelBox = new Rect(1018f, 254f, 257f, 46f); // x, y(위), 너비, 높이

        // 기준 해상도(1280x720) 높이에 거의 꽉 차게 그린다. 너비는 그림 비율대로.
        private const float WindowHeight = 700f;

        private Character player;
        private CharacterMovement2D playerMovement;
        private GameObject panel;
        private readonly Text[] valueTexts = new Text[ValueRowCenterY.Length];
        private Text levelText;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize(Character character, CharacterMovement2D movement)
        {
            player = character;
            playerMovement = movement;
            BuildPanel();
        }

        private void BuildPanel()
        {
            // 화면 전체를 어둡게 덮어 뒤의 맵/HUD를 가린다.
            panel = new GameObject("StatusPanel", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

            var windowGO = new GameObject("Window", typeof(RectTransform), typeof(RawImage));
            var windowRect = (RectTransform)windowGO.transform;
            windowRect.SetParent(rect, false);
            windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(WindowHeight * ImageWidth / ImageHeight, WindowHeight);
            var windowImage = windowGO.GetComponent<RawImage>();
            windowImage.texture = Resources.Load<Texture2D>("UI/Menus/NecrosiaStatus");
            windowImage.raycastTarget = false;

            for (int i = 0; i < ValueRowCenterY.Length; i++)
            {
                float top = ValueRowCenterY[i] - ValueBoxHalfHeight;
                valueTexts[i] = BuildText(windowRect, "Value_" + i,
                    new Rect(ValueBoxLeft, top, ValueBoxRight - ValueBoxLeft, ValueBoxHalfHeight * 2f), 22, TextAnchor.MiddleCenter);
            }
            // "Lv." 옆 밑줄 위에 지금 레벨을 쓴다.
            levelText = BuildText(windowRect, "Level", LevelBox, 26, TextAnchor.MiddleLeft);

            panel.AddComponent<PanelFadeIn>(); // 켜질 때 공통 연출 + 여는 소리
            panel.SetActive(false);
        }

        // box: 그림 픽셀 좌표(x, 위쪽 y, 너비, 높이).
        private static Text BuildText(Transform parent, string name, Rect box, int fontSize, TextAnchor alignment)
        {
            var textGO = new GameObject(name, typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(parent, false);
            textRect.anchorMin = new Vector2(box.xMin / ImageWidth, 1f - box.yMax / ImageHeight);
            textRect.anchorMax = new Vector2(box.xMax / ImageWidth, 1f - box.yMin / ImageHeight);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = new Color(0.93f, 0.88f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private void Refresh()
        {
            if (player == null) return;

            levelText.text = player.Level.ToString();
            var stats = player.Stats;
            valueTexts[0].text = stats.attack.ToString("0.#");
            valueTexts[1].text = stats.defense.ToString("0.#");
            valueTexts[2].text = player.CritChancePercent.ToString("0.#") + "%";
            valueTexts[3].text = player.CritDamagePercent.ToString("0.#") + "%";
            valueTexts[4].text = "+" + player.SkillDamagePercent.ToString("0.#") + "%";
        }

        public void Show()
        {
            Refresh();
            panel.SetActive(true);
            panel.transform.SetAsLastSibling(); // 나중에 추가된 HUD 요소보다 위에 그린다
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        public void Hide()
        {
            if (panel.activeSelf) UiSfx.Close();
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (IsOpen)
            {
                if (keyboard.cKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame)
                {
                    Hide();
                }
                return;
            }

            // 대화창/인벤토리/전투 등 다른 곳에서 이미 조작을 잠가 둔 상태면 열지 않는다.
            if (player == null || player.IsDead) return;
            if (playerMovement != null && playerMovement.IsInputLocked) return;

            if (keyboard.cKey.wasPressedThisFrame)
            {
                Show();
            }
        }
    }
}
