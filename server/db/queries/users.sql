-- name: GetUserByDeviceID :one
SELECT * FROM users WHERE device_id = $1;

-- name: CreateUser :one
INSERT INTO users (device_id) VALUES ($1)
RETURNING *;

-- name: UpdateLastSeen :exec
UPDATE users SET last_seen_at = now() WHERE id = $1;

-- name: GetUserByID :one
SELECT * FROM users WHERE id = $1;
