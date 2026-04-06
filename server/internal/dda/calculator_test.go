package dda

import (
	"testing"

	"github.com/gensdeis/SGT/server/internal/storage"
)

func TestCalculate(t *testing.T) {
	cases := []struct {
		sr   float64
		want int
	}{
		{0.0, -1},
		{0.39, -1},
		{0.4, 0},
		{0.5, 0},
		{0.8, 0},
		{0.81, +1},
		{1.0, +1},
	}
	for _, c := range cases {
		if got := Calculate(c.sr); got != c.want {
			t.Errorf("Calculate(%.2f)=%d want %d", c.sr, got, c.want)
		}
	}
}

func TestFromResults_Empty(t *testing.T) {
	sr, delta := FromResults(nil)
	if sr != 0.5 || delta != 0 {
		t.Fatalf("expected sr=0.5 delta=0, got sr=%v delta=%v", sr, delta)
	}
}

func TestFromResults_AllCleared(t *testing.T) {
	r := []storage.SessionResult{
		{Cleared: true}, {Cleared: true}, {Cleared: true}, {Cleared: true}, {Cleared: true},
		{Cleared: true}, {Cleared: true}, {Cleared: true}, {Cleared: true}, {Cleared: true},
	}
	sr, delta := FromResults(r)
	if sr != 1.0 || delta != +1 {
		t.Fatalf("expected sr=1.0 delta=+1, got %v %v", sr, delta)
	}
}

func TestFromResults_LowSR(t *testing.T) {
	r := []storage.SessionResult{{Cleared: false}, {Cleared: false}, {Cleared: true}}
	sr, delta := FromResults(r)
	if delta != -1 {
		t.Fatalf("expected delta=-1, got %v (sr=%v)", delta, sr)
	}
}
