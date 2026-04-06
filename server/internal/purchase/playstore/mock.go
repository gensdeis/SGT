package playstore

import "context"

// Mock 은 DEV_MOCK_RECEIPT=true 일 때 사용. 항상 valid 반환.
type Mock struct{}

// Verify 는 모든 토큰을 valid 로 처리.
func (Mock) Verify(_ context.Context, productID, _ string) (Result, error) {
	return Result{Valid: true, ProductID: productID}, nil
}
