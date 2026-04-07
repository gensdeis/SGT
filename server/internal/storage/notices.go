package storage

import (
	"context"
	"time"

	"github.com/google/uuid"
)

// Iter 4c: 공지 + 밴.

type Notice struct {
	ID        int64      `json:"id"`
	Title     string     `json:"title"`
	Body      string     `json:"body"`
	CreatedAt time.Time  `json:"created_at"`
	ExpiresAt *time.Time `json:"expires_at,omitempty"`
}

func (s *Store) ListNotices(ctx context.Context, limit int) ([]Notice, error) {
	if limit <= 0 || limit > 200 {
		limit = 50
	}
	rows, err := s.pool.Query(ctx, `
		SELECT id, title, body, created_at, expires_at
		FROM notices ORDER BY created_at DESC LIMIT $1`, limit)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	var out []Notice
	for rows.Next() {
		var n Notice
		if err := rows.Scan(&n.ID, &n.Title, &n.Body, &n.CreatedAt, &n.ExpiresAt); err != nil {
			return nil, err
		}
		out = append(out, n)
	}
	return out, rows.Err()
}

// ListActiveNotices 는 expires_at 이 NULL 이거나 미래인 것만 — 클라용.
func (s *Store) ListActiveNotices(ctx context.Context) ([]Notice, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT id, title, body, created_at, expires_at
		FROM notices
		WHERE expires_at IS NULL OR expires_at > now()
		ORDER BY created_at DESC LIMIT 20`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	var out []Notice
	for rows.Next() {
		var n Notice
		if err := rows.Scan(&n.ID, &n.Title, &n.Body, &n.CreatedAt, &n.ExpiresAt); err != nil {
			return nil, err
		}
		out = append(out, n)
	}
	return out, rows.Err()
}

func (s *Store) CreateNotice(ctx context.Context, title, body string, expiresAt *time.Time) (Notice, error) {
	row := s.pool.QueryRow(ctx, `
		INSERT INTO notices (title, body, expires_at)
		VALUES ($1, $2, $3)
		RETURNING id, title, body, created_at, expires_at`,
		title, body, expiresAt)
	var n Notice
	if err := row.Scan(&n.ID, &n.Title, &n.Body, &n.CreatedAt, &n.ExpiresAt); err != nil {
		return Notice{}, err
	}
	return n, nil
}

func (s *Store) DeleteNotice(ctx context.Context, id int64) error {
	_, err := s.pool.Exec(ctx, `DELETE FROM notices WHERE id = $1`, id)
	return err
}

// SetUserBanned 토글.
func (s *Store) SetUserBanned(ctx context.Context, id uuid.UUID, banned bool) error {
	_, err := s.pool.Exec(ctx, `UPDATE users SET banned = $2 WHERE id = $1`, id, banned)
	return err
}
