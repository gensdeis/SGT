// Package anticheat 은 점수 제출 검증을 담당한다.
//
// 3단계 흐름:
//  1. HMAC Signature 검증 (pkg/hmac)
//  2. games.yaml 의 min_play_seconds, max_score 검증
//  3. Redis rate limiter (분당 제출 한도)
package anticheat

import (
	"context"
	"errors"

	"github.com/gensdeis/SGT/server/internal/config"
	"github.com/gensdeis/SGT/server/internal/metrics"
	"github.com/gensdeis/SGT/server/internal/ratelimit"
	"github.com/gensdeis/SGT/server/pkg/hmac"
	"github.com/google/uuid"
)

// 검증 거부 사유.
var (
	ErrInvalidSignature = errors.New("anticheat: invalid signature")
	ErrUnknownGame      = errors.New("anticheat: unknown game id")
	ErrPlayTimeTooShort = errors.New("anticheat: play time too short")
	ErrScoreOutOfRange  = errors.New("anticheat: score out of range")
	ErrRateLimited      = errors.New("anticheat: rate limit exceeded")
)

// Submission 은 점수 제출 페이로드 (HMAC payload + 사용자 컨텍스트).
type Submission struct {
	UserID    uuid.UUID
	GameID    string
	Score     int
	PlayTime  float64
	Timestamp int64
	Signature string
}

// Validator 는 검증기. 모든 의존성은 main.go 에서 주입.
type Validator struct {
	verifier *hmac.Verifier
	games    *config.GameRegistry
	limiter  ratelimit.Limiter
}

// NewValidator 는 검증기를 만든다.
func NewValidator(v *hmac.Verifier, g *config.GameRegistry, l ratelimit.Limiter) *Validator {
	return &Validator{verifier: v, games: g, limiter: l}
}

// Validate 는 3단계 검증을 수행한다.
// 통과 시 nil. 거부 시 위 에러 중 하나 + Prometheus reject counter 증가.
func (v *Validator) Validate(ctx context.Context, s Submission) error {
	// 1. games.yaml 조회
	game, ok := v.games.Get(s.GameID)
	if !ok {
		metrics.AnticheatRejectsTotal.WithLabelValues("unknown_game").Inc()
		return ErrUnknownGame
	}

	// 2. HMAC + replay
	if err := v.verifier.Verify(hmac.ScoreRequest{
		GameID:    s.GameID,
		Score:     s.Score,
		PlayTime:  s.PlayTime,
		Timestamp: s.Timestamp,
	}, s.Signature); err != nil {
		metrics.AnticheatRejectsTotal.WithLabelValues("hmac").Inc()
		return ErrInvalidSignature
	}

	// 3. 시간/점수 범위
	if int(s.PlayTime) < game.MinPlaySeconds {
		metrics.AnticheatRejectsTotal.WithLabelValues("play_time").Inc()
		return ErrPlayTimeTooShort
	}
	if s.Score > game.MaxScore || s.Score < 0 {
		metrics.AnticheatRejectsTotal.WithLabelValues("score_range").Inc()
		return ErrScoreOutOfRange
	}

	// 4. rate limit
	allowed, err := v.limiter.Allow(ctx, s.UserID, s.GameID, game.RateLimitPerMin)
	if err != nil {
		// rate limiter 자체 오류는 거부하지 않고 통과 (의존성 장애가 게임을 막으면 안 됨)
		return nil
	}
	if !allowed {
		metrics.AnticheatRejectsTotal.WithLabelValues("rate_limit").Inc()
		return ErrRateLimited
	}

	return nil
}
