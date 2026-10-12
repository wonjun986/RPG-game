using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Bootstrap;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 필드에서 Esc를 누르면 "타이틀 화면으로 돌아가시겠습니까?" 확인 창을 띄운다.
    // 예(Z/Enter)를 고르면 지금 진행을 자동 저장한 뒤 타이틀로 돌아가고, 아니오(X/Esc)면 그대로 닫는다.
    // 인벤토리/상점/대화/리듬 전투 등 다른 창이 열려 있으면(조작이 잠겨 있으면) 열지 않는다 — 그 창들은 Esc로
    // 닫히는데, 닫으려고 누른 Esc가 같은 프레임에 이 창까지 열지 않도록 직전 프레임에도 잠겨 있지 않았을 때만 연다.
    public class TitleReturnUI : MonoBehaviour
    {
        private Character player;
        private CharacterMovement2D playerMovement;
        private GameObject panel;
        private bool wasBlockedLastFrame = true;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize(Character character, CharacterMovement2D movement)
        {
            player = character;
            playerMovement = movement;
            BuildPanel();
        }

        private void BuildPanel()
        {
            panel = new GameObject("TitleReturnPanel", typeof(RectTransform), typeof(Image));
            var dimRect = (RectTransform)panel.transform;
            dimRect.SetParent(transform, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImage = panel.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.65f);
            dimImage.raycastTarget = true; // 뒤쪽 HUD 클릭을 막는다

            var boxGO = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var boxRect = (RectTransform)boxGO.transform;
            boxRect.SetParent(dimRect, false);
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(600f, 250f);
            boxGO.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.09f, 0.97f);

            var messageGO = new GameObject("Message", typeof(RectTransform), typeof(Text));
            var messageRect = (RectTransform)messageGO.transform;
            messageRect.SetParent(boxRect, false);
            messageRect.anchorMin = new Vector2(0f, 0.45f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.offsetMin = Vector2.zero;
            messageRect.offsetMax = Vector2.zero;
            var messageText = messageGO.GetComponent<Text>();
            messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            messageText.fontSize = 26;
            messageText.fontStyle = FontStyle.Bold;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.color = Color.white;
            messageText.text = "타이틀 화면으로 돌아가시겠습니까?\n<size=20>(지금까지의 진행은 자동 저장됩니다)</size>";

            WorldMapUI.BuildDialogButton(boxRect, "YesButton", "예 (Z)", new Vector2(0.08f, 0.1f), new Vector2(0.47f, 0.38f), ConfirmYes);
            WorldMapUI.BuildDialogButton(boxRect, "NoButton", "아니오 (X)", new Vector2(0.53f, 0.1f), new Vector2(0.92f, 0.38f), Hide);

            panel.SetActive(false);
        }

        private void Show()
        {
            TitleScreenUI.EnsureEventSystem();
            panel.transform.SetAsLastSibling(); // 다른 HUD 위에 그린다
            panel.SetActive(true);
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        private void Hide()
        {
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private void ConfirmYes()
        {
            panel.SetActive(false);
            GameBootstrap.SaveGame();
            GameBootstrap.ReturnToTitle(); // HUD(이 컴포넌트 포함)를 통째로 지우고 timeScale도 되돌린다
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (IsOpen)
            {
                if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    ConfirmYes();
                }
                else if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                {
                    Hide();
                    wasBlockedLastFrame = true; // 닫은 그 Esc로 바로 다시 열리지 않게
                }
                return;
            }

            bool blocked = player == null || player.IsDead
                || (playerMovement != null && playerMovement.IsInputLocked)
                || Time.timeScale == 0f;

            if (!blocked && !wasBlockedLastFrame && keyboard.escapeKey.wasPressedThisFrame)
            {
                Show();
                return;
            }
            wasBlockedLastFrame = blocked;
        }
    }
}
