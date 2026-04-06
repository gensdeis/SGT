-- name: CreateSession :one
INSERT INTO sessions (user_id, recommended_game_ids, dda_intensity)
VALUES ($1, $2, $3)
RETURNING *;

-- name: GetSession :one
SELECT * FROM sessions WHERE id = $1;

-- name: EndSession :exec
UPDATE sessions SET ended_at = now() WHERE id = $1 AND user_id = $2;

-- name: CountSessionsByUser :one
SELECT COUNT(*) FROM sessions WHERE user_id = $1;

-- name: InsertSessionResult :exec
INSERT INTO session_results (session_id, game_id, score, play_time_sec, cleared)
VALUES ($1, $2, $3, $4, $5)
ON CONFLICT (session_id, game_id) DO UPDATE SET
    score = EXCLUDED.score,
    play_time_sec = EXCLUDED.play_time_sec,
    cleared = EXCLUDED.cleared,
    submitted_at = now();

-- name: ListResultsBySession :many
SELECT * FROM session_results WHERE session_id = $1;

-- name: ListRecentResultsByUser :many
SELECT sr.* FROM session_results sr
JOIN sessions s ON sr.session_id = s.id
WHERE s.user_id = $1
ORDER BY sr.submitted_at DESC
LIMIT $2;
