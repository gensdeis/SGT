// Package dda 는 다이나믹 난이도 조절 (Dynamic Difficulty Adjustment) 로직.
//
// 설계 근거: Docs/PROJECT_PLAN.md §"DDA"
//
//	Success Rate (SR) = 최근 10게임 클리어 수 / 10
//	SR > 0.8  → 강도 +1
//	SR < 0.4  → 강도 -1
//	otherwise → 0 (유지)
//	조절폭은 ±1단계로 제한 (급격한 변화 방지)
package dda

import "github.com/gensdeis/SGT/server/internal/storage"

// Calculate 는 SR 값에 대한 강도 변화량을 반환한다 (-1, 0, +1).
func Calculate(sr float64) int {
	switch {
	case sr > 0.8:
		return +1
	case sr < 0.4:
		return -1
	default:
		return 0
	}
}

// FromResults 는 최근 결과 목록에서 SR 을 계산해 강도 변화량을 반환한다.
// 결과가 0개면 0 (중간 강도) 반환.
func FromResults(results []storage.SessionResult) (sr float64, delta int) {
	if len(results) == 0 {
		return 0.5, 0
	}
	cleared := 0
	for _, r := range results {
		if r.Cleared {
			cleared++
		}
	}
	sr = float64(cleared) / float64(len(results))
	delta = Calculate(sr)
	return sr, delta
}
