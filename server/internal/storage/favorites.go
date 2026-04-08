package storage

import (
	"context"

	"github.com/google/uuid"
)

// 보관함 + 게임 통계.
// raw pgx (sqlc 우회).

func (s *Store) ListFavoriteGameIDs(ctx context.Context, userID uuid.UUID) ([]string, error) {
	rows, err := s.pool.Query(ctx,
		`SELECT game_id FROM user_favorites WHERE user_id = $1 ORDER BY created_at DESC`, userID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	var out []string
	for rows.Next() {
		var id string
		if err := rows.Scan(&id); err != nil {
			return nil, err
		}
		out = append(out, id)
	}
	return out, rows.Err()
}

func (s *Store) AddFavorite(ctx context.Context, userID uuid.UUID, gameID string) error {
	_, err := s.pool.Exec(ctx, `
		INSERT INTO user_favorites (user_id, game_id) VALUES ($1, $2)
		ON CONFLICT (user_id, game_id) DO NOTHING`, userID, gameID)
	return err
}

func (s *Store) RemoveFavorite(ctx context.Context, userID uuid.UUID, gameID string) error {
	_, err := s.pool.Exec(ctx,
		`DELETE FROM user_favorites WHERE user_id = $1 AND game_id = $2`, userID, gameID)
	return err
}

// GameStat 은 한 게임의 사용자 관점 통계.
type GameStat struct {
	GameID     string `json:"game_id"`
	PlayCount  int64  `json:"play_count"`  // 전체 플레이 횟수 (session_results row 수)
	MyBest     int32  `json:"my_best"`     // 내 최고 점수 (없으면 0)
	Favorited  bool   `json:"favorited"`   // 내 보관함 여부
}

// GameStatsForUser 는 모든 게임에 대해 (play_count, my_best, favorited) 를 한 쿼리로.
func (s *Store) GameStatsForUser(ctx context.Context, userID uuid.UUID) ([]GameStat, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT
			g.id,
			COALESCE((SELECT COUNT(*) FROM session_results WHERE game_id = g.id), 0) AS play_count,
			COALESCE((SELECT best_score FROM score_aggregates WHERE game_id = g.id AND user_id = $1), 0) AS my_best,
			EXISTS(SELECT 1 FROM user_favorites WHERE user_id = $1 AND game_id = g.id) AS favorited
		FROM games g
		ORDER BY g.id`, userID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	var out []GameStat
	for rows.Next() {
		var st GameStat
		if err := rows.Scan(&st.GameID, &st.PlayCount, &st.MyBest, &st.Favorited); err != nil {
			return nil, err
		}
		out = append(out, st)
	}
	return out, rows.Err()
}
