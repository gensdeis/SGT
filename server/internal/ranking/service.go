// Package ranking 은 게임별/글로벌 랭킹 조회 + Worker Pool 기반 점수 반영을 담당한다.
package ranking

import (
	"context"
	"time"

	"github.com/gensdeis/SGT/server/internal/storage"
	"github.com/google/uuid"
)

// Entry 는 응답 항목.
type Entry struct {
	UserID    uuid.UUID `json:"user_id"`
	BestScore int32     `json:"best_score"`
	Rank      int       `json:"rank"`
	UpdatedAt time.Time `json:"updated_at,omitempty"`
}

// GlobalEntry 는 글로벌 주간 랭킹 항목.
type GlobalEntry struct {
	UserID     uuid.UUID `json:"user_id"`
	TotalScore int64     `json:"total_score"`
	Rank       int32     `json:"rank"`
	WeekStart  time.Time `json:"week_start"`
}

// Service 는 조회 API.
type Service struct {
	store *storage.Store
}

func NewService(store *storage.Store) *Service {
	return &Service{store: store}
}

// TopByGame 은 단일 게임의 best_score 상위 limit 명을 반환한다.
func (s *Service) TopByGame(ctx context.Context, gameID string, limit int) ([]Entry, error) {
	rows, err := s.store.ListTopByGame(ctx, gameID, int32(limit))
	if err != nil {
		return nil, err
	}
	out := make([]Entry, 0, len(rows))
	for i, r := range rows {
		out = append(out, Entry{
			UserID:    r.UserID,
			BestScore: r.BestScore,
			Rank:      i + 1,
			UpdatedAt: r.UpdatedAt,
		})
	}
	return out, nil
}

// GlobalTop 은 주간 글로벌 랭킹 상위 limit 명.
// week 가 zero 면 이번 주 월요일을 사용.
func (s *Service) GlobalTop(ctx context.Context, week time.Time, limit int) ([]GlobalEntry, error) {
	if week.IsZero() {
		week = mondayOf(time.Now())
	}
	rows, err := s.store.ListGlobalTop(ctx, week, int32(limit))
	if err != nil {
		return nil, err
	}
	out := make([]GlobalEntry, 0, len(rows))
	for _, r := range rows {
		out = append(out, GlobalEntry{
			UserID:     r.UserID,
			TotalScore: r.TotalScore,
			Rank:       r.Rank,
			WeekStart:  r.WeekStart,
		})
	}
	return out, nil
}

func mondayOf(t time.Time) time.Time {
	t = t.UTC()
	wd := int(t.Weekday())
	if wd == 0 {
		wd = 7
	}
	monday := t.AddDate(0, 0, -(wd - 1))
	return time.Date(monday.Year(), monday.Month(), monday.Day(), 0, 0, 0, 0, time.UTC)
}
