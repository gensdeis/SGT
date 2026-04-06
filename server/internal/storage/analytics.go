package storage

import (
	"context"

	"github.com/gensdeis/SGT/server/internal/storage/sqlc"
	"github.com/google/uuid"
)

// InsertAnalyticsEventParams 는 이벤트 1건 인자.
type InsertAnalyticsEventParams struct {
	UserID    uuid.UUID
	GameID    string
	EventType string
	Payload   []byte // 이미 json marshal 된 바이트
}

func (s *Store) InsertAnalyticsEvent(ctx context.Context, p InsertAnalyticsEventParams) error {
	return s.q.InsertAnalyticsEvent(ctx, sqlc.InsertAnalyticsEventParams{
		UserID:    p.UserID,
		GameID:    p.GameID,
		EventType: p.EventType,
		Payload:   p.Payload,
	})
}

// InsertAnalyticsEventBatch 는 worker 가 호출. 단일 트랜잭션으로 묶는다.
// sqlc 는 transaction 자동 wrap 을 제공하지 않으므로 수동으로 tx 를 만들어 sqlc Queries 를 WithTx 한다.
func (s *Store) InsertAnalyticsEventBatch(ctx context.Context, batch []InsertAnalyticsEventParams) error {
	if len(batch) == 0 {
		return nil
	}
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return err
	}
	defer tx.Rollback(ctx)
	q := s.q.WithTx(tx)
	for _, p := range batch {
		if err := q.InsertAnalyticsEvent(ctx, sqlc.InsertAnalyticsEventParams{
			UserID:    p.UserID,
			GameID:    p.GameID,
			EventType: p.EventType,
			Payload:   p.Payload,
		}); err != nil {
			return err
		}
	}
	return tx.Commit(ctx)
}

func (s *Store) ListEventsByUser(ctx context.Context, userID uuid.UUID, limit int32) ([]AnalyticsEvent, error) {
	rows, err := s.q.ListEventsByUser(ctx, sqlc.ListEventsByUserParams{UserID: userID, Limit: limit})
	if err != nil {
		return nil, err
	}
	out := make([]AnalyticsEvent, len(rows))
	for i, r := range rows {
		out[i] = AnalyticsEvent{
			ID:        r.ID,
			UserID:    r.UserID,
			GameID:    r.GameID,
			EventType: r.EventType,
			Payload:   r.Payload,
			CreatedAt: r.CreatedAt,
		}
	}
	return out, nil
}
