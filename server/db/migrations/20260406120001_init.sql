-- +goose Up
-- +goose StatementBegin
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

CREATE TABLE users (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id     text NOT NULL UNIQUE,
    created_at    timestamptz NOT NULL DEFAULT now(),
    last_seen_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE games (
    id              text PRIMARY KEY,
    title           text NOT NULL,
    creator_id      text NOT NULL,
    time_limit_sec  int  NOT NULL,
    tags            text[] NOT NULL DEFAULT '{}',
    bundle_url      text NOT NULL DEFAULT '',
    bundle_version  text NOT NULL DEFAULT '',
    bundle_hash     text NOT NULL DEFAULT '',
    created_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX idx_games_tags ON games USING GIN (tags);

CREATE TABLE sessions (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id               uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    started_at            timestamptz NOT NULL DEFAULT now(),
    ended_at              timestamptz,
    recommended_game_ids  text[] NOT NULL DEFAULT '{}',
    dda_intensity         int NOT NULL DEFAULT 0
);
CREATE INDEX idx_sessions_user ON sessions(user_id, started_at DESC);

CREATE TABLE session_results (
    session_id    uuid NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    game_id       text NOT NULL REFERENCES games(id),
    score         int  NOT NULL,
    play_time_sec real NOT NULL,
    cleared       bool NOT NULL,
    submitted_at  timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (session_id, game_id)
);

CREATE TABLE analytics_events (
    id          bigserial PRIMARY KEY,
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    game_id     text NOT NULL,
    event_type  text NOT NULL,
    payload     jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX idx_analytics_user_game ON analytics_events(user_id, game_id, created_at DESC);

CREATE TABLE purchases (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id       uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    product_id    text NOT NULL,
    platform      text NOT NULL,
    receipt_token text NOT NULL,
    verified      bool NOT NULL DEFAULT false,
    expires_at    timestamptz,
    created_at    timestamptz NOT NULL DEFAULT now(),
    UNIQUE (platform, receipt_token)
);
CREATE INDEX idx_purchases_user ON purchases(user_id, verified);

CREATE TABLE score_aggregates (
    game_id     text NOT NULL REFERENCES games(id),
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    best_score  int  NOT NULL,
    updated_at  timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (game_id, user_id)
);
CREATE INDEX idx_score_aggregates_game_score ON score_aggregates(game_id, best_score DESC);

CREATE TABLE global_rankings_weekly (
    week_start   date NOT NULL,
    user_id      uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    total_score  bigint NOT NULL,
    rank         int NOT NULL,
    PRIMARY KEY (week_start, user_id)
);
CREATE INDEX idx_global_rankings_week_rank ON global_rankings_weekly(week_start, rank);
-- +goose StatementEnd

-- +goose Down
-- +goose StatementBegin
DROP TABLE IF EXISTS global_rankings_weekly;
DROP TABLE IF EXISTS score_aggregates;
DROP TABLE IF EXISTS purchases;
DROP TABLE IF EXISTS analytics_events;
DROP TABLE IF EXISTS session_results;
DROP TABLE IF EXISTS sessions;
DROP TABLE IF EXISTS games;
DROP TABLE IF EXISTS users;
-- +goose StatementEnd
