package hmac

import (
	"errors"
	"testing"
	"time"
)

func newTestVerifier(t *testing.T, fixedNow time.Time) *Verifier {
	t.Helper()
	v := NewVerifier("test-base-key", "test-build-guid", 30*time.Second)
	v.SetClock(func() time.Time { return fixedNow })
	return v
}

func TestDeriveSecretKey_PerGameUnique(t *testing.T) {
	v := NewVerifier("base", "salt", 30*time.Second)
	a := v.DeriveSecretKey("frog_catch_v1")
	b := v.DeriveSecretKey("noodle_boil_v1")
	if string(a) == string(b) {
		t.Fatal("expected per-game keys to differ")
	}
	if len(a) != 32 {
		t.Fatalf("expected 32 bytes (sha256), got %d", len(a))
	}
}

func TestDeriveSecretKey_DifferentSalt(t *testing.T) {
	v1 := NewVerifier("base", "salt1", 30*time.Second)
	v2 := NewVerifier("base", "salt2", 30*time.Second)
	if string(v1.DeriveSecretKey("g")) == string(v2.DeriveSecretKey("g")) {
		t.Fatal("expected different salts to produce different keys")
	}
}

func TestVerify_RoundTrip(t *testing.T) {
	now := time.Date(2026, 4, 6, 12, 0, 0, 0, time.UTC)
	v := newTestVerifier(t, now)
	req := ScoreRequest{GameID: "frog_catch_v1", Score: 500, PlayTime: 12.5, Timestamp: now.Unix()}
	sig := v.Sign(req)
	if err := v.Verify(req, sig); err != nil {
		t.Fatalf("expected verify ok, got %v", err)
	}
}

func TestVerify_BadSignature(t *testing.T) {
	now := time.Date(2026, 4, 6, 12, 0, 0, 0, time.UTC)
	v := newTestVerifier(t, now)
	req := ScoreRequest{GameID: "g", Score: 1, PlayTime: 1.0, Timestamp: now.Unix()}
	if err := v.Verify(req, "deadbeef"); !errors.Is(err, ErrInvalidSignature) {
		t.Fatalf("expected ErrInvalidSignature, got %v", err)
	}
}

func TestVerify_ReplayExpired(t *testing.T) {
	now := time.Date(2026, 4, 6, 12, 0, 0, 0, time.UTC)
	v := newTestVerifier(t, now)
	req := ScoreRequest{GameID: "g", Score: 1, PlayTime: 1.0, Timestamp: now.Add(-2 * time.Minute).Unix()}
	sig := v.Sign(req)
	if err := v.Verify(req, sig); !errors.Is(err, ErrReplayExpired) {
		t.Fatalf("expected ErrReplayExpired, got %v", err)
	}
}

func TestVerify_FutureTimestamp(t *testing.T) {
	now := time.Date(2026, 4, 6, 12, 0, 0, 0, time.UTC)
	v := newTestVerifier(t, now)
	req := ScoreRequest{GameID: "g", Score: 1, PlayTime: 1.0, Timestamp: now.Add(2 * time.Minute).Unix()}
	sig := v.Sign(req)
	if err := v.Verify(req, sig); !errors.Is(err, ErrTimestampFuture) {
		t.Fatalf("expected ErrTimestampFuture, got %v", err)
	}
}

func TestVerify_PayloadTamper(t *testing.T) {
	now := time.Date(2026, 4, 6, 12, 0, 0, 0, time.UTC)
	v := newTestVerifier(t, now)
	req := ScoreRequest{GameID: "g", Score: 100, PlayTime: 10, Timestamp: now.Unix()}
	sig := v.Sign(req)
	// 점수만 살짝 늘려 검증
	req.Score = 999999
	if err := v.Verify(req, sig); !errors.Is(err, ErrInvalidSignature) {
		t.Fatalf("expected tamper detection, got %v", err)
	}
}
