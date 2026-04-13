using System.Collections.Generic;
using UnityEngine;

namespace ShortGeta.Core.UI
{
    // Resources/Sprites/{GameFolder}/ 에서 게임별 스프라이트를 로드하는 헬퍼.
    // 없는 파일은 null 반환 (fallback 으로 이모지/단색 사용).
    //
    // 사용:
    //   var frog = GameSpriteLoader.Load("FrogCatch", "frog_idle");
    //   var bg   = GameSpriteLoader.LoadBg("frog_catch_v1");
    //   var thumb = GameSpriteLoader.LoadThumbnail("frog_catch_v1");
    public static class GameSpriteLoader
    {
        // gameId → Resources 폴더명 매핑 (13종 전체)
        private static readonly Dictionary<string, string> FolderMap = new()
        {
            { "frog_catch_v1",       "FrogCatch" },
            { "noodle_boil_v1",      "NoodleBoil" },
            { "poker_face_v1",       "PokerFace" },
            { "dark_souls_v1",       "DarkSouls" },
            { "kakao_unread_v1",     "KakaoUnread" },
            { "math_genius_v1",      "MathGenius" },
            { "classroom_click_v1",  "ClassroomClick" },
            { "track_run_v1",        "TrackRun" },
            { "pole_climb_v1",       "PoleClimb" },
            { "fly_catch_v1",        "FlyCatch" },
            { "soccer_topdown_v1",   "SoccerTopdown" },
            { "soccer_side_v1",      "SoccerSide" },
            { "dark_explore_v1",     "DarkExplore" },
        };

        // gameId → 썸네일 파일명 매핑 (홈 카드용)
        private static readonly Dictionary<string, string> ThumbnailMap = new()
        {
            { "frog_catch_v1",       "frog_idle" },
            { "noodle_boil_v1",      "ramen_pot" },
            { "poker_face_v1",       "poker_face_thumb" },
            { "dark_souls_v1",       "boss_silhouette" },
            { "kakao_unread_v1",     "kakao_thumb" },
            { "math_genius_v1",      "math_thumb" },
            { "classroom_click_v1",  "classroom_thumb" },
            { "track_run_v1",        "runner_thumb" },
            { "pole_climb_v1",       "pole_thumb" },
            { "fly_catch_v1",        "fly_thumb" },
            { "soccer_topdown_v1",   "soccer_top_thumb" },
            { "soccer_side_v1",      "soccer_side_thumb" },
            { "dark_explore_v1",     "explorer_thumb" },
        };

        // gameId → 배경 파일명 매핑 (인게임용)
        private static readonly Dictionary<string, string> BgMap = new()
        {
            { "frog_catch_v1",       "bg_pond" },
            { "noodle_boil_v1",      "bg_kitchen" },
            { "poker_face_v1",       "bg_poker" },
            { "dark_souls_v1",       "bg_dungeon" },
            { "kakao_unread_v1",     "bg_kakao" },
            { "math_genius_v1",      "bg_chalkboard" },
            { "classroom_click_v1",  "bg_classroom" },
            { "track_run_v1",        "bg_track" },
            { "pole_climb_v1",       "bg_pole" },
            { "fly_catch_v1",        "bg_wall" },
            { "soccer_topdown_v1",   "bg_field_top" },
            { "soccer_side_v1",      "bg_field_side" },
            { "dark_explore_v1",     "bg_cave" },
        };

        // 캐시
        private static readonly Dictionary<string, Sprite> _cache = new();

        /// <summary>특정 게임 폴더 내 스프라이트 로드.</summary>
        public static Sprite Load(string folder, string fileName)
        {
            string key = $"{folder}/{fileName}";
            if (_cache.TryGetValue(key, out var cached)) return cached;
            var sprite = Resources.Load<Sprite>($"Sprites/{folder}/{fileName}");
            _cache[key] = sprite; // null 도 캐시 (재시도 방지)
            return sprite;
        }

        /// <summary>홈 카드 썸네일용 스프라이트.</summary>
        public static Sprite LoadThumbnail(string gameId)
        {
            if (!FolderMap.TryGetValue(gameId, out var folder)) return null;
            if (!ThumbnailMap.TryGetValue(gameId, out var file)) return null;
            return Load(folder, file);
        }

        /// <summary>인게임 배경 스프라이트.</summary>
        public static Sprite LoadBg(string gameId)
        {
            if (!FolderMap.TryGetValue(gameId, out var folder)) return null;
            if (!BgMap.TryGetValue(gameId, out var file)) return null;
            return Load(folder, file);
        }

        /// <summary>게임 폴더 내 임의 파일.</summary>
        public static Sprite LoadByGameId(string gameId, string fileName)
        {
            if (!FolderMap.TryGetValue(gameId, out var folder)) return null;
            return Load(folder, fileName);
        }
    }
}
