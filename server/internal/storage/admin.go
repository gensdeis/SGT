package storage

import (
	"context"
	"time"

	"github.com/google/uuid"
)

// Iter 4a: admin_users + 검색.
// raw pgx (sqlc 우회) — sqlc 정식 재생성은 후속.

type AdminUser struct {
	ID           uuid.UUID `json:"id"`
	Login        string    `json:"login"`
	PasswordHash string    `json:"-"`
	Role         string    `json:"role"`
	CreatedAt    time.Time `json:"created_at"`
}

func (s *Store) GetAdminByLogin(ctx context.Context, login string) (AdminUser, error) {
	row := s.pool.QueryRow(ctx,
		`SELECT id, login, password_hash, role, created_at FROM admin_users WHERE login = $1`, login)
	var a AdminUser
	if err := row.Scan(&a.ID, &a.Login, &a.PasswordHash, &a.Role, &a.CreatedAt); err != nil {
		return AdminUser{}, wrapNoRows(err)
	}
	return a, nil
}

// EnsureAdmin 은 idempotent — 이미 있으면 no-op.
func (s *Store) EnsureAdmin(ctx context.Context, login, passwordHash, role string) error {
	_, err := s.pool.Exec(ctx, `
		INSERT INTO admin_users (login, password_hash, role)
		VALUES ($1, $2, $3)
		ON CONFLICT (login) DO NOTHING`,
		login, passwordHash, role)
	return err
}

// UserSearchRow 는 admin /users 검색 결과.
type UserSearchRow struct {
	ID       uuid.UUID `json:"id"`
	DeviceID string    `json:"device_id"`
	Nickname string    `json:"nickname"`
	Coins    int32     `json:"coins"`
	Banned   bool      `json:"banned"`
}

// SearchUsers 는 device_id prefix 또는 uuid prefix 로 검색. 빈 q 면 최근 50.
func (s *Store) SearchUsers(ctx context.Context, q string, limit int) ([]UserSearchRow, error) {
	if limit <= 0 || limit > 200 {
		limit = 50
	}
	var rows []UserSearchRow
	if q == "" {
		r, err := s.pool.Query(ctx, `
			SELECT id, device_id, nickname, coins, banned
			FROM users ORDER BY last_seen_at DESC LIMIT $1`, limit)
		if err != nil {
			return nil, err
		}
		defer r.Close()
		for r.Next() {
			var u UserSearchRow
			if err := r.Scan(&u.ID, &u.DeviceID, &u.Nickname, &u.Coins, &u.Banned); err != nil {
				return nil, err
			}
			rows = append(rows, u)
		}
		return rows, r.Err()
	}
	r, err := s.pool.Query(ctx, `
		SELECT id, device_id, nickname, coins, banned
		FROM users
		WHERE device_id ILIKE $1 OR id::text ILIKE $1
		ORDER BY last_seen_at DESC LIMIT $2`,
		q+"%", limit)
	if err != nil {
		return nil, err
	}
	defer r.Close()
	for r.Next() {
		var u UserSearchRow
		if err := r.Scan(&u.ID, &u.DeviceID, &u.Nickname, &u.Coins, &u.Banned); err != nil {
			return nil, err
		}
		rows = append(rows, u)
	}
	return rows, r.Err()
}
