namespace ShortGeta.Core
{
    // 옵션 인터페이스. 미니게임이 서버 DDA 강도(±1) 를 받아 자체 노브를 조정하고
    // 싶을 때 IMinigame 과 함께 구현한다. IMinigame 자체는 변경 금지.
    //
    // 호출 시점:
    //   BootstrapController 가 _registry.Create(...) 직후, OnGameStart() 이전에
    //   `if (game is IDifficultyAware d) d.SetDifficulty(intensity)` 호출.
    //
    // intensity 범위:
    //   -1 = 쉬움  / 0 = 기본 / +1 = 어려움
    //   서버가 ±1 만 보내지만, 방어적으로 클램프 권장.
    //
    // 게임은 SetDifficulty 결과를 OnGameStart 직전에 사용해 spawn rate 등 단일
    // 노브 1개를 조정한다. UI 안내는 띄우지 않는다 (PROJECT_PLAN.md §"DDA: 유저에
    // 노출하지 않는다").
    public interface IDifficultyAware
    {
        void SetDifficulty(int intensity);
    }
}
