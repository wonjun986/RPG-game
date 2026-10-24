using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Stages;

namespace Aethoria.UI
{
    // 던전 첫 맵의 왼쪽 끝(마을로 돌아가는) 포탈을 밟으면 바로 마을로 보내지 않고 "마을로 이동하시겠습니까?"를 묻는다.
    // 생김새는 월드맵의 마을 이동 확인 창과 같다. 떠 있는 동안 게임을 멈추고(몬스터가 다가오지 않게) 플레이어 입력을 잠근다.
    // 예(Z/Enter) = onYes 실행, 아니오(X/Esc) = 그 자리에 남고 포탈은 한 번 벗어났다 다시 밟아야 발동한다(Portal.ResetTrigger).
    public class PortalConfirmUI : MonoBehaviour
    {
        private GameObject panel;
        private Text messageText;
        private CharacterMovement2D playerMovement;
        private Portal sourcePortal;
        private System.Action onYes;
        private int openedFrame = -1;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize(CharacterMovement2D movement)
        {
            playerMovement = movement;
            BuildPanel();
        }

        private void BuildPanel()
        {
            TitleScreenUI.EnsureEventSystem();

            panel = new GameObject("PortalConfirmPanel", typeof(RectTransform), typeof(Image));
            var dimRect = (RectTransform)panel.transform;
            dimRect.SetParent(transform, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImage = panel.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.55f);
            dimImage.raycastTarget = true;

            var boxGO = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var boxRect = (RectTransform)boxGO.transform;
            boxRect.SetParent(panel.transform, false);
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(560f, 230f);
            boxGO.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.09f, 0.97f);

            var messageGO = new GameObject("Message", typeof(RectTransform), typeof(Text));
            var messageRect = (RectTransform)messageGO.transform;
            messageRect.SetParent(boxRect, false);
            messageRect.anchorMin = new Vector2(0f, 0.5f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.offsetMin = Vector2.zero;
            messageRect.offsetMax = Vector2.zero;
            messageText = messageGO.GetComponent<Text>();
            messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            messageText.fontSize = 28;
            messageText.fontStyle = FontStyle.Bold;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.color = Color.white;

            WorldMapUI.BuildDialogButton(boxRect, "YesButton", "예 (Z)", new Vector2(0.08f, 0.12f), new Vector2(0.47f, 0.42f), Confirm);
            WorldMapUI.BuildDialogButton(boxRect, "NoButton", "아니오 (X)", new Vector2(0.53f, 0.12f), new Vector2(0.92f, 0.42f), Cancel);

            panel.SetActive(false);
        }

        public void Show(Portal portal, string message, System.Action yes)
        {
            sourcePortal = portal;
            onYes = yes;
            messageText.text = message;
            openedFrame = Time.frameCount;
            panel.SetActive(true);
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        private void Update()
        {
            if (!IsOpen) return;
            // 포탈을 밟은 그 프레임에 마침 Z/X를 누르고 있었으면 창이 뜨자마자 닫혀 버리니 한 프레임 거른다.
            if (Time.frameCount == openedFrame) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                Confirm();
            }
            else if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                Cancel();
            }
        }

        private void Close()
        {
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private void Confirm()
        {
            if (!IsOpen) return;
            var action = onYes;
            onYes = null;
            sourcePortal = null;
            Close();
            action?.Invoke();
        }

        private void Cancel()
        {
            if (!IsOpen) return;
            var portal = sourcePortal;
            onYes = null;
            sourcePortal = null;
            Close();
            if (portal != null) portal.ResetTrigger();
        }
    }
}
