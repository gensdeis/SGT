// Package game 은 Game Registry API 의 비즈니스 로직.
//
// games.yaml (메모리 캐시 GameRegistry) 가 메타 데이터의 source of truth 다.
// DB 의 games 테이블은 번들 URL/버전/해시 같은 동적 정보를 보관한다.
// 두 출처를 join 해서 응답한다.
package game

import (
	"context"
	"errors"

	"github.com/gensdeis/SGT/server/internal/config"
	"github.com/gensdeis/SGT/server/internal/storage"
)

// ErrNotFound 는 게임을 찾지 못한 경우.
var ErrNotFound = errors.New("game: not found")

// View 는 클라이언트 응답 모델 (yaml + db join 결과).
type View struct {
	ID              string   `json:"id"`
	Title           string   `json:"title"`
	CreatorID       string   `json:"creator_id"`
	TimeLimitSec    int      `json:"time_limit_sec"`
	Tags            []string `json:"tags"`
	BundleURL       string   `json:"bundle_url"`
	BundleVersion   string   `json:"bundle_version"`
	BundleHash      string   `json:"bundle_hash"`
}

// Service 는 yaml 레지스트리 + DB 를 합친다.
type Service struct {
	registry *config.GameRegistry
	store    *storage.Store
}

func NewService(reg *config.GameRegistry, store *storage.Store) *Service {
	return &Service{registry: reg, store: store}
}

// viewFromConfig 는 yaml Game 을 View 로 변환 (bundle 필드 포함, Iter 2C''').
func viewFromConfig(g config.Game) View {
	return View{
		ID:            g.ID,
		Title:         g.Title,
		CreatorID:     g.CreatorID,
		TimeLimitSec:  g.TimeLimitSec,
		Tags:          g.Tags,
		BundleURL:     g.BundleURL,
		BundleVersion: g.BundleVersion,
		BundleHash:    g.BundleHash,
	}
}

// List 는 yaml 의 모든 게임을 반환한다. tags 가 있으면 필터링.
// 우선순위: DB 에 채워진 bundle 메타가 있으면 그쪽, 없으면 yaml 값.
func (s *Service) List(ctx context.Context, tags []string) ([]View, error) {
	games := s.registry.FilterByTags(tags)
	out := make([]View, 0, len(games))
	for _, g := range games {
		v := viewFromConfig(g)
		if dbg, err := s.store.GetGameByID(ctx, g.ID); err == nil && dbg.BundleURL != "" {
			v.BundleURL = dbg.BundleURL
			v.BundleVersion = dbg.BundleVersion
			v.BundleHash = dbg.BundleHash
		}
		out = append(out, v)
	}
	return out, nil
}

// Get 은 단일 게임 상세를 반환한다.
func (s *Service) Get(ctx context.Context, id string) (View, error) {
	g, ok := s.registry.Get(id)
	if !ok {
		return View{}, ErrNotFound
	}
	v := viewFromConfig(g)
	if dbg, err := s.store.GetGameByID(ctx, g.ID); err == nil && dbg.BundleURL != "" {
		v.BundleURL = dbg.BundleURL
		v.BundleVersion = dbg.BundleVersion
		v.BundleHash = dbg.BundleHash
	}
	return v, nil
}
