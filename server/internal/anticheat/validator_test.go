package anticheat

import (
	"context"
	"errors"
	"testing"
	"time"

	"github.com/gensdeis/SGT/server/internal/config"
	"github.com/gensdeis/SGT/server/internal/ratelimit"
	"github.com/gensdeis/SGT/server/pkg/hmac"
	"github.com/google/uuid"
)

func newTestValidator(t *testing.T, fixedNow time.Time) (*Validator, *hmac.Verifier) {
	t.Helper()
	v := hmac.NewVerifier("base", "salt", 30*time.Second)
	v.SetClock(func() time.Time { return fixedNow })
	// 인메모리 게임 레지스트리 (단일 게임)
	reg, err := loadInline(t, `
games:
  - id: "frog_catch_v1"
    title: "test"
    creator_id: "x"
    time_limit_sec: 30
    min_play_seconds: 8
    max_score: 1000
    rate_limit_per_min: 10
    tags: ["반응속도"]
`)
	if err != nil {
		t.Fatal(err)
	}
	return NewValidator(v, reg, &ratelimit.MockLimiter{}), v
}

func loadInline(t *testing.T, yaml string) (*config.GameRegistry, error) {
	t.Helper()
	tmp := t.TempDir() + "/games.yaml"
	if err := writeFile(tmp, yaml); err != nil {
		return nil, err
	}
	return config.LoadGames(tmp)
}

func writeFile(path, content string) error {
	return osWrite(path, []byte(content))
}

func TestValidate_Pass(t *testing.T) {
	now := time.Date(2026, 4, 6, 12, 0, 0, 0, time.UTC)
	val, ver := newTestValidator(t, now)
	uid := uuid.New()
	sub := Submission{
		UserID:    uid,
		GameID:    "frog_catch_v1",
		Score:     500,
		PlayTime:  12.5,
		Timestamp: now.Unix(),
	}
	sub.Signature = ver.Sign(hmac.ScoreRequest{GameID: sub.GameID, Score: sub.Score, PlayTime: sub.PlayTime, Timestamp: sub.Timestamp})
	if err := val.Validate(context.Background(), sub); err != nil {
		t.Fatalf("expected pass, got %v", err)
	}
}

func TestValidate_UnknownGame(t *testing.T) {
	now := time.Date(2026, 4, 6, 12, 0, 0, 0, time.UTC)
	val, _ := newTestValidator(t, now)
	sub := Submission{GameID: "nope", Score: 1, PlayTime: 10, Timestamp: now.Unix()}
	if err := val.Validate(context.Background(), sub); !errors.Is(err, ErrUnknownGame) {
		t.Fatalf("expected ErrUnknownGame, got %v", err)
	}
}

func TestValidate_PlayTimeTooShort(t *testing.T) {
	now := time.Date(2026, 4, 6, 12, 0, 0, 0, time.UTC)
	val, ver := newTestValidator(t, now)
	sub := Submission{GameID: "frog_catch_v1", Score: 100, PlayTime: 3, Timestamp: now.Unix(), UserID: uuid.New()}
	sub.Signature = ver.Sign(hmac.ScoreRequest{GameID: sub.GameID, Score: sub.Score, PlayTime: sub.PlayTime, Timestamp: sub.Timestamp})
	if err := val.Validate(context.Background(), sub); !errors.Is(err, ErrPlayTimeTooShort) {
		t.Fatalf("expected ErrPlayTimeTooShort, got %v", err)
	}
}

func TestValidate_ScoreOverMax(t *testing.T) {
	now := time.Date(2026, 4, 6, 12, 0, 0, 0, time.UTC)
	val, ver := newTestValidator(t, now)
	sub := Submission{GameID: "frog_catch_v1", Score: 999999, PlayTime: 20, Timestamp: now.Unix(), UserID: uuid.New()}
	sub.Signature = ver.Sign(hmac.ScoreRequest{GameID: sub.GameID, Score: sub.Score, PlayTime: sub.PlayTime, Timestamp: sub.Timestamp})
	if err := val.Validate(context.Background(), sub); !errors.Is(err, ErrScoreOutOfRange) {
		t.Fatalf("expected ErrScoreOutOfRange, got %v", err)
	}
}

func TestValidate_BadSignature(t *testing.T) {
	now := time.Date(2026, 4, 6, 12, 0, 0, 0, time.UTC)
	val, _ := newTestValidator(t, now)
	sub := Submission{GameID: "frog_catch_v1", Score: 100, PlayTime: 20, Timestamp: now.Unix(), Signature: "deadbeef", UserID: uuid.New()}
	if err := val.Validate(context.Background(), sub); !errors.Is(err, ErrInvalidSignature) {
		t.Fatalf("expected ErrInvalidSignature, got %v", err)
	}
}

func TestValidate_RateLimited(t *testing.T) {
	now := time.Date(2026, 4, 6, 12, 0, 0, 0, time.UTC)
	verifier := hmac.NewVerifier("base", "salt", 30*time.Second)
	verifier.SetClock(func() time.Time { return now })
	reg, _ := loadInline(t, `
games:
  - id: "frog_catch_v1"
    title: "t"
    creator_id: "x"
    time_limit_sec: 30
    min_play_seconds: 8
    max_score: 1000
    rate_limit_per_min: 10
    tags: ["a"]
`)
	denied := &ratelimit.MockLimiter{AllowFn: func() (bool, error) { return false, nil }}
	val := NewValidator(verifier, reg, denied)
	sub := Submission{GameID: "frog_catch_v1", Score: 100, PlayTime: 20, Timestamp: now.Unix(), UserID: uuid.New()}
	sub.Signature = verifier.Sign(hmac.ScoreRequest{GameID: sub.GameID, Score: sub.Score, PlayTime: sub.PlayTime, Timestamp: sub.Timestamp})
	if err := val.Validate(context.Background(), sub); !errors.Is(err, ErrRateLimited) {
		t.Fatalf("expected ErrRateLimited, got %v", err)
	}
}
