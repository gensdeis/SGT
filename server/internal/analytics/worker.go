// Package analytics 는 플레이 이벤트 수집 + Worker Pool 배치 적재.
package analytics

import (
	"context"
	"log/slog"
	"sync"
	"time"

	"github.com/gensdeis/SGT/server/internal/metrics"
	"github.com/gensdeis/SGT/server/internal/storage"
)

// Event 는 큐 단위 이벤트.
type Event = storage.InsertAnalyticsEventParams

// Worker 는 1000 버퍼, 5 worker, 5초 또는 100건 마다 flush.
type Worker struct {
	jobs       chan Event
	store      *storage.Store
	wg         sync.WaitGroup
	workers    int
	batchSize  int
	flushEvery time.Duration
}

// NewWorker 는 워커를 만든다.
func NewWorker(store *storage.Store, bufferSize, workerCount, batchSize int, flushEvery time.Duration) *Worker {
	return &Worker{
		jobs:       make(chan Event, bufferSize),
		store:      store,
		workers:    workerCount,
		batchSize:  batchSize,
		flushEvery: flushEvery,
	}
}

// Start 는 백그라운드 워커들을 띄운다.
func (w *Worker) Start(ctx context.Context) {
	for i := 0; i < w.workers; i++ {
		w.wg.Add(1)
		go w.loop(ctx)
	}
	w.wg.Add(1)
	go w.metricsLoop(ctx)
}

// Enqueue 는 이벤트 1건을 큐에 넣는다. 큐 가득 시 드롭.
func (w *Worker) Enqueue(e Event) {
	select {
	case w.jobs <- e:
	default:
		slog.Warn("analytics queue full, event dropped",
			"user_id", e.UserID, "game_id", e.GameID, "event_type", e.EventType)
	}
}

// Shutdown 은 큐를 닫고 워커들이 잔여를 처리할 때까지 대기.
func (w *Worker) Shutdown(ctx context.Context) {
	close(w.jobs)
	done := make(chan struct{})
	go func() { w.wg.Wait(); close(done) }()
	select {
	case <-done:
	case <-ctx.Done():
		slog.Warn("analytics worker shutdown timeout")
	}
}

func (w *Worker) loop(ctx context.Context) {
	defer w.wg.Done()
	batch := make([]Event, 0, w.batchSize)
	timer := time.NewTimer(w.flushEvery)
	defer timer.Stop()

	flush := func() {
		if len(batch) == 0 {
			return
		}
		if err := w.store.InsertAnalyticsEventBatch(ctx, batch); err != nil {
			slog.Error("analytics batch insert failed", "size", len(batch), "error", err)
		}
		batch = batch[:0]
	}

	for {
		select {
		case e, ok := <-w.jobs:
			if !ok {
				flush()
				return
			}
			batch = append(batch, e)
			if len(batch) >= w.batchSize {
				flush()
				if !timer.Stop() {
					select {
					case <-timer.C:
					default:
					}
				}
				timer.Reset(w.flushEvery)
			}
		case <-timer.C:
			flush()
			timer.Reset(w.flushEvery)
		case <-ctx.Done():
			flush()
			return
		}
	}
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
			metrics.AnalyticsQueueDepth.Set(float64(len(w.jobs)))
		}
	}
}
