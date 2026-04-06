package playstore

import (
	"context"
	"errors"
)

// Real 은 Google Play Developer API 기반 구현체. Phase 1 에서는 미완성.
//
// TODO (Iteration 2):
//  1. service account 발급 (Google Cloud → IAM → Service Account, role: androidpublisher)
//  2. JSON key Sealed Secret 으로 주입
//  3. golang.org/x/oauth2/google + google.golang.org/api/androidpublisher/v3 로 호출
//  4. products.purchases.products.get(packageName, productID, token)
type Real struct {
	PackageName string
	// CredentialsJSON []byte
}

// Verify 는 미구현. Phase 1 은 Mock 만 사용.
func (r *Real) Verify(_ context.Context, _, _ string) (Result, error) {
	return Result{}, errors.New("playstore.Real not implemented (use Mock in Phase 1)")
}
