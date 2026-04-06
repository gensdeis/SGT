package analytics

import (
	"sync"
	"testing"

	"github.com/google/uuid"
)

func TestWorker_DropOnFull(t *testing.T) {
	// 직접 jobs 채널을 만들어서 Enqueue 만 검증 (DB 호출 없이)
	w := &Worker{jobs: make(chan Event, 2)}
	w.Enqueue(Event{UserID: uuid.New(), GameID: "g", EventType: "x"})
	w.Enqueue(Event{UserID: uuid.New(), GameID: "g", EventType: "x"})
	// 3번째는 드롭되어야 함 (panic 없이)
	w.Enqueue(Event{UserID: uuid.New(), GameID: "g", EventType: "x"})
	if len(w.jobs) != 2 {
		t.Fatalf("expected len=2 (drop happened), got %d", len(w.jobs))
	}
}

func TestWorker_EnqueueParallel(t *testing.T) {
	w := &Worker{jobs: make(chan Event, 100)}
	var wg sync.WaitGroup
	for i := 0; i < 50; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			w.Enqueue(Event{UserID: uuid.New(), GameID: "g", EventType: "y"})
		}()
	}
	wg.Wait()
	if len(w.jobs) != 50 {
		t.Fatalf("expected 50 enqueued, got %d", len(w.jobs))
	}
}
