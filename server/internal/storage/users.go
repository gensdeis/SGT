package storage

import (
	"context"

	"github.com/google/uuid"
)

const sqlGetUserByDeviceID = `SELECT id, device_id, created_at, last_seen_at FROM users WHERE device_id = $1`

func (s *Store) GetUserByDeviceID(ctx context.Context, deviceID string) (User, error) {
	var u User
	err := s.pool.QueryRow(ctx, sqlGetUserByDeviceID, deviceID).Scan(&u.ID, &u.DeviceID, &u.CreatedAt, &u.LastSeenAt)
	return u, wrapNoRows(err)
}

const sqlCreateUser = `INSERT INTO users (device_id) VALUES ($1) RETURNING id, device_id, created_at, last_seen_at`

func (s *Store) CreateUser(ctx context.Context, deviceID string) (User, error) {
	var u User
	err := s.pool.QueryRow(ctx, sqlCreateUser, deviceID).Scan(&u.ID, &u.DeviceID, &u.CreatedAt, &u.LastSeenAt)
	return u, err
}

const sqlUpdateLastSeen = `UPDATE users SET last_seen_at = now() WHERE id = $1`

func (s *Store) UpdateLastSeen(ctx context.Context, id uuid.UUID) error {
	_, err := s.pool.Exec(ctx, sqlUpdateLastSeen, id)
	return err
}

const sqlGetUserByID = `SELECT id, device_id, created_at, last_seen_at FROM users WHERE id = $1`

func (s *Store) GetUserByID(ctx context.Context, id uuid.UUID) (User, error) {
	var u User
	err := s.pool.QueryRow(ctx, sqlGetUserByID, id).Scan(&u.ID, &u.DeviceID, &u.CreatedAt, &u.LastSeenAt)
	return u, wrapNoRows(err)
}
