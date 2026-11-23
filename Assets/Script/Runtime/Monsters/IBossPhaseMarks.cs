namespace Aethoria.Monsters
{
    // 보스 체력바(BossHealthUI)에 눈금으로 그릴 "패턴이 바뀌는 체력 비율"(0~1). 보스 AI가 구현한다.
    public interface IBossPhaseMarks
    {
        float[] PhaseMarks { get; }
    }
}
