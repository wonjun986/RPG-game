using UnityEngine;

namespace Aethoria.Monsters
{
    // 튜토리얼 몬스터에 붙인다. 일반 공격으로는 체력 1에서 버티고(Monster.SurviveLethalHit), 체력이 절반 이하로 떨어지면
    // 리듬 전투가 내 턴마다 필살 게이지를 가득 채워 주며 스킬로 마무리하라고 알려 준다. 스킬 피해로만 쓰러진다(RhythmBattleUI).
    [RequireComponent(typeof(Monster))]
    public class TutorialSkillFinish : MonoBehaviour
    {
        public const float HintHealthFraction = 0.5f;

        private void Awake()
        {
            GetComponent<Monster>().SurviveLethalHit = true;
        }
    }
}
