// Package storage 는 DB 접근 레이어.
//
// TODO: 이 파일과 store*.go 의 수기 코드는 sqlc 생성 코드로 교체 예정.
//   sqlc CLI 설치 후 `sqlc generate` 실행하면 internal/storage/sqlc/ 에 같은 인터페이스로
//   자동 생성된다. 그때까지는 BACKEND_PLAN.md "sqlc only" 규칙의 정신 (SQL 격리, 타입 안전)
//   을 유지하기 위해 모든 쿼리를 이 패키지에 모아둔다.
package storage

import (
	"time"

	"github.com/google/uuid"
)

// User 는 users 테이블.
type User struct {
	ID         uuid.UUID `json:"id"`
	DeviceID   string    `json:"device_id"`
	CreatedAt  time.Time `json:"created_at"`
	LastSeenAt time.Time `json:"last_seen_at"`
}

// Game 은 games 테이블.
type Game struct {
	ID            string    `json:"id"`
	Title         string    `json:"title"`
	CreatorID     string    `json:"creator_id"`
	TimeLimitSec  int32     `json:"time_limit_sec"`
	Tags          []string  `json:"tags"`
	BundleURL     string    `json:"bundle_url"`
	BundleVersion string    `json:"bundle_version"`
	BundleHash    string    `json:"bundle_hash"`
	CreatedAt     time.Time `json:"created_at"`
}

// Session 은 sessions 테이블.
type Session struct {
	ID                  uuid.UUID  `json:"id"`
	UserID              uuid.UUID  `json:"user_id"`
	StartedAt           time.Time  `json:"started_at"`
	EndedAt             *time.Time `json:"ended_at,omitempty"`
	RecommendedGameIDs  []string   `json:"recommended_game_ids"`
	DDAIntensity        int32      `json:"dda_intensity"`
}

// SessionResult 는 session_results 테이블.
type SessionResult struct {
	SessionID    uuid.UUID `json:"session_id"`
	GameID       string    `json:"game_id"`
	Score        int32     `json:"score"`
	PlayTimeSec  float32   `json:"play_time_sec"`
	Cleared      bool      `json:"cleared"`
	SubmittedAt  time.Time `json:"submitted_at"`
}

// AnalyticsEvent 는 analytics_events 테이블.
type AnalyticsEvent struct {
	ID        int64     `json:"id"`
	UserID    uuid.UUID `json:"user_id"`
	GameID    string    `json:"game_id"`
	EventType string    `json:"event_type"`
	Payload   []byte    `json:"payload"`
	CreatedAt time.Time `json:"created_at"`
}

// Purchase 는 purchases 테이블.
type Purchase struct {
	ID           uuid.UUID  `json:"id"`
	UserID       uuid.UUID  `json:"user_id"`
	ProductID    string     `json:"product_id"`
	Platform     string     `json:"platform"`
	ReceiptToken string     `json:"receipt_token"`
	Verified     bool       `json:"verified"`
	ExpiresAt    *time.Time `json:"expires_at,omitempty"`
	CreatedAt    time.Time  `json:"created_at"`
}

// ScoreAggregate 는 score_aggregates 테이블.
type ScoreAggregate struct {
	GameID    string    `json:"game_id"`
	UserID    uuid.UUID `json:"user_id"`
	BestScore int32     `json:"best_score"`
	UpdatedAt time.Time `json:"updated_at"`
}

// GlobalRankWeekly 는 global_rankings_weekly 테이블.
type GlobalRankWeekly struct {
	WeekStart  time.Time `json:"week_start"`
	UserID     uuid.UUID `json:"user_id"`
	TotalScore int64     `json:"total_score"`
	Rank       int32     `json:"rank"`
}
