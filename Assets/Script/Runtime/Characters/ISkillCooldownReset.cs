namespace Aethoria.Characters
{
    // 연습장처럼 스킬을 쿨다운 없이 계속 써 볼 수 있어야 하는 곳에서, 남은 쿨다운(충전형은 충전 수)을 즉시 되돌린다.
    public interface ISkillCooldownReset
    {
        void ResetCooldown();
    }
}
