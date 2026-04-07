-- +goose Up
-- +goose StatementBegin
-- Iter 4a: 운영툴 admin

CREATE TABLE IF NOT EXISTS admin_users (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    login         text NOT NULL UNIQUE,
    password_hash text NOT NULL,
    role          text NOT NULL DEFAULT 'admin',
    created_at    timestamptz NOT NULL DEFAULT now()
);

ALTER TABLE users
    ADD COLUMN IF NOT EXISTS banned bool NOT NULL DEFAULT false;

CREATE TABLE IF NOT EXISTS notices (
    id          bigserial PRIMARY KEY,
    title       text NOT NULL,
    body        text NOT NULL,
    created_at  timestamptz NOT NULL DEFAULT now(),
    expires_at  timestamptz
);
-- +goose StatementEnd

-- +goose Down
-- +goose StatementBegin
DROP TABLE IF EXISTS notices;
ALTER TABLE users DROP COLUMN IF EXISTS banned;
DROP TABLE IF EXISTS admin_users;
-- +goose StatementEnd
