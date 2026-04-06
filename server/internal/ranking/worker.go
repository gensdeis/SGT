package ranking

import (
	"context"
	"log/slog"
	"sync"
	"time"

	"github.com/gensdeis/SGT/server/internal/metrics"
	"github.com/gensdeis/SGT/server/internal/storage"
	"github.com/google/uuid"
)

// ScoreJob 은 Worker 가 처리할 점수 반영 작업.
type ScoreJob struct {
	UserID uuid.UUID
	GameID string
	Score  int32
}

// Worker 는 Worker Pool 패턴. 큐가 가득 차면 드롭 (게임 진행을 막지 않음).
type Worker struct {
	jobs    chan ScoreJob
	store   *storage.Store
	wg      sync.WaitGroup
	workers int
}

// NewWorker 는 워커 풀을 만든다. 시작은 Run 호출.
func NewWorker(store *storage.Store, bufferSize, workerCount int) *Worker {
	return &Worker{
		jobs:    make(chan ScoreJob, bufferSize),
		store:   store,
		workers: workerCount,
	}
}

// Start 는 백그라운드 워커들을 띄운다.
func (w *Worker) Start(ctx context.Context) {
	for i := 0; i < w.workers; i++ {
		w.wg.Add(1)
		go w.loop(ctx)
	}
	// 메트릭 갱신 루프
	w.wg.Add(1)
	go w.metricsLoop(ctx)
}

// Enqueue 는 작업을 큐에 넣는다. 큐 가득 시 드롭.
func (w *Worker) Enqueue(job ScoreJob) {
	select {
	case w.jobs <- job:
	default:
		slog.Warn("ranking queue full, score job dropped",
			"user_id", job.UserID, "game_id", job.GameID, "score", job.Score)
	}
}

// Shutdown 은 큐를 닫고 워커들이 잔여 작업을 처리할 때까지 대기한다.
func (w *Worker) Shutdown(ctx context.Context) {
	close(w.jobs)
	done := make(chan struct{})
	go func() { w.wg.Wait(); close(done) }()
	select {
	case <-done:
	case <-ctx.Done():
		slog.Warn("ranking worker shutdown timeout")
	}
}

func (w *Worker) loop(ctx context.Context) {
	defer w.wg.Done()
	for job := range w.jobs {
		w.processWithRetry(ctx, job)
	}
}

func (w *Worker) processWithRetry(ctx context.Context, job ScoreJob) {
	backoff := 100 * time.Millisecond
	for attempt := 0; attempt < 3; attempt++ {
		err := w.store.UpsertBestScore(ctx, job.GameID, job.UserID, job.Score)
		if err == nil {
			return
		}
		slog.Warn("ranking upsert failed, retry",
			"attempt", attempt+1, "error", err, "user_id", job.UserID, "game_id", job.GameID)
		select {
		case <-ctx.Done():
			return
		case <-time.After(backoff):
		}
		backoff *= 2
	}
	slog.Error("ranking upsert exhausted retries",
		"user_id", job.UserID, "game_id", job.GameID, "score", job.Score)
}

func (w *Worker) metricsLoop(ctx context.Context) {
	defer w.wg.Done()
	t := time.NewTicker(time.Second)
	defer t.Stop()
	for {
		select {
		case <-ctx.Done():
			return
		case <-t.C:
			metrics.RankingQueueDepth.Set(float64(len(w.jobs)))
		}
	}
}
