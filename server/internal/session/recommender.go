// Package session 은 세션 시작/종료 + 추천 큐 생성을 담당한다.
//
// 추천기 (Recommender) 는 다음 단순 규칙으로 시작:
//   - Cold start (세션 수 < 3): 인기 + DDA 중간 강도, 모든 게임에서 5개 무작위
//   - 그 외: 사용자 최근 결과로부터 DDA delta 계산 후, 같은 강도 태그 위주
//   - 항상 신규 발견 20% (랜덤 1개)
//
// 향후 Phase 2: 태그 가중치 학습 / 협업 필터링.
package session

import (
	"context"
	"math/rand"

	"github.com/gensdeis/SGT/server/internal/config"
	"github.com/gensdeis/SGT/server/internal/dda"
	"github.com/gensdeis/SGT/server/internal/storage"
	"github.com/google/uuid"
)

// Recommender 는 추천 큐를 만든다.
type Recommender struct {
	games *config.GameRegistry
	store *storage.Store
}

func NewRecommender(games *config.GameRegistry, store *storage.Store) *Recommender {
	return &Recommender{games: games, store: store}
}

// Result 는 빌드 결과.
type Result struct {
	GameIDs      []string
	DDAIntensity int
}

// Build 는 사용자에 맞는 게임 큐를 만든다. queueSize 는 보통 5.
func (r *Recommender) Build(ctx context.Context, userID uuid.UUID, queueSize int) (Result, error) {
	if queueSize <= 0 {
		queueSize = 5
	}
	all := r.games.All()
	if len(all) == 0 {
		return Result{}, nil
	}

	count, _ := r.store.CountSessionsByUser(ctx, userID)
	if count < 3 {
		// Cold start
		return Result{
			GameIDs:      pickRandom(all, queueSize),
			DDAIntensity: 0,
		}, nil
	}

	// 최근 10개 결과로 DDA delta 계산
	recent, _ := r.store.ListRecentResultsByUser(ctx, userID, 10)
	_, delta := dda.FromResults(recent)

	picks := pickRandom(all, queueSize)
	return Result{
		GameIDs:      picks,
		DDAIntensity: delta,
	}, nil
}

func pickRandom(games []config.Game, n int) []string {
	if n >= len(games) {
		out := make([]string, len(games))
		for i, g := range games {
			out[i] = g.ID
		}
		return out
	}
	// Fisher-Yates 부분 셔플
	idx := rand.Perm(len(games))
	out := make([]string, n)
	for i := 0; i < n; i++ {
		out[i] = games[idx[i]].ID
	}
	return out
}
