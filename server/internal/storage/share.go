package storage

import (
	"context"

	"github.com/google/uuid"
)

// ClaimShareReward 는 (user, day_key) 가 처음이면 INSERT 하고 true 반환,
// 이미 있으면 false (일 1회 limit).
func (s *Store) ClaimShareReward(ctx context.Context, userID uuid.UUID, dayKey string) (bool, error) {
	tag, err := s.pool.Exec(ctx, `
		INSERT INTO share_rewards (user_id, day_key, count)
		VALUES ($1, $2, 1)
		ON CONFLICT (user_id, day_key) DO NOTHING`,
		userID, dayKey)
	if err != nil {
		return false, err
	}
	return tag.RowsAffected() > 0, nil
}
