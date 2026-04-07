package storage

import (
	"context"

	"github.com/google/uuid"
)

// Iter 3: profile/coin 컬럼은 sqlc 재생성 없이 raw pgx 로 조회/업데이트.
// 본 모듈에서만 사용하는 protocol 이므로 store.go 의 sqlc 위임 규칙 예외.

type Profile struct {
	UserID   uuid.UUID `json:"user_id"`
	Nickname string    `json:"nickname"`
	AvatarID int32     `json:"avatar_id"`
	Coins    int32     `json:"coins"`
	Banned   bool      `json:"banned"`
}

func (s *Store) GetProfile(ctx context.Context, id uuid.UUID) (Profile, error) {
	row := s.pool.QueryRow(ctx,
		`SELECT id, nickname, avatar_id, coins, COALESCE(banned, false) FROM users WHERE id = $1`, id)
	var p Profile
	if err := row.Scan(&p.UserID, &p.Nickname, &p.AvatarID, &p.Coins, &p.Banned); err != nil {
		return Profile{}, wrapNoRows(err)
	}
	return p, nil
}

func (s *Store) UpdateProfile(ctx context.Context, id uuid.UUID, nickname string, avatarID int32) error {
	_, err := s.pool.Exec(ctx,
		`UPDATE users SET nickname = $2, avatar_id = $3 WHERE id = $1`,
		id, nickname, avatarID)
	return err
}

// IncCoins 는 코인을 더한다 (음수 가능). 현재 잔액 반환.
func (s *Store) IncCoins(ctx context.Context, id uuid.UUID, delta int32) (int32, error) {
	row := s.pool.QueryRow(ctx,
		`UPDATE users SET coins = coins + $2 WHERE id = $1 RETURNING coins`,
		id, delta)
	var coins int32
	if err := row.Scan(&coins); err != nil {
		return 0, err
	}
	return coins, nil
}
