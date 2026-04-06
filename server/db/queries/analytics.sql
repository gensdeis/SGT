-- name: InsertAnalyticsEvent :exec
INSERT INTO analytics_events (user_id, game_id, event_type, payload)
VALUES ($1, $2, $3, $4);

-- name: ListEventsByUser :many
SELECT * FROM analytics_events
WHERE user_id = $1
ORDER BY created_at DESC
LIMIT $2;
