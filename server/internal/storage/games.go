package storage

import "context"

const sqlListGames = `
SELECT id, title, creator_id, time_limit_sec, tags, bundle_url, bundle_version, bundle_hash, created_at
FROM games ORDER BY id`

func (s *Store) ListGames(ctx context.Context) ([]Game, error) {
	rows, err := s.pool.Query(ctx, sqlListGames)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := make([]Game, 0)
	for rows.Next() {
		var g Game
		if err := rows.Scan(&g.ID, &g.Title, &g.CreatorID, &g.TimeLimitSec, &g.Tags, &g.BundleURL, &g.BundleVersion, &g.BundleHash, &g.CreatedAt); err != nil {
			return nil, err
		}
		out = append(out, g)
	}
	return out, rows.Err()
}

const sqlListGamesByTags = `
SELECT id, title, creator_id, time_limit_sec, tags, bundle_url, bundle_version, bundle_hash, created_at
FROM games WHERE tags && $1::text[] ORDER BY id`

func (s *Store) ListGamesByTags(ctx context.Context, tags []string) ([]Game, error) {
	rows, err := s.pool.Query(ctx, sqlListGamesByTags, tags)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := make([]Game, 0)
	for rows.Next() {
		var g Game
		if err := rows.Scan(&g.ID, &g.Title, &g.CreatorID, &g.TimeLimitSec, &g.Tags, &g.BundleURL, &g.BundleVersion, &g.BundleHash, &g.CreatedAt); err != nil {
			return nil, err
		}
		out = append(out, g)
	}
	return out, rows.Err()
}

const sqlGetGameByID = `
SELECT id, title, creator_id, time_limit_sec, tags, bundle_url, bundle_version, bundle_hash, created_at
FROM games WHERE id = $1`

func (s *Store) GetGameByID(ctx context.Context, id string) (Game, error) {
	var g Game
	err := s.pool.QueryRow(ctx, sqlGetGameByID, id).Scan(&g.ID, &g.Title, &g.CreatorID, &g.TimeLimitSec, &g.Tags, &g.BundleURL, &g.BundleVersion, &g.BundleHash, &g.CreatedAt)
	return g, wrapNoRows(err)
}

const sqlUpsertGame = `
INSERT INTO games (id, title, creator_id, time_limit_sec, tags, bundle_url, bundle_version, bundle_hash)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title,
    creator_id = EXCLUDED.creator_id,
    time_limit_sec = EXCLUDED.time_limit_sec,
    tags = EXCLUDED.tags,
    bundle_url = EXCLUDED.bundle_url,
    bundle_version = EXCLUDED.bundle_version,
    bundle_hash = EXCLUDED.bundle_hash`

// UpsertGameParams 는 UpsertGame 인자.
type UpsertGameParams struct {
	ID            string
	Title         string
	CreatorID     string
	TimeLimitSec  int32
	Tags          []string
	BundleURL     string
	BundleVersion string
	BundleHash    string
}

func (s *Store) UpsertGame(ctx context.Context, p UpsertGameParams) error {
	_, err := s.pool.Exec(ctx, sqlUpsertGame, p.ID, p.Title, p.CreatorID, p.TimeLimitSec, p.Tags, p.BundleURL, p.BundleVersion, p.BundleHash)
	return err
}
