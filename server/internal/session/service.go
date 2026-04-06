package session

import (
	"context"
	"errors"

	"github.com/gensdeis/SGT/server/internal/anticheat"
	"github.com/gensdeis/SGT/server/internal/ranking"
	"github.com/gensdeis/SGT/server/internal/storage"
	"github.com/google/uuid"
)

// Service 는 세션 시작/종료 흐름을 캡슐화한다.
type Service struct {
	store        *storage.Store
	recommender  *Recommender
	validator    *anticheat.Validator
	rankingQueue *ranking.Worker
}

func NewService(store *storage.Store, rec *Recommender, val *anticheat.Validator, rq *ranking.Worker) *Service {
	return &Service{store: store, recommender: rec, validator: val, rankingQueue: rq}
}

// StartResult 는 Start 응답.
type StartResult struct {
	SessionID    uuid.UUID `json:"session_id"`
	GameIDs      []string  `json:"game_ids"`
	DDAIntensity int       `json:"dda_intensity"`
}

// Start 는 새 세션 + 추천 큐를 만든다.
func (s *Service) Start(ctx context.Context, userID uuid.UUID) (StartResult, error) {
	rec, err := s.recommender.Build(ctx, userID, 5)
	if err != nil {
		return StartResult{}, err
	}
	sess, err := s.store.CreateSession(ctx, storage.CreateSessionParams{
		UserID:             userID,
		RecommendedGameIDs: rec.GameIDs,
		DDAIntensity:       int32(rec.DDAIntensity),
	})
	if err != nil {
		return StartResult{}, err
	}
	return StartResult{
		SessionID:    sess.ID,
		GameIDs:      rec.GameIDs,
		DDAIntensity: rec.DDAIntensity,
	}, nil
}

// EndScore 는 세션 종료 시 단일 게임 점수.
type EndScore struct {
	GameID    string  `json:"game_id"`
	Score     int     `json:"score"`
	PlayTime  float64 `json:"play_time"`
	Cleared   bool    `json:"cleared"`
	Timestamp int64   `json:"timestamp"`
	Signature string  `json:"signature"`
}

// EndResult 는 End 응답.
type EndResult struct {
	Accepted []string `json:"accepted"`
	Rejected []string `json:"rejected"`
}

// End 는 세션을 종료하고 점수들을 검증·반영한다.
// 검증 실패한 점수는 거부 목록에 들어가지만 다른 점수는 정상 처리.
func (s *Service) End(ctx context.Context, userID, sessionID uuid.UUID, scores []EndScore) (EndResult, error) {
	// 세션 소유권 확인
	sess, err := s.store.GetSession(ctx, sessionID)
	if err != nil {
		return EndResult{}, err
	}
	if sess.UserID != userID {
		return EndResult{}, errors.New("session: ownership mismatch")
	}

	res := EndResult{Accepted: []string{}, Rejected: []string{}}

	for _, sc := range scores {
		sub := anticheat.Submission{
			UserID:    userID,
			GameID:    sc.GameID,
			Score:     sc.Score,
			PlayTime:  sc.PlayTime,
			Timestamp: sc.Timestamp,
			Signature: sc.Signature,
		}
		if err := s.validator.Validate(ctx, sub); err != nil {
			res.Rejected = append(res.Rejected, sc.GameID)
			continue
		}
		// session_results 즉시 기록
		_ = s.store.InsertSessionResult(ctx, storage.InsertSessionResultParams{
			SessionID:   sessionID,
			GameID:      sc.GameID,
			Score:       int32(sc.Score),
			PlayTimeSec: float32(sc.PlayTime),
			Cleared:     sc.Cleared,
		})
		// score_aggregates 는 Worker Pool 로 비동기 upsert
		s.rankingQueue.Enqueue(ranking.ScoreJob{
			UserID: userID,
			GameID: sc.GameID,
			Score:  int32(sc.Score),
		})
		res.Accepted = append(res.Accepted, sc.GameID)
	}

	_ = s.store.EndSession(ctx, sessionID, userID)
	return res, nil
}
