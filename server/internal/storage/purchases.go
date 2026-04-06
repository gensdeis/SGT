package storage

import (
	"context"
	"time"

	"github.com/gensdeis/SGT/server/internal/storage/sqlc"
	"github.com/google/uuid"
)

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
	pu, err := s.q.InsertPurchase(ctx, sqlc.InsertPurchaseParams{
		UserID:       p.UserID,
		ProductID:    p.ProductID,
		Platform:     p.Platform,
		ReceiptToken: p.ReceiptToken,
		Verified:     p.Verified,
		ExpiresAt:    p.ExpiresAt,
	})
	if err != nil {
		return Purchase{}, err
	}
	return convertPurchase(pu), nil
}

func (s *Store) ListActivePurchases(ctx context.Context, userID uuid.UUID) ([]Purchase, error) {
	rows, err := s.q.ListActivePurchases(ctx, userID)
	if err != nil {
		return nil, err
	}
	out := make([]Purchase, len(rows))
	for i, r := range rows {
		out[i] = convertPurchase(r)
	}
	return out, nil
}

func (s *Store) HasAdRemoval(ctx context.Context, userID uuid.UUID, productID string) (bool, error) {
	return s.q.HasAdRemoval(ctx, sqlc.HasAdRemovalParams{UserID: userID, ProductID: productID})
}

func convertPurchase(p sqlc.Purchase) Purchase {
	return Purchase{
		ID:           p.ID,
		UserID:       p.UserID,
		ProductID:    p.ProductID,
		Platform:     p.Platform,
		ReceiptToken: p.ReceiptToken,
		Verified:     p.Verified,
		ExpiresAt:    p.ExpiresAt,
		CreatedAt:    p.CreatedAt,
	}
}
