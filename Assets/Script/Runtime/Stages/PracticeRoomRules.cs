using UnityEngine;
using Aethoria.Characters;

namespace Aethoria.Stages
{
    // 연습장 맵(stageRoot)에 붙어서, 머무는 동안 플레이어의 체력/마나를 계속 가득 채운다 — 허수아비와의
    // 연습 리듬 전투에서 노드를 놓쳐도 쓰러지지 않는다(전투 중 timeScale=0이어도 LateUpdate는 돈다).
    // 맵을 떠나면 stageRoot와 함께 사라진다.
    public class PracticeRoomRules : MonoBehaviour
    {
        private Character player;

        public void Initialize(Character playerCharacter)
        {
            player = playerCharacter;
        }

        private void LateUpdate()
        {
            if (player == null || player.IsDead) return;

            player.Heal(player.MaxHp);
            player.RestoreMana(player.MaxMana);
        }
    }
}
