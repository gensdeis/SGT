namespace ShortGeta.Core
{
    // games.yaml 의 time_limit_sec 값을 게임에 주입하는 옵션 인터페이스.
    // IMinigame 은 변경 금지이므로, 이 인터페이스를 별도 구현해 TimeLimit 를
    // 서버 데이터로 덮어쓸 수 있다.
    // IDifficultyAware 와 동일한 패턴 — PlaySingleAsync 에서 OnGameStart 이전에 호출.
    public interface ITimeLimitAware
    {
        void SetTimeLimit(float sec);
    }
}
