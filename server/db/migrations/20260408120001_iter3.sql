-- +goose Up
-- +goose StatementBegin
-- Iter 3: 프로필 + 데일리 미션 + 공유 보상 + 코인

ALTER TABLE users
    ADD COLUMN IF NOT EXISTS nickname  text NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS avatar_id int  NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS coins     int  NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS daily_missions (
    user_id      uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    mission_id   text NOT NULL,
    day_key      text NOT NULL,           -- YYYYMMDD UTC
    progress     int  NOT NULL DEFAULT 0,
    target       int  NOT NULL,
    completed_at timestamptz,
    claimed_at   timestamptz,
    created_at   timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, mission_id, day_key)
);
CREATE INDEX IF NOT EXISTS idx_daily_missions_user_day ON daily_missions(user_id, day_key);

CREATE TABLE IF NOT EXISTS share_rewards (
    user_id          uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    day_key          text NOT NULL,
    count            int  NOT NULL DEFAULT 0,
    last_claimed_at  timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, day_key)
);
-- +goose StatementEnd

-- +goose Down
-- +goose StatementBegin
DROP TABLE IF EXISTS share_rewards;
DROP TABLE IF EXISTS daily_missions;
ALTER TABLE users
    DROP COLUMN IF EXISTS coins,
    DROP COLUMN IF EXISTS avatar_id,
    DROP COLUMN IF EXISTS nickname;
-- +goose StatementEnd
