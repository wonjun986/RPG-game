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
        // UI/NecrosiaStatus(1448x1086) 그림 속 값 칸 좌표를 픽셀로 잰 값.
        private const float ImageWidth = 1448f;
        private const float ImageHeight = 1086f;
        private const float ValueBoxLeft = 1145f;
        private const float ValueBoxRight = 1350f;
        private const float ValueBoxHalfHeight = 24f;
        private static readonly float[] ValueRowCenterY = { 356f, 485f, 614f, 743f, 872f };

        // 기준 해상도(1280x720) 높이에 거의 꽉 차게 그린다. 너비는 그림 비율대로.
        private const float WindowHeight = 700f;

        private Character player;
        private CharacterMovement2D playerMovement;
        private GameObject panel;
        private readonly Text[] valueTexts = new Text[ValueRowCenterY.Length];

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
            windowImage.texture = Resources.Load<Texture2D>("UI/NecrosiaStatus");
            windowImage.raycastTarget = false;

            for (int i = 0; i < ValueRowCenterY.Length; i++)
            {
                valueTexts[i] = BuildValueText(windowRect, i);
            }

            panel.SetActive(false);
        }

        private static Text BuildValueText(Transform parent, int row)
        {
            float top = ValueRowCenterY[row] - ValueBoxHalfHeight;
            float bottom = ValueRowCenterY[row] + ValueBoxHalfHeight;

            var textGO = new GameObject("Value_" + row, typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(parent, false);
            textRect.anchorMin = new Vector2(ValueBoxLeft / ImageWidth, 1f - bottom / ImageHeight);
            textRect.anchorMax = new Vector2(ValueBoxRight / ImageWidth, 1f - top / ImageHeight);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.93f, 0.88f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private void Refresh()
        {
            if (player == null) return;

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
