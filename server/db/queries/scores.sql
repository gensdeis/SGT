-- name: UpsertBestScore :exec
INSERT INTO score_aggregates (game_id, user_id, best_score, updated_at)
VALUES ($1, $2, $3, now())
ON CONFLICT (game_id, user_id) DO UPDATE SET
    best_score = GREATEST(score_aggregates.best_score, EXCLUDED.best_score),
    updated_at = now();

-- name: ListTopByGame :many
SELECT game_id, user_id, best_score, updated_at
FROM score_aggregates
WHERE game_id = $1
ORDER BY best_score DESC
LIMIT $2;

-- name: GetUserBest :one
SELECT * FROM score_aggregates WHERE game_id = $1 AND user_id = $2;

-- name: UpsertWeeklyRank :exec
INSERT INTO global_rankings_weekly (week_start, user_id, total_score, rank)
VALUES ($1, $2, $3, $4)
ON CONFLICT (week_start, user_id) DO UPDATE SET
    total_score = EXCLUDED.total_score,
    rank = EXCLUDED.rank;

-- name: ListGlobalTop :many
SELECT * FROM global_rankings_weekly
WHERE week_start = $1
ORDER BY rank ASC
LIMIT $2;
