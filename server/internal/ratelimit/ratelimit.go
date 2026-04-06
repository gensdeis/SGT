// Package ratelimit 은 점수 제출 등 짧은 윈도우 내 호출 빈도를 제한한다.
//
// Phase 1 구현: Redis INCR + EXPIRE 60s 간단 슬라이딩 윈도우.
// 키 형식: ratelimit:{userID}:{gameID}
package ratelimit

import (
	"context"
	"fmt"
	"time"

	"github.com/google/uuid"
	"github.com/redis/go-redis/v9"
)

// Limiter 는 인터페이스. 핸들러/검증기는 이걸만 본다 (Mock 주입 용이).
type Limiter interface {
	Allow(ctx context.Context, userID uuid.UUID, gameID string, perMinute int) (bool, error)
}

// RedisLimiter 는 Redis 기반 구현체.
type RedisLimiter struct {
	rdb *redis.Client
}

func NewRedisLimiter(rdb *redis.Client) *RedisLimiter {
	return &RedisLimiter{rdb: rdb}
}

// Allow 는 분당 한도를 초과했는지 판단한다.
// 키가 처음 생성될 때 60초 만료를 설정한다.
func (l *RedisLimiter) Allow(ctx context.Context, userID uuid.UUID, gameID string, perMinute int) (bool, error) {
	if perMinute <= 0 {
		return true, nil
	}
	key := fmt.Sprintf("ratelimit:%s:%s", userID.String(), gameID)
	pipe := l.rdb.TxPipeline()
	incr := pipe.Incr(ctx, key)
	pipe.Expire(ctx, key, time.Minute)
	if _, err := pipe.Exec(ctx); err != nil {
		return false, err
	}
	return incr.Val() <= int64(perMinute), nil
}

// MockLimiter 는 테스트용. AllowFn 으로 결과를 제어.
type MockLimiter struct {
	AllowFn func() (bool, error)
}

func (m *MockLimiter) Allow(_ context.Context, _ uuid.UUID, _ string, _ int) (bool, error) {
	if m.AllowFn != nil {
		return m.AllowFn()
	}
	return true, nil
}
