// Package hmac 는 점수 서명 동적 키 파생 + 검증을 담당한다.
//
// 설계 근거: Docs/BACKEND_PLAN.md §"Anti-cheat — HMAC 동적 키 파생"
//
//	레이어 1 (클라이언트): 점수/시간 변수 XOR 난독화 + IL2CPP
//	레이어 2 (전송 구간): 본 패키지 — HMAC Signature 무결성 검증
//	레이어 3 (서버): games.yaml 기반 시간/점수/rate-limit 검증
package hmac

import (
	cryptohmac "crypto/hmac"
	"crypto/sha256"
	"encoding/hex"
	"errors"
	"fmt"
	"time"
)

// ScoreRequest 는 클라이언트가 보낸 점수 페이로드.
// 페이로드 직렬화 형식은 "{gameID}:{score}:{playTime:.2f}:{timestamp}".
type ScoreRequest struct {
	GameID    string  `json:"game_id"`
	Score     int     `json:"score"`
	PlayTime  float64 `json:"play_time"`
	Timestamp int64   `json:"timestamp"`
}

// Verifier 는 HMAC 검증기. baseKey 와 buildSalt 는 환경변수 주입.
// 두 값은 sealed secret 으로 관리되며 코드/이미지에 하드코딩 금지.
type Verifier struct {
	baseKey      []byte
	buildSalt    []byte
	replayWindow time.Duration
	now          func() time.Time // 테스트용 주입
}

// NewVerifier 는 새 검증기를 생성한다. replayWindow 는 30s 권장.
func NewVerifier(baseKey, buildSalt string, replayWindow time.Duration) *Verifier {
	return &Verifier{
		baseKey:      []byte(baseKey),
		buildSalt:    []byte(buildSalt),
		replayWindow: replayWindow,
		now:          time.Now,
	}
}

// SetClock 은 테스트에서 시계를 주입한다.
func (v *Verifier) SetClock(now func() time.Time) {
	v.now = now
}

// DeriveSecretKey 는 게임 ID 별 고유 비밀키를 파생한다.
//
//	HMAC_SHA256(baseKey, gameID || buildSalt)
//
// 클라이언트(C#)와 서버(Go) 양쪽이 같은 식을 사용해야 매칭된다.
// 키 하나가 노출돼도 다른 게임 키는 보호된다.
func (v *Verifier) DeriveSecretKey(gameID string) []byte {
	mac := cryptohmac.New(sha256.New, v.baseKey)
	mac.Write([]byte(gameID))
	mac.Write(v.buildSalt)
	return mac.Sum(nil)
}

// Sign 은 (테스트/디버깅 편의용) 페이로드 서명 생성. 클라이언트는 이걸 사용 금지.
func (v *Verifier) Sign(req ScoreRequest) string {
	secret := v.DeriveSecretKey(req.GameID)
	payload := payloadString(req)
	mac := cryptohmac.New(sha256.New, secret)
	mac.Write([]byte(payload))
	return hex.EncodeToString(mac.Sum(nil))
}

// 검증 실패 사유.
var (
	ErrInvalidSignature = errors.New("hmac: invalid signature")
	ErrReplayExpired    = errors.New("hmac: replay window expired")
	ErrTimestampFuture  = errors.New("hmac: timestamp in the future")
)

// Verify 는 서명 + Replay Attack(타임스탬프 윈도우)을 검증한다.
func (v *Verifier) Verify(req ScoreRequest, signature string) error {
	now := v.now().Unix()

	// 미래 timestamp 거부 (clock skew 5초 허용)
	if req.Timestamp > now+5 {
		return ErrTimestampFuture
	}

	// Replay window
	if now-req.Timestamp > int64(v.replayWindow.Seconds()) {
		return ErrReplayExpired
	}

	expected := v.Sign(req)
	if !cryptohmac.Equal([]byte(expected), []byte(signature)) {
		return ErrInvalidSignature
	}
	return nil
}

func payloadString(req ScoreRequest) string {
	return fmt.Sprintf("%s:%d:%.2f:%d", req.GameID, req.Score, req.PlayTime, req.Timestamp)
}
