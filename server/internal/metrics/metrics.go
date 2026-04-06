// Package metrics 는 Fiber Prometheus 미들웨어가 잡지 못하는 커스텀 게이지를 정의한다.
//
// pgxpool 게이지는 pkg/db 패키지에서, http 메트릭은 fiberprometheus 가 자동 등록.
package metrics

import (
	"github.com/prometheus/client_golang/prometheus"
	"github.com/prometheus/client_golang/prometheus/promauto"
)

// AnalyticsQueueDepth 는 Analytics Worker 큐 적체.
var AnalyticsQueueDepth = promauto.NewGauge(prometheus.GaugeOpts{
	Name: "analytics_queue_depth",
	Help: "현재 Analytics Worker 큐에 쌓인 이벤트 수",
})

// RankingQueueDepth 는 Ranking Worker 큐 적체.
var RankingQueueDepth = promauto.NewGauge(prometheus.GaugeOpts{
	Name: "ranking_queue_depth",
	Help: "현재 Ranking Worker 큐에 쌓인 작업 수",
})

// AnticheatRejectsTotal 는 검증 거부 누적.
var AnticheatRejectsTotal = promauto.NewCounterVec(prometheus.CounterOpts{
	Name: "anticheat_rejects_total",
	Help: "anti-cheat 검증 거부 누적 (사유별)",
}, []string{"reason"})
