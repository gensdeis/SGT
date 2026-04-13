namespace ShortGeta.Core
{
    // IMinigame 의 선택적 확장 인터페이스.
    // 제한 시간 만료 전에 게임 자체 조건으로 종료될 수 있는 게임이 구현한다.
    // MinigameLauncher 는 매 Update 마다 이 인터페이스 여부를 확인해 조기 종료를 처리한다.
    // (IMinigame 은 변경 금지이므로 별도 인터페이스로 분리)
    public interface IEarlyCompletable
    {
        // true 를 반환하면 MinigameLauncher 가 즉시 FinishInternal() 호출
        bool IsComplete { get; }
    }
}
