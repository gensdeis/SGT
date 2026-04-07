-- +goose Up
-- +goose StatementBegin
-- 보관함 (하트) — 유저별 즐겨찾기 게임

CREATE TABLE IF NOT EXISTS user_favorites (
    user_id    uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    game_id    text NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, game_id)
);
CREATE INDEX IF NOT EXISTS idx_user_favorites_user ON user_favorites(user_id);
-- +goose StatementEnd

-- +goose Down
-- +goose StatementBegin
DROP TABLE IF EXISTS user_favorites;
-- +goose StatementEnd
