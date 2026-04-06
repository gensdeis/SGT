// Package playstore 는 Google Play 구매 영수증 검증 인터페이스 + 실제/Mock 구현체.
//
// 실제 구현은 Google Play Developer API (androidpublisher v3) 호출이 필요하다.
// Phase 1 에서는 Mock 만 동작하고, real.go 는 service account 발급 후 채워진다.
package playstore

import (
	"context"
	"time"
)

// Result 는 영수증 검증 결과.
type Result struct {
	Valid     bool
	ProductID string
	ExpiresAt *time.Time // 비-구독은 nil
}

// Verifier 는 영수증 검증기.
type Verifier interface {
	Verify(ctx context.Context, productID, purchaseToken string) (Result, error)
}
