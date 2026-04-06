-- name: ListGames :many
SELECT * FROM games ORDER BY id;

-- name: ListGamesByTags :many
SELECT * FROM games WHERE tags && $1::text[] ORDER BY id;

-- name: GetGameByID :one
SELECT * FROM games WHERE id = $1;

-- name: UpsertGame :exec
INSERT INTO games (id, title, creator_id, time_limit_sec, tags, bundle_url, bundle_version, bundle_hash)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title,
    creator_id = EXCLUDED.creator_id,
    time_limit_sec = EXCLUDED.time_limit_sec,
    tags = EXCLUDED.tags,
    bundle_url = EXCLUDED.bundle_url,
    bundle_version = EXCLUDED.bundle_version,
    bundle_hash = EXCLUDED.bundle_hash;
