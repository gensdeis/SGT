package storage

import (
	"context"
	"time"

	"github.com/google/uuid"
)

type DailyMission struct {
	UserID      uuid.UUID  `json:"user_id"`
	MissionID   string     `json:"mission_id"`
	DayKey      string     `json:"day_key"`
	Progress    int32      `json:"progress"`
	Target      int32      `json:"target"`
	CompletedAt *time.Time `json:"completed_at,omitempty"`
	ClaimedAt   *time.Time `json:"claimed_at,omitempty"`
}

// EnsureDailyMission 은 (user, mission, day) 가 없으면 INSERT.
func (s *Store) EnsureDailyMission(ctx context.Context, userID uuid.UUID, missionID, dayKey string, target int32) error {
	_, err := s.pool.Exec(ctx, `
		INSERT INTO daily_missions (user_id, mission_id, day_key, target)
		VALUES ($1, $2, $3, $4)
		ON CONFLICT (user_id, mission_id, day_key) DO NOTHING`,
		userID, missionID, dayKey, target)
	return err
}

func (s *Store) ListDailyMissions(ctx context.Context, userID uuid.UUID, dayKey string) ([]DailyMission, error) {
	rows, err := s.pool.Query(ctx, `
		SELECT user_id, mission_id, day_key, progress, target, completed_at, claimed_at
		FROM daily_missions WHERE user_id = $1 AND day_key = $2
		ORDER BY mission_id`,
		userID, dayKey)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	var out []DailyMission
	for rows.Next() {
		var m DailyMission
		if err := rows.Scan(&m.UserID, &m.MissionID, &m.DayKey, &m.Progress, &m.Target, &m.CompletedAt, &m.ClaimedAt); err != nil {
			return nil, err
		}
		out = append(out, m)
	}
	return out, rows.Err()
}

// IncMissionProgress 는 progress 를 더하고 target 도달 시 completed_at 자동 set.
func (s *Store) IncMissionProgress(ctx context.Context, userID uuid.UUID, missionID, dayKey string, delta int32) error {
	_, err := s.pool.Exec(ctx, `
		UPDATE daily_missions
		SET progress = LEAST(progress + $4, target),
		    completed_at = CASE
		        WHEN completed_at IS NULL AND progress + $4 >= target THEN now()
		        ELSE completed_at
		    END
		WHERE user_id = $1 AND mission_id = $2 AND day_key = $3`,
		userID, missionID, dayKey, delta)
	return err
}

// ClaimMission 은 completed && !claimed 인 미션을 claim 처리. 성공 시 true.
func (s *Store) ClaimMission(ctx context.Context, userID uuid.UUID, missionID, dayKey string) (bool, error) {
	tag, err := s.pool.Exec(ctx, `
		UPDATE daily_missions
		SET claimed_at = now()
		WHERE user_id = $1 AND mission_id = $2 AND day_key = $3
		  AND completed_at IS NOT NULL AND claimed_at IS NULL`,
		userID, missionID, dayKey)
	if err != nil {
		return false, err
	}
	return tag.RowsAffected() > 0, nil
}
