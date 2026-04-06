package storage

import (
	"context"
	"time"

	"github.com/google/uuid"
)

const sqlInsertPurchase = `
INSERT INTO purchases (user_id, product_id, platform, receipt_token, verified, expires_at)
VALUES ($1, $2, $3, $4, $5, $6)
ON CONFLICT (platform, receipt_token) DO UPDATE SET
    verified = EXCLUDED.verified,
    expires_at = EXCLUDED.expires_at
RETURNING id, user_id, product_id, platform, receipt_token, verified, expires_at, created_at`

// InsertPurchaseParams 는 영수증 저장 인자.
type InsertPurchaseParams struct {
	UserID       uuid.UUID
	ProductID    string
	Platform     string
	ReceiptToken string
	Verified     bool
	ExpiresAt    *time.Time
}

func (s *Store) InsertPurchase(ctx context.Context, p InsertPurchaseParams) (Purchase, error) {
	var pu Purchase
	err := s.pool.QueryRow(ctx, sqlInsertPurchase, p.UserID, p.ProductID, p.Platform, p.ReceiptToken, p.Verified, p.ExpiresAt).Scan(
		&pu.ID, &pu.UserID, &pu.ProductID, &pu.Platform, &pu.ReceiptToken, &pu.Verified, &pu.ExpiresAt, &pu.CreatedAt)
	return pu, err
}

const sqlListActivePurchases = `
SELECT id, user_id, product_id, platform, receipt_token, verified, expires_at, created_at
FROM purchases WHERE user_id = $1 AND verified = true`

func (s *Store) ListActivePurchases(ctx context.Context, userID uuid.UUID) ([]Purchase, error) {
	rows, err := s.pool.Query(ctx, sqlListActivePurchases, userID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := make([]Purchase, 0)
	for rows.Next() {
		var p Purchase
		if err := rows.Scan(&p.ID, &p.UserID, &p.ProductID, &p.Platform, &p.ReceiptToken, &p.Verified, &p.ExpiresAt, &p.CreatedAt); err != nil {
			return nil, err
		}
		out = append(out, p)
	}
	return out, rows.Err()
}

const sqlHasAdRemoval = `
SELECT EXISTS (
    SELECT 1 FROM purchases
    WHERE user_id = $1
      AND product_id = $2
      AND verified = true
      AND (expires_at IS NULL OR expires_at > now())
)`

func (s *Store) HasAdRemoval(ctx context.Context, userID uuid.UUID, productID string) (bool, error) {
	var owned bool
	err := s.pool.QueryRow(ctx, sqlHasAdRemoval, userID, productID).Scan(&owned)
	return owned, err
}
