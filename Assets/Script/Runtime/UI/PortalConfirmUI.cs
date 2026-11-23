using UnityEngine;
using Aethoria.Characters;
using Aethoria.Stages;

namespace Aethoria.UI
{
    // 던전 첫 맵의 왼쪽 끝(마을로 돌아가는) 포탈을 밟으면 바로 마을로 보내지 않고 "마을로 이동하시겠습니까?"를 묻는다.
    // 창은 공통 선택 팝업(ChoicePopup)을 쓴다. 떠 있는 동안 게임을 멈추고(몬스터가 다가오지 않게) 플레이어 입력을 잠근다.
    // 예 = onYes 실행, 아니오 = 그 자리에 남고 포탈은 한 번 벗어났다 다시 밟아야 발동한다(Portal.ResetTrigger).
    public class PortalConfirmUI : MonoBehaviour
    {
        private CharacterMovement2D playerMovement;

        public bool IsOpen => ChoicePopup.IsOpen;

        public void Initialize(CharacterMovement2D movement)
        {
            playerMovement = movement;
        }

        public void Show(Portal portal, string message, System.Action yes)
        {
            ChoicePopup.Ask(message, yes, () => { if (portal != null) portal.ResetTrigger(); }, lockMovement: playerMovement);
        }
    }
}
