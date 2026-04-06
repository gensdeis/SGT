// Package purchase 는 광고제거 영수증 검증 + 저장.
//
// 흐름:
//  1. 클라이언트가 PurchaseToken 을 전송
//  2. playstore.Verifier 로 검증 (Mock or Real)
//  3. purchases 테이블에 저장 (verified=true)
//  4. 다음 로그인부터 JWT.AdRemoved=true
package purchase

import (
	"context"
	"errors"

	"github.com/gensdeis/SGT/server/internal/purchase/playstore"
	"github.com/gensdeis/SGT/server/internal/storage"
	"github.com/google/uuid"
)

// AdRemovalProductID 는 광고제거 SKU.
const AdRemovalProductID = "ad_removal"

// Service 는 비즈니스 로직.
type Service struct {
	store    *storage.Store
	verifier playstore.Verifier
}

func NewService(store *storage.Store, verifier playstore.Verifier) *Service {
	return &Service{store: store, verifier: verifier}
}

// VerifyResult 는 응답.
type VerifyResult struct {
	Valid     bool   `json:"valid"`
	ProductID string `json:"product_id"`
	AdRemoved bool   `json:"ad_removed"`
}

// Verify 는 영수증을 검증하고 DB 에 저장한다.
func (s *Service) Verify(ctx context.Context, userID uuid.UUID, productID, token string) (VerifyResult, error) {
	if productID == "" || token == "" {
		return VerifyResult{}, errors.New("purchase: product_id and token required")
	}
	res, err := s.verifier.Verify(ctx, productID, token)
	if err != nil {
		return VerifyResult{}, err
	}
	if !res.Valid {
		return VerifyResult{Valid: false, ProductID: productID}, nil
	}
	_, err = s.store.InsertPurchase(ctx, storage.InsertPurchaseParams{
		UserID:       userID,
		ProductID:    res.ProductID,
		Platform:     "android",
		ReceiptToken: token,
		Verified:     true,
		ExpiresAt:    res.ExpiresAt,
	})
	if err != nil {
		return VerifyResult{}, err
	}
	return VerifyResult{
		Valid:     true,
		ProductID: res.ProductID,
		AdRemoved: res.ProductID == AdRemovalProductID,
	}, nil
}
