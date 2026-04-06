package storage

import (
	"context"

	"github.com/gensdeis/SGT/server/internal/storage/sqlc"
	"github.com/google/uuid"
)

func (s *Store) GetUserByDeviceID(ctx context.Context, deviceID string) (User, error) {
	u, err := s.q.GetUserByDeviceID(ctx, deviceID)
	if err != nil {
		return User{}, wrapNoRows(err)
	}
	return convertUser(u), nil
}

func (s *Store) CreateUser(ctx context.Context, deviceID string) (User, error) {
	u, err := s.q.CreateUser(ctx, deviceID)
	if err != nil {
		return User{}, err
	}
	return convertUser(u), nil
}

func (s *Store) UpdateLastSeen(ctx context.Context, id uuid.UUID) error {
	return s.q.UpdateLastSeen(ctx, id)
}

func (s *Store) GetUserByID(ctx context.Context, id uuid.UUID) (User, error) {
	u, err := s.q.GetUserByID(ctx, id)
	if err != nil {
		return User{}, wrapNoRows(err)
	}
	return convertUser(u), nil
}

func convertUser(u sqlc.User) User {
	return User{
		ID:         u.ID,
		DeviceID:   u.DeviceID,
		CreatedAt:  u.CreatedAt,
		LastSeenAt: u.LastSeenAt,
	}
}
