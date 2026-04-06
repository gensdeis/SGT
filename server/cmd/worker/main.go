// Command worker 는 주기적 배치 잡 (랭킹 리셋, 시즌 마감 등) 진입점.
//
// Phase 1: 스켈레톤만. 주간/월간 cron-like ticker 로 향후 채움.
package main

import (
	"context"
	"log/slog"
	"os"
	"os/signal"
	"syscall"
	"time"
)

func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, nil))
	slog.SetDefault(logger)

	slog.Info("worker started (phase 1 skeleton)")

	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	go func() {
		t := time.NewTicker(time.Hour)
		defer t.Stop()
		for {
			select {
			case <-ctx.Done():
				return
			case now := <-t.C:
				// TODO: 월요일 0시면 weekly_rankings rollover
				// TODO: 매월 1일이면 monthly_rankings rollover
				slog.Debug("worker tick", "now", now)
			}
		}
	}()

	quit := make(chan os.Signal, 1)
	signal.Notify(quit, syscall.SIGINT, syscall.SIGTERM)
	<-quit
	slog.Info("worker shutdown")
}
