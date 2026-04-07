// Package share 는 하이라이트 공유 보상.
package share

import (
	"context"
	"time"

	"github.com/gensdeis/SGT/server/internal/storage"
	"github.com/google/uuid"
)

const RewardCoins int32 = 10

type Service struct {
	store *storage.Store
}

func NewService(store *storage.Store) *Service { return &Service{store: store} }

type ClaimResult struct {
	OK     bool  `json:"ok"`
	Reward int32 `json:"reward"`
	Coins  int32 `json:"coins"`
}

func (s *Service) Claim(ctx context.Context, userID uuid.UUID) (ClaimResult, error) {
	dk := time.Now().UTC().Format("20060102")
	first, err := s.store.ClaimShareReward(ctx, userID, dk)
	if err != nil {
		return ClaimResult{}, err
	}
	if !first {
		return ClaimResult{OK: false}, nil
	}
	coins, err := s.store.IncCoins(ctx, userID, RewardCoins)
	if err != nil {
		return ClaimResult{}, err
	}
	return ClaimResult{OK: true, Reward: RewardCoins, Coins: coins}, nil
}
