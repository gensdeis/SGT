package missions

import (
	"context"
	"errors"
	"time"

	"github.com/gensdeis/SGT/server/internal/storage"
	"github.com/google/uuid"
)

type Service struct {
	store *storage.Store
}

func NewService(store *storage.Store) *Service {
	return &Service{store: store}
}

// EnsureToday 는 오늘 미션이 없으면 INSERT 한 뒤 전체 목록 반환.
func (s *Service) EnsureToday(ctx context.Context, userID uuid.UUID) ([]storage.DailyMission, error) {
	dk := DayKeyUTC(time.Now())
	for _, d := range All {
		if err := s.store.EnsureDailyMission(ctx, userID, d.ID, dk, d.Target); err != nil {
			return nil, err
		}
	}
	return s.store.ListDailyMissions(ctx, userID, dk)
}

// ClaimResult 는 claim 결과.
type ClaimResult struct {
	OK     bool  `json:"ok"`
	Reward int32 `json:"reward"`
	Coins  int32 `json:"coins"`
}

// Claim 은 완료된 미션을 claim 하고 코인을 지급.
func (s *Service) Claim(ctx context.Context, userID uuid.UUID, missionID string) (ClaimResult, error) {
	def, ok := FindByID(missionID)
	if !ok {
		return ClaimResult{}, errors.New("unknown mission")
	}
	dk := DayKeyUTC(time.Now())
	claimed, err := s.store.ClaimMission(ctx, userID, missionID, dk)
	if err != nil {
		return ClaimResult{}, err
	}
	if !claimed {
		return ClaimResult{OK: false}, nil
	}
	coins, err := s.store.IncCoins(ctx, userID, def.Reward)
	if err != nil {
		return ClaimResult{}, err
	}
	return ClaimResult{OK: true, Reward: def.Reward, Coins: coins}, nil
}

// OnSessionResult 는 session.Service 가 SessionResult 제출 시 호출하는 hook.
// best-effort: 에러는 로그만, 트랜잭션 분리.
func (s *Service) OnSessionResult(ctx context.Context, userID uuid.UUID, cleared bool, playTimeSec float64) {
	dk := DayKeyUTC(time.Now())
	// 모든 정의에 대해 ensure (첫 호출 시)
	for _, d := range All {
		_ = s.store.EnsureDailyMission(ctx, userID, d.ID, dk, d.Target)
	}
	_ = s.store.IncMissionProgress(ctx, userID, "play_3_games", dk, 1)
	if cleared {
		_ = s.store.IncMissionProgress(ctx, userID, "clear_1_game", dk, 1)
	}
	if playTimeSec > 0 {
		_ = s.store.IncMissionProgress(ctx, userID, "play_total_60s", dk, int32(playTimeSec))
	}
}
