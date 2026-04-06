// 숏게타 코어 인터페이스 — Docs/CLAUDE.md §"핵심 인터페이스 — IMinigame.cs" 의 정의 그대로.
// !!! 절대 변경 금지 !!!
// 모든 미니게임(오리지널 + UGC)이 이 인터페이스를 구현한다.
namespace ShortGeta.Core
{
    public interface IMinigame
    {
        // "frog_catch_v1" — 소문자_스네이크_버전
        string GameId { get; }

        // "개구리 잡아라"
        string Title { get; }

        // "shotgeta_official" or UGC 크리에이터 ID
        string CreatorId { get; }

        // 제한시간 (초)
        float TimeLimit { get; }

        // ["반응속도", "동물", "병맛"]
        string[] Tags { get; }

        void OnGameStart();
        void OnGameEnd();
        int GetScore();

        // 터치/클릭/키보드 추상화
        void OnInput(InputEvent input);
    }
}
