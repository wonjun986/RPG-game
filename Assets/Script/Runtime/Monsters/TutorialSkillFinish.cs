using UnityEngine;

namespace Aethoria.Monsters
{
    // 튜토리얼 몬스터에 붙인다. 기본 공격으로는 체력 1에서 버티고(Monster.SkillFinishOnly), 스킬 피해로만 쓰러진다.
    // 체력이 절반 이하로 떨어지면 튜토리얼(ForestTutorial)이 스킬로 마무리하라고 알려 준다.
    [RequireComponent(typeof(Monster))]
    public class TutorialSkillFinish : MonoBehaviour
    {
        public const float HintHealthFraction = 0.5f;

        private void Awake()
        {
            GetComponent<Monster>().SkillFinishOnly = true;
        }
    }
}
