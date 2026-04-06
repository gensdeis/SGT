// Package migrate 는 서버 시작 시 goose 마이그레이션을 자동 적용한다.
//
// goose v3 라이브러리 모드 사용 — CLI 불필요.
// 마이그레이션 파일은 db/migrations/ 의 .sql 파일 (Up/Down 양방향).
//
// 이미지 안에는 /app/db/migrations 로 복사된다 (Dockerfile 참조).
package migrate

import (
	"context"
	"database/sql"
	"fmt"
	"log/slog"

	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/jackc/pgx/v5/stdlib"
	"github.com/pressly/goose/v3"
)

// Up 은 마이그레이션 디렉토리의 모든 Up 을 적용한다.
// 이미 적용된 마이그레이션은 goose 가 알아서 스킵.
func Up(ctx context.Context, pool *pgxpool.Pool, dir string) error {
	// goose 는 database/sql 인터페이스가 필요. pgx stdlib 어댑터 사용.
	cfg := pool.Config().ConnConfig
	connStr := stdlib.RegisterConnConfig(cfg)
	db, err := sql.Open("pgx", connStr)
	if err != nil {
		return fmt.Errorf("open sql: %w", err)
	}
	defer db.Close()

	if err := db.PingContext(ctx); err != nil {
		return fmt.Errorf("ping: %w", err)
	}

	goose.SetLogger(slogGooseLogger{})
	if err := goose.SetDialect("postgres"); err != nil {
		return fmt.Errorf("set dialect: %w", err)
	}

	if err := goose.UpContext(ctx, db, dir); err != nil {
		return fmt.Errorf("goose up: %w", err)
	}
	slog.Info("migrations applied", "dir", dir)
	return nil
}

// slogGooseLogger 는 goose 의 Printf/Fatalf 를 slog 로 라우팅.
type slogGooseLogger struct{}

func (slogGooseLogger) Printf(format string, args ...interface{}) {
	slog.Info("goose", "msg", fmt.Sprintf(format, args...))
}
func (slogGooseLogger) Fatalf(format string, args ...interface{}) {
	slog.Error("goose", "msg", fmt.Sprintf(format, args...))
}
