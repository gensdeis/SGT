package storage

import (
	"context"

	"github.com/google/uuid"
)

const sqlInsertAnalyticsEvent = `
INSERT INTO analytics_events (user_id, game_id, event_type, payload)
VALUES ($1, $2, $3, $4)`

// InsertAnalyticsEventParams 는 이벤트 1건 인자.
type InsertAnalyticsEventParams struct {
	UserID    uuid.UUID
	GameID    string
	EventType string
	Payload   []byte // 이미 json marshal 된 바이트
}

func (s *Store) InsertAnalyticsEvent(ctx context.Context, p InsertAnalyticsEventParams) error {
	_, err := s.pool.Exec(ctx, sqlInsertAnalyticsEvent, p.UserID, p.GameID, p.EventType, p.Payload)
	return err
}

// InsertAnalyticsEventBatch 는 worker 가 호출. 단일 트랜잭션으로 묶는다.
func (s *Store) InsertAnalyticsEventBatch(ctx context.Context, batch []InsertAnalyticsEventParams) error {
	if len(batch) == 0 {
		return nil
	}
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return err
	}
	defer tx.Rollback(ctx)
	for _, p := range batch {
		if _, err := tx.Exec(ctx, sqlInsertAnalyticsEvent, p.UserID, p.GameID, p.EventType, p.Payload); err != nil {
			return err
		}
	}
	return tx.Commit(ctx)
}

const sqlListEventsByUser = `
SELECT id, user_id, game_id, event_type, payload, created_at
FROM analytics_events
WHERE user_id = $1
ORDER BY created_at DESC
LIMIT $2`

func (s *Store) ListEventsByUser(ctx context.Context, userID uuid.UUID, limit int32) ([]AnalyticsEvent, error) {
	rows, err := s.pool.Query(ctx, sqlListEventsByUser, userID, limit)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := make([]AnalyticsEvent, 0)
	for rows.Next() {
		var e AnalyticsEvent
		if err := rows.Scan(&e.ID, &e.UserID, &e.GameID, &e.EventType, &e.Payload, &e.CreatedAt); err != nil {
			return nil, err
		}
		out = append(out, e)
	}
	return out, rows.Err()
}
