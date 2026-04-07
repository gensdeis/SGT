package storage

import (
	"context"
	"time"

	"github.com/google/uuid"
)

// Iter 4b: 운영툴 추가 helper. raw pgx.

// === Games CRUD ===

func (s *Store) ListGamesAdmin(ctx context.Context) ([]Game, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT id, title, creator_id, time_limit_sec, tags, bundle_url, bundle_version, bundle_hash, created_at
		FROM games ORDER BY id`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	var out []Game
	for rows.Next() {
		var g Game
		if err := rows.Scan(&g.ID, &g.Title, &g.CreatorID, &g.TimeLimitSec, &g.Tags,
			&g.BundleURL, &g.BundleVersion, &g.BundleHash, &g.CreatedAt); err != nil {
			return nil, err
		}
		out = append(out, g)
	}
	return out, rows.Err()
}

// UpsertGameAdmin 은 INSERT OR UPDATE.
func (s *Store) UpsertGameAdmin(ctx context.Context, g Game) error {
	_, err := s.pool.Exec(ctx, `
		INSERT INTO games (id, title, creator_id, time_limit_sec, tags, bundle_url, bundle_version, bundle_hash)
		VALUES ($1,$2,$3,$4,$5,$6,$7,$8)
		ON CONFLICT (id) DO UPDATE SET
			title=$2, creator_id=$3, time_limit_sec=$4, tags=$5,
			bundle_url=$6, bundle_version=$7, bundle_hash=$8`,
		g.ID, g.Title, g.CreatorID, g.TimeLimitSec, g.Tags, g.BundleURL, g.BundleVersion, g.BundleHash)
	return err
}

// === Dashboard ===

type DashboardStats struct {
	DAU             int64 `json:"dau"`
	PlaysToday      int64 `json:"plays_today"`
	SessionsToday   int64 `json:"sessions_today"`
	TotalUsers      int64 `json:"total_users"`
}

func (s *Store) DashboardStats(ctx context.Context) (DashboardStats, error) {
	var d DashboardStats
	row := s.pool.QueryRow(ctx, `
		SELECT
			(SELECT COUNT(DISTINCT user_id) FROM sessions WHERE started_at >= now() - interval '1 day'),
			(SELECT COUNT(*) FROM session_results WHERE submitted_at >= now() - interval '1 day'),
			(SELECT COUNT(*) FROM sessions WHERE started_at >= now() - interval '1 day'),
			(SELECT COUNT(*) FROM users)`)
	if err := row.Scan(&d.DAU, &d.PlaysToday, &d.SessionsToday, &d.TotalUsers); err != nil {
		return DashboardStats{}, err
	}
	return d, nil
}

// === Sessions ===

type SessionRow struct {
	ID         uuid.UUID  `json:"id"`
	UserID     uuid.UUID  `json:"user_id"`
	StartedAt  time.Time  `json:"started_at"`
	EndedAt    *time.Time `json:"ended_at,omitempty"`
	GameCount  int        `json:"game_count"`
	TotalScore int64      `json:"total_score"`
}

func (s *Store) RecentSessions(ctx context.Context, limit int) ([]SessionRow, error) {
	if limit <= 0 || limit > 200 {
		limit = 50
	}
	rows, err := s.pool.Query(ctx, `
		SELECT s.id, s.user_id, s.started_at, s.ended_at,
		       COALESCE((SELECT COUNT(*) FROM session_results sr WHERE sr.session_id = s.id), 0),
		       COALESCE((SELECT SUM(score) FROM session_results sr WHERE sr.session_id = s.id), 0)
		FROM sessions s
		ORDER BY s.started_at DESC
		LIMIT $1`, limit)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	var out []SessionRow
	for rows.Next() {
		var r SessionRow
		if err := rows.Scan(&r.ID, &r.UserID, &r.StartedAt, &r.EndedAt, &r.GameCount, &r.TotalScore); err != nil {
			return nil, err
		}
		out = append(out, r)
	}
	return out, rows.Err()
}

// === Rankings (admin reuse) ===

type AdminRankingRow struct {
	UserID    uuid.UUID `json:"user_id"`
	BestScore int32     `json:"best_score"`
	Rank      int       `json:"rank"`
}

func (s *Store) RankingByGameAdmin(ctx context.Context, gameID string, limit int) ([]AdminRankingRow, error) {
	if limit <= 0 || limit > 500 {
		limit = 100
	}
	rows, err := s.pool.Query(ctx, `
		SELECT user_id, best_score
		FROM score_aggregates
		WHERE game_id = $1
		ORDER BY best_score DESC
		LIMIT $2`, gameID, limit)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	var out []AdminRankingRow
	rk := 1
	for rows.Next() {
		var r AdminRankingRow
		if err := rows.Scan(&r.UserID, &r.BestScore); err != nil {
			return nil, err
		}
		r.Rank = rk
		rk++
		out = append(out, r)
	}
	return out, rows.Err()
}
