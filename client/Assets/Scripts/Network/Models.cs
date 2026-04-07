using System;
using Newtonsoft.Json;

namespace ShortGeta.Network
{
    // 서버 DTO 매핑. 필드명은 server/internal/storage/models.go 와 일치.

    [Serializable]
    public class DeviceLoginRequest
    {
        [JsonProperty("device_id")] public string DeviceId;
    }

    [Serializable]
    public class DeviceLoginResponse
    {
        [JsonProperty("user_id")] public string UserId;
        [JsonProperty("token")] public string Token;
        [JsonProperty("ad_removed")] public bool AdRemoved;
    }

    [Serializable]
    public class GameView
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("title")] public string Title;
        [JsonProperty("creator_id")] public string CreatorId;
        [JsonProperty("time_limit_sec")] public int TimeLimitSec;
        [JsonProperty("tags")] public string[] Tags;
        [JsonProperty("bundle_url")] public string BundleUrl;
        [JsonProperty("bundle_version")] public string BundleVersion;
        [JsonProperty("bundle_hash")] public string BundleHash;
    }

    [Serializable]
    public class ListGamesResponse
    {
        [JsonProperty("games")] public GameView[] Games;
    }

    [Serializable]
    public class StartSessionResponse
    {
        [JsonProperty("session_id")] public string SessionId;
        [JsonProperty("game_ids")] public string[] GameIds;
        [JsonProperty("dda_intensity")] public int DdaIntensity;
    }

    [Serializable]
    public class EndSessionRequest
    {
        [JsonProperty("scores")] public ScoreSubmission[] Scores;
    }

    [Serializable]
    public class ScoreSubmission
    {
        [JsonProperty("game_id")] public string GameId;
        [JsonProperty("score")] public int Score;
        [JsonProperty("play_time")] public double PlayTime;
        [JsonProperty("cleared")] public bool Cleared;
        [JsonProperty("timestamp")] public long Timestamp;
        [JsonProperty("signature")] public string Signature;
    }

    [Serializable]
    public class EndSessionResponse
    {
        [JsonProperty("accepted")] public string[] Accepted;
        [JsonProperty("rejected")] public string[] Rejected;
    }

    [Serializable]
    public class RankingEntry
    {
        [JsonProperty("user_id")] public string UserId;
        [JsonProperty("best_score")] public int BestScore;
        [JsonProperty("rank")] public int Rank;
    }

    [Serializable]
    public class RankingByGameResponse
    {
        [JsonProperty("game_id")] public string GameId;
        [JsonProperty("rankings")] public RankingEntry[] Rankings;
    }

    [Serializable]
    public class GlobalRankingEntry
    {
        [JsonProperty("user_id")] public string UserId;
        [JsonProperty("total_score")] public long TotalScore;
        [JsonProperty("rank")] public int Rank;
    }

    [Serializable]
    public class GlobalRankingResponse
    {
        [JsonProperty("rankings")] public GlobalRankingEntry[] Rankings;
    }

    [Serializable]
    public class AnalyticsEventRequest
    {
        [JsonProperty("game_id")] public string GameId;
        [JsonProperty("event_type")] public string EventType;
        [JsonProperty("payload")] public object Payload;
    }

    // ───── Iter 3 ─────

    [Serializable]
    public class ProfileResponse
    {
        [JsonProperty("user_id")] public string UserId;
        [JsonProperty("nickname")] public string Nickname;
        [JsonProperty("avatar_id")] public int AvatarId;
        [JsonProperty("coins")] public int Coins;
    }

    [Serializable]
    public class ProfileUpdateRequest
    {
        [JsonProperty("nickname")] public string Nickname;
        [JsonProperty("avatar_id")] public int AvatarId;
    }

    [Serializable]
    public class MissionView
    {
        [JsonProperty("mission_id")] public string MissionId;
        [JsonProperty("title")] public string Title;
        [JsonProperty("progress")] public int Progress;
        [JsonProperty("target")] public int Target;
        [JsonProperty("reward")] public int Reward;
        [JsonProperty("completed")] public bool Completed;
        [JsonProperty("claimed")] public bool Claimed;
    }

    [Serializable]
    public class MissionsTodayResponse
    {
        [JsonProperty("missions")] public MissionView[] Missions;
    }

    [Serializable]
    public class MissionClaimRequest
    {
        [JsonProperty("mission_id")] public string MissionId;
    }

    [Serializable]
    public class ClaimResult
    {
        [JsonProperty("ok")] public bool Ok;
        [JsonProperty("reward")] public int Reward;
        [JsonProperty("coins")] public int Coins;
    }

    [Serializable]
    public class ShareClaimRequest
    {
        [JsonProperty("platform")] public string Platform;
        [JsonProperty("highlight_tag")] public string HighlightTag;
    }

    [Serializable]
    public class PurchaseVerifyRequest
    {
        [JsonProperty("product_id")] public string ProductId;
        [JsonProperty("token")] public string Token;
    }

    [Serializable]
    public class PurchaseVerifyResponse
    {
        [JsonProperty("valid")] public bool Valid;
        [JsonProperty("product_id")] public string ProductId;
        [JsonProperty("ad_removed")] public bool AdRemoved;
    }
}
