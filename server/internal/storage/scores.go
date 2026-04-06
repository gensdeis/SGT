package storage

import (
	"context"
	"time"

	"github.com/gensdeis/SGT/server/internal/storage/sqlc"
	"github.com/google/uuid"
)

func (s *Store) UpsertBestScore(ctx context.Context, gameID string, userID uuid.UUID, score int32) error {
	return s.q.UpsertBestScore(ctx, sqlc.UpsertBestScoreParams{
		GameID:    gameID,
		UserID:    userID,
		BestScore: score,
	})
}

func (s *Store) ListTopByGame(ctx context.Context, gameID string, limit int32) ([]ScoreAggregate, error) {
	rows, err := s.q.ListTopByGame(ctx, sqlc.ListTopByGameParams{GameID: gameID, Limit: limit})
	if err != nil {
		return nil, err
	}
	out := make([]ScoreAggregate, len(rows))
	for i, r := range rows {
		out[i] = ScoreAggregate{
			GameID:    r.GameID,
			UserID:    r.UserID,
			BestScore: r.BestScore,
			UpdatedAt: r.UpdatedAt,
		}
	}
	return out, nil
}

func (s *Store) GetUserBest(ctx context.Context, gameID string, userID uuid.UUID) (ScoreAggregate, error) {
	a, err := s.q.GetUserBest(ctx, sqlc.GetUserBestParams{GameID: gameID, UserID: userID})
	if err != nil {
		return ScoreAggregate{}, wrapNoRows(err)
	}
	return ScoreAggregate{
		GameID:    a.GameID,
		UserID:    a.UserID,
		BestScore: a.BestScore,
		UpdatedAt: a.UpdatedAt,
	}, nil
}

func (s *Store) UpsertWeeklyRank(ctx context.Context, weekStart time.Time, userID uuid.UUID, totalScore int64, rank int32) error {
	return s.q.UpsertWeeklyRank(ctx, sqlc.UpsertWeeklyRankParams{
		WeekStart:  weekStart,
		UserID:     userID,
		TotalScore: totalScore,
		Rank:       rank,
	})
}

func (s *Store) ListGlobalTop(ctx context.Context, weekStart time.Time, limit int32) ([]GlobalRankWeekly, error) {
	rows, err := s.q.ListGlobalTop(ctx, sqlc.ListGlobalTopParams{WeekStart: weekStart, Limit: limit})
	if err != nil {
		return nil, err
	}
	out := make([]GlobalRankWeekly, len(rows))
	for i, r := range rows {
		out[i] = GlobalRankWeekly{
			WeekStart:  r.WeekStart,
			UserID:     r.UserID,
			TotalScore: r.TotalScore,
			Rank:       r.Rank,
		}
	}
	return out, nil
}
