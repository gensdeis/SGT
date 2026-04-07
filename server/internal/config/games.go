package config

import (
	"fmt"
	"os"
	"sync"

	"gopkg.in/yaml.v3"
)

// Game 은 config/games.yaml 의 단일 게임 정의.
type Game struct {
	ID              string   `yaml:"id"`
	Title           string   `yaml:"title"`
	CreatorID       string   `yaml:"creator_id"`
	TimeLimitSec    int      `yaml:"time_limit_sec"`
	MinPlaySeconds  int      `yaml:"min_play_seconds"`
	MaxScore        int      `yaml:"max_score"`
	RateLimitPerMin int      `yaml:"rate_limit_per_min"`
	Tags            []string `yaml:"tags"`

	// Iter 2C''': Addressables remote bundle 메타.
	// DB 에 행이 있으면 그쪽 우선, 없으면 yaml 값을 클라이언트 응답에 사용.
	BundleURL     string `yaml:"bundle_url,omitempty"`
	BundleVersion string `yaml:"bundle_version,omitempty"`
	BundleHash    string `yaml:"bundle_hash,omitempty"`
}

// GameRegistry 는 메모리 캐시. 시작 시 1회 로드 후 read-only.
// 새 게임 추가 시 yaml 만 수정 후 서버 재시작.
type GameRegistry struct {
	mu    sync.RWMutex
	byID  map[string]Game
	all   []Game
}

type gamesFile struct {
	Games []Game `yaml:"games"`
}

// LoadGames 는 yaml 파일을 읽어 GameRegistry 를 반환한다.
func LoadGames(path string) (*GameRegistry, error) {
	data, err := os.ReadFile(path)
	if err != nil {
		return nil, fmt.Errorf("read games yaml: %w", err)
	}
	var f gamesFile
	if err := yaml.Unmarshal(data, &f); err != nil {
		return nil, fmt.Errorf("parse games yaml: %w", err)
	}
	if len(f.Games) == 0 {
		return nil, fmt.Errorf("games yaml is empty: %s", path)
	}
	r := &GameRegistry{
		byID: make(map[string]Game, len(f.Games)),
		all:  make([]Game, 0, len(f.Games)),
	}
	for _, g := range f.Games {
		if g.ID == "" {
			return nil, fmt.Errorf("game with empty id in %s", path)
		}
		if _, dup := r.byID[g.ID]; dup {
			return nil, fmt.Errorf("duplicate game id: %s", g.ID)
		}
		r.byID[g.ID] = g
		r.all = append(r.all, g)
	}
	return r, nil
}

// Get 는 ID 로 게임을 조회한다.
func (r *GameRegistry) Get(id string) (Game, bool) {
	r.mu.RLock()
	defer r.mu.RUnlock()
	g, ok := r.byID[id]
	return g, ok
}

// All 은 모든 게임 목록을 반환한다.
func (r *GameRegistry) All() []Game {
	r.mu.RLock()
	defer r.mu.RUnlock()
	out := make([]Game, len(r.all))
	copy(out, r.all)
	return out
}

// FilterByTags 는 주어진 태그 중 하나라도 매칭되는 게임을 반환한다 (OR).
// tags 가 비어 있으면 전체 반환.
func (r *GameRegistry) FilterByTags(tags []string) []Game {
	if len(tags) == 0 {
		return r.All()
	}
	want := make(map[string]struct{}, len(tags))
	for _, t := range tags {
		want[t] = struct{}{}
	}
	r.mu.RLock()
	defer r.mu.RUnlock()
	out := make([]Game, 0)
	for _, g := range r.all {
		for _, t := range g.Tags {
			if _, ok := want[t]; ok {
				out = append(out, g)
				break
			}
		}
	}
	return out
}
