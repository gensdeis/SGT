// Package db 는 pgxpool 초기화 + 메트릭 노출을 담당한다.
package db

import (
	"context"
	"fmt"
	"time"

	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/prometheus/client_golang/prometheus"
	"github.com/prometheus/client_golang/prometheus/promauto"
)

// PoolConfig 는 pgxpool 설정.
type PoolConfig struct {
	DatabaseURL string
	MaxConns    int32
	MinConns    int32
}

var dbPoolAcquired = promauto.NewGauge(prometheus.GaugeOpts{
	Name: "db_pool_acquired_conns",
	Help: "현재 pgxpool 에서 사용 중인 연결 수",
})

var dbPoolTotal = promauto.NewGauge(prometheus.GaugeOpts{
	Name: "db_pool_total_conns",
	Help: "현재 pgxpool 의 전체 연결 수 (idle + acquired)",
})

// NewPool 은 pgxpool 을 생성하고 ping 으로 검증한다.
// MaxConns 는 BACKEND_PLAN.md §"DB Connection Pool" 의 Phase 별 권장값 참조.
func NewPool(ctx context.Context, cfg PoolConfig) (*pgxpool.Pool, error) {
	poolCfg, err := pgxpool.ParseConfig(cfg.DatabaseURL)
	if err != nil {
		return nil, fmt.Errorf("parse database url: %w", err)
	}
	if cfg.MaxConns > 0 {
		poolCfg.MaxConns = cfg.MaxConns
	}
	if cfg.MinConns > 0 {
		poolCfg.MinConns = cfg.MinConns
	}
	poolCfg.MaxConnLifetime = 30 * time.Minute
	poolCfg.MaxConnIdleTime = 5 * time.Minute
	poolCfg.HealthCheckPeriod = 1 * time.Minute

	pool, err := pgxpool.NewWithConfig(ctx, poolCfg)
	if err != nil {
		return nil, fmt.Errorf("create pgxpool: %w", err)
	}

	pingCtx, cancel := context.WithTimeout(ctx, 5*time.Second)
	defer cancel()
	if err := pool.Ping(pingCtx); err != nil {
		pool.Close()
		return nil, fmt.Errorf("db ping failed: %w", err)
	}

	return pool, nil
}

// StartMetricsLoop 는 주기적으로 풀 통계를 Prometheus Gauge 에 갱신한다.
// ctx 가 종료되면 루프를 빠져나간다. 호출자가 별도 고루틴으로 띄운다.
func StartMetricsLoop(ctx context.Context, pool *pgxpool.Pool, interval time.Duration) {
	t := time.NewTicker(interval)
	defer t.Stop()
	for {
		select {
		case <-ctx.Done():
			return
		case <-t.C:
			s := pool.Stat()
			dbPoolAcquired.Set(float64(s.AcquiredConns()))
			dbPoolTotal.Set(float64(s.TotalConns()))
		}
	}
}
