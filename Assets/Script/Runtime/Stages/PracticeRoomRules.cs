using UnityEngine;
using Aethoria.Characters;

namespace Aethoria.Stages
{
    // 연습장 맵(stageRoot)에 붙어서, 머무는 동안 플레이어의 체력/마나를 계속 가득 채우고
    // 모든 스킬의 쿨다운(A는 충전 수)을 매 프레임 되돌린다. 맵을 떠나면 stageRoot와 함께 사라진다.
    public class PracticeRoomRules : MonoBehaviour
    {
        private Character player;
        private ISkillCooldownReset[] skills;

        public void Initialize(Character playerCharacter)
        {
            player = playerCharacter;
            skills = player.GetComponents<ISkillCooldownReset>();
        }

        private void LateUpdate()
        {
            if (player == null || player.IsDead) return;

            player.Heal(player.MaxHp);
            player.RestoreMana(player.MaxMana);
            foreach (var skill in skills) skill.ResetCooldown();
        }
    }
}
