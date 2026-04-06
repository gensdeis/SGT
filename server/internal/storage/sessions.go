package storage

import (
	"context"

	"github.com/gensdeis/SGT/server/internal/storage/sqlc"
	"github.com/google/uuid"
)

// CreateSessionParams 는 새 세션 생성 인자.
type CreateSessionParams struct {
	UserID             uuid.UUID
	RecommendedGameIDs []string
	DDAIntensity       int32
}

func (s *Store) CreateSession(ctx context.Context, p CreateSessionParams) (Session, error) {
	sess, err := s.q.CreateSession(ctx, sqlc.CreateSessionParams{
		UserID:             p.UserID,
		RecommendedGameIds: p.RecommendedGameIDs,
		DdaIntensity:       p.DDAIntensity,
	})
	if err != nil {
		return Session{}, err
	}
	return convertSession(sess), nil
}

func (s *Store) GetSession(ctx context.Context, id uuid.UUID) (Session, error) {
	sess, err := s.q.GetSession(ctx, id)
	if err != nil {
		return Session{}, wrapNoRows(err)
	}
	return convertSession(sess), nil
}

func (s *Store) EndSession(ctx context.Context, id, userID uuid.UUID) error {
	return s.q.EndSession(ctx, sqlc.EndSessionParams{ID: id, UserID: userID})
}

func (s *Store) CountSessionsByUser(ctx context.Context, userID uuid.UUID) (int64, error) {
	return s.q.CountSessionsByUser(ctx, userID)
}

// InsertSessionResultParams 는 점수 기록 인자.
type InsertSessionResultParams struct {
	SessionID   uuid.UUID
	GameID      string
	Score       int32
	PlayTimeSec float32
	Cleared     bool
}

func (s *Store) InsertSessionResult(ctx context.Context, p InsertSessionResultParams) error {
	return s.q.InsertSessionResult(ctx, sqlc.InsertSessionResultParams{
		SessionID:   p.SessionID,
		GameID:      p.GameID,
		Score:       p.Score,
		PlayTimeSec: p.PlayTimeSec,
		Cleared:     p.Cleared,
	})
}

func (s *Store) ListResultsBySession(ctx context.Context, sessionID uuid.UUID) ([]SessionResult, error) {
	rows, err := s.q.ListResultsBySession(ctx, sessionID)
	if err != nil {
		return nil, err
	}
	out := make([]SessionResult, len(rows))
	for i, r := range rows {
		out[i] = convertSessionResult(r)
	}
	return out, nil
}

func (s *Store) ListRecentResultsByUser(ctx context.Context, userID uuid.UUID, limit int32) ([]SessionResult, error) {
	rows, err := s.q.ListRecentResultsByUser(ctx, sqlc.ListRecentResultsByUserParams{
		UserID: userID,
		Limit:  limit,
	})
	if err != nil {
		return nil, err
	}
	out := make([]SessionResult, len(rows))
	for i, r := range rows {
		out[i] = convertSessionResult(r)
	}
	return out, nil
}

func convertSession(s sqlc.Session) Session {
	return Session{
		ID:                 s.ID,
		UserID:             s.UserID,
		StartedAt:          s.StartedAt,
		EndedAt:            s.EndedAt,
		RecommendedGameIDs: s.RecommendedGameIds,
		DDAIntensity:       s.DdaIntensity,
	}
}

func convertSessionResult(r sqlc.SessionResult) SessionResult {
	return SessionResult{
		SessionID:   r.SessionID,
		GameID:      r.GameID,
		Score:       r.Score,
		PlayTimeSec: r.PlayTimeSec,
		Cleared:     r.Cleared,
		SubmittedAt: r.SubmittedAt,
	}
}
