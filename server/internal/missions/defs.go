// Package missions 는 데일리 미션 정의 + 진행도/보상.
package missions

import "time"

// Definition 은 단일 미션 메타.
type Definition struct {
	ID     string
	Title  string
	Target int32
	Reward int32 // 코인
}

// All 은 매일 자동 발급되는 미션 목록 (코드 상수 — Iter 3 MVP).
var All = []Definition{
	{ID: "play_3_games", Title: "게임 3판 플레이", Target: 3, Reward: 10},
	{ID: "clear_1_game", Title: "게임 1판 클리어", Target: 1, Reward: 15},
	{ID: "play_total_60s", Title: "총 60초 플레이", Target: 60, Reward: 5},
}

// FindByID 는 ID 로 미션 정의 조회.
func FindByID(id string) (Definition, bool) {
	for _, d := range All {
		if d.ID == id {
			return d, true
		}
	}
	return Definition{}, false
}

// DayKeyUTC 는 오늘 (UTC) 의 YYYYMMDD 키.
func DayKeyUTC(now time.Time) string {
	return now.UTC().Format("20060102")
}
