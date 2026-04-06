package storage

import (
	"context"

	"github.com/gensdeis/SGT/server/internal/storage/sqlc"
)

func (s *Store) ListGames(ctx context.Context) ([]Game, error) {
	rows, err := s.q.ListGames(ctx)
	if err != nil {
		return nil, err
	}
	out := make([]Game, len(rows))
	for i, r := range rows {
		out[i] = convertGame(r)
	}
	return out, nil
}

func (s *Store) ListGamesByTags(ctx context.Context, tags []string) ([]Game, error) {
	rows, err := s.q.ListGamesByTags(ctx, tags)
	if err != nil {
		return nil, err
	}
	out := make([]Game, len(rows))
	for i, r := range rows {
		out[i] = convertGame(r)
	}
	return out, nil
}

func (s *Store) GetGameByID(ctx context.Context, id string) (Game, error) {
	g, err := s.q.GetGameByID(ctx, id)
	if err != nil {
		return Game{}, wrapNoRows(err)
	}
	return convertGame(g), nil
}

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
	return s.q.UpsertGame(ctx, sqlc.UpsertGameParams{
		ID:            p.ID,
		Title:         p.Title,
		CreatorID:     p.CreatorID,
		TimeLimitSec:  p.TimeLimitSec,
		Tags:          p.Tags,
		BundleUrl:     p.BundleURL,
		BundleVersion: p.BundleVersion,
		BundleHash:    p.BundleHash,
	})
}

func convertGame(g sqlc.Game) Game {
	return Game{
		ID:            g.ID,
		Title:         g.Title,
		CreatorID:     g.CreatorID,
		TimeLimitSec:  g.TimeLimitSec,
		Tags:          g.Tags,
		BundleURL:     g.BundleUrl,
		BundleVersion: g.BundleVersion,
		BundleHash:    g.BundleHash,
		CreatedAt:     g.CreatedAt,
	}
}
