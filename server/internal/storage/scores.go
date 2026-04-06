package storage

import (
	"context"
	"time"

	"github.com/google/uuid"
)

const sqlUpsertBestScore = `
INSERT INTO score_aggregates (game_id, user_id, best_score, updated_at)
VALUES ($1, $2, $3, now())
ON CONFLICT (game_id, user_id) DO UPDATE SET
    best_score = GREATEST(score_aggregates.best_score, EXCLUDED.best_score),
    updated_at = now()`

func (s *Store) UpsertBestScore(ctx context.Context, gameID string, userID uuid.UUID, score int32) error {
	_, err := s.pool.Exec(ctx, sqlUpsertBestScore, gameID, userID, score)
	return err
}

const sqlListTopByGame = `
SELECT game_id, user_id, best_score, updated_at
FROM score_aggregates
WHERE game_id = $1
ORDER BY best_score DESC
LIMIT $2`

func (s *Store) ListTopByGame(ctx context.Context, gameID string, limit int32) ([]ScoreAggregate, error) {
	rows, err := s.pool.Query(ctx, sqlListTopByGame, gameID, limit)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := make([]ScoreAggregate, 0)
	for rows.Next() {
		var a ScoreAggregate
		if err := rows.Scan(&a.GameID, &a.UserID, &a.BestScore, &a.UpdatedAt); err != nil {
			return nil, err
		}
		out = append(out, a)
	}
	return out, rows.Err()
}

const sqlGetUserBest = `
SELECT game_id, user_id, best_score, updated_at
FROM score_aggregates WHERE game_id = $1 AND user_id = $2`

func (s *Store) GetUserBest(ctx context.Context, gameID string, userID uuid.UUID) (ScoreAggregate, error) {
	var a ScoreAggregate
	err := s.pool.QueryRow(ctx, sqlGetUserBest, gameID, userID).Scan(&a.GameID, &a.UserID, &a.BestScore, &a.UpdatedAt)
	return a, wrapNoRows(err)
}

const sqlUpsertWeeklyRank = `
INSERT INTO global_rankings_weekly (week_start, user_id, total_score, rank)
VALUES ($1, $2, $3, $4)
ON CONFLICT (week_start, user_id) DO UPDATE SET
    total_score = EXCLUDED.total_score,
    rank = EXCLUDED.rank`

func (s *Store) UpsertWeeklyRank(ctx context.Context, weekStart time.Time, userID uuid.UUID, totalScore int64, rank int32) error {
	_, err := s.pool.Exec(ctx, sqlUpsertWeeklyRank, weekStart, userID, totalScore, rank)
	return err
}

const sqlListGlobalTop = `
SELECT week_start, user_id, total_score, rank
FROM global_rankings_weekly
WHERE week_start = $1
ORDER BY rank ASC
LIMIT $2`

func (s *Store) ListGlobalTop(ctx context.Context, weekStart time.Time, limit int32) ([]GlobalRankWeekly, error) {
	rows, err := s.pool.Query(ctx, sqlListGlobalTop, weekStart, limit)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := make([]GlobalRankWeekly, 0)
	for rows.Next() {
		var r GlobalRankWeekly
		if err := rows.Scan(&r.WeekStart, &r.UserID, &r.TotalScore, &r.Rank); err != nil {
			return nil, err
		}
		out = append(out, r)
	}
	return out, rows.Err()
}
