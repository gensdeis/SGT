namespace ShortGeta.Core
{
    // 태그 카테고리 — Docs/PROJECT_PLAN.md §"태그 체계" 참조.
    // 미니게임의 Tags 배열은 이 enum 의 ToString() 형태가 아니라
    // 한국어 문자열 그대로 사용한다 (서버 games.yaml 과 일치).
    public static class GameTags
    {
        // 장르
        public const string Reflex = "반응속도";
        public const string Timing = "타이밍";
        public const string Memory = "기억력";
        public const string Awareness = "눈치";
        public const string Luck = "운빨";
        public const string Focus = "집중력";
        public const string Multitask = "멀티태스킹";

        // 강도 (DDA가 동적 조절)
        public const string Hardcore = "하드코어";
        public const string Medium = "중간";
        public const string Calm = "잔잔";
        public const string Intense = "자극적";

        // 소재
        public const string Animal = "동물";
        public const string Food = "음식";
        public const string Office = "직장";
        public const string Romance = "연애";
        public const string Horror = "공포";
        public const string Daily = "일상";
        public const string Fantasy = "판타지";

        // 밈도
        public const string Bmovie = "병맛";          // B급
        public const string Comedy = "순수개그";
        public const string Dark = "블랙유머";
        public const string Retro = "레트로";
        public const string Internet = "인터넷밈";
    }
}
