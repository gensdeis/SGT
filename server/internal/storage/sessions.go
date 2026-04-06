package storage

import (
	"context"

	"github.com/google/uuid"
)

const sqlCreateSession = `
INSERT INTO sessions (user_id, recommended_game_ids, dda_intensity)
VALUES ($1, $2, $3)
RETURNING id, user_id, started_at, ended_at, recommended_game_ids, dda_intensity`

// CreateSessionParams 는 새 세션 생성 인자.
type CreateSessionParams struct {
	UserID             uuid.UUID
	RecommendedGameIDs []string
	DDAIntensity       int32
}

func (s *Store) CreateSession(ctx context.Context, p CreateSessionParams) (Session, error) {
	var sess Session
	err := s.pool.QueryRow(ctx, sqlCreateSession, p.UserID, p.RecommendedGameIDs, p.DDAIntensity).Scan(
		&sess.ID, &sess.UserID, &sess.StartedAt, &sess.EndedAt, &sess.RecommendedGameIDs, &sess.DDAIntensity)
	return sess, err
}

const sqlGetSession = `
SELECT id, user_id, started_at, ended_at, recommended_game_ids, dda_intensity
FROM sessions WHERE id = $1`

func (s *Store) GetSession(ctx context.Context, id uuid.UUID) (Session, error) {
	var sess Session
	err := s.pool.QueryRow(ctx, sqlGetSession, id).Scan(
		&sess.ID, &sess.UserID, &sess.StartedAt, &sess.EndedAt, &sess.RecommendedGameIDs, &sess.DDAIntensity)
	return sess, wrapNoRows(err)
}

const sqlEndSession = `UPDATE sessions SET ended_at = now() WHERE id = $1 AND user_id = $2`

func (s *Store) EndSession(ctx context.Context, id, userID uuid.UUID) error {
	_, err := s.pool.Exec(ctx, sqlEndSession, id, userID)
	return err
}

const sqlCountSessionsByUser = `SELECT COUNT(*) FROM sessions WHERE user_id = $1`

func (s *Store) CountSessionsByUser(ctx context.Context, userID uuid.UUID) (int64, error) {
	var n int64
	err := s.pool.QueryRow(ctx, sqlCountSessionsByUser, userID).Scan(&n)
	return n, err
}

const sqlInsertSessionResult = `
INSERT INTO session_results (session_id, game_id, score, play_time_sec, cleared)
VALUES ($1, $2, $3, $4, $5)
ON CONFLICT (session_id, game_id) DO UPDATE SET
    score = EXCLUDED.score,
    play_time_sec = EXCLUDED.play_time_sec,
    cleared = EXCLUDED.cleared,
    submitted_at = now()`

// InsertSessionResultParams 는 점수 기록 인자.
type InsertSessionResultParams struct {
	SessionID   uuid.UUID
	GameID      string
	Score       int32
	PlayTimeSec float32
	Cleared     bool
}

func (s *Store) InsertSessionResult(ctx context.Context, p InsertSessionResultParams) error {
	_, err := s.pool.Exec(ctx, sqlInsertSessionResult, p.SessionID, p.GameID, p.Score, p.PlayTimeSec, p.Cleared)
	return err
}

const sqlListResultsBySession = `
SELECT session_id, game_id, score, play_time_sec, cleared, submitted_at
FROM session_results WHERE session_id = $1`

func (s *Store) ListResultsBySession(ctx context.Context, sessionID uuid.UUID) ([]SessionResult, error) {
	rows, err := s.pool.Query(ctx, sqlListResultsBySession, sessionID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := make([]SessionResult, 0)
	for rows.Next() {
		var r SessionResult
		if err := rows.Scan(&r.SessionID, &r.GameID, &r.Score, &r.PlayTimeSec, &r.Cleared, &r.SubmittedAt); err != nil {
			return nil, err
		}
		out = append(out, r)
	}
	return out, rows.Err()
}

const sqlListRecentResultsByUser = `
SELECT sr.session_id, sr.game_id, sr.score, sr.play_time_sec, sr.cleared, sr.submitted_at
FROM session_results sr
JOIN sessions s ON sr.session_id = s.id
WHERE s.user_id = $1
ORDER BY sr.submitted_at DESC
LIMIT $2`

func (s *Store) ListRecentResultsByUser(ctx context.Context, userID uuid.UUID, limit int32) ([]SessionResult, error) {
	rows, err := s.pool.Query(ctx, sqlListRecentResultsByUser, userID, limit)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := make([]SessionResult, 0)
	for rows.Next() {
		var r SessionResult
		if err := rows.Scan(&r.SessionID, &r.GameID, &r.Score, &r.PlayTimeSec, &r.Cleared, &r.SubmittedAt); err != nil {
			return nil, err
		}
		out = append(out, r)
	}
	return out, rows.Err()
}
