-- name: InsertPurchase :one
INSERT INTO purchases (user_id, product_id, platform, receipt_token, verified, expires_at)
VALUES ($1, $2, $3, $4, $5, $6)
ON CONFLICT (platform, receipt_token) DO UPDATE SET
    verified = EXCLUDED.verified,
    expires_at = EXCLUDED.expires_at
RETURNING *;

-- name: ListActivePurchases :many
SELECT * FROM purchases WHERE user_id = $1 AND verified = true;

-- name: HasAdRemoval :one
SELECT EXISTS (
    SELECT 1 FROM purchases
    WHERE user_id = $1
      AND product_id = $2
      AND verified = true
      AND (expires_at IS NULL OR expires_at > now())
) AS owned;
