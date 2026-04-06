package storage

import (
	"context"
	"errors"

	"github.com/gensdeis/SGT/server/internal/storage/sqlc"
	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
)

// ErrNotFound 는 단일 행 조회 결과가 없을 때.
var ErrNotFound = errors.New("storage: not found")

// Store 는 모든 쿼리 메서드를 노출하는 구조체.
// 내부적으로 sqlc 생성 코드 (*sqlc.Queries) 에 위임한다.
// 도메인 코드는 이 패키지의 메서드만 사용하고 sqlc 패키지를 직접 import 하지 않는다.
type Store struct {
	pool *pgxpool.Pool
	q    *sqlc.Queries
}

// New 는 pgxpool 을 감싸는 Store 를 반환한다.
func New(pool *pgxpool.Pool) *Store {
	return &Store{pool: pool, q: sqlc.New(pool)}
}

// Pool 은 raw 접근이 필요한 경우 (예: 트랜잭션) 노출.
func (s *Store) Pool() *pgxpool.Pool { return s.pool }

func wrapNoRows(err error) error {
	if errors.Is(err, pgx.ErrNoRows) {
		return ErrNotFound
	}
	return err
}

// Ping 은 readiness 체크용.
func (s *Store) Ping(ctx context.Context) error {
	return s.pool.Ping(ctx)
}
