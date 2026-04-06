// Package config 는 환경변수 + games.yaml 로딩을 담당한다.
// 모든 분기/Mock 결정은 cmd/api/main.go 에서만 수행하고,
// 이 패키지는 단순히 값을 로드/파싱하기만 한다.
package config

import (
	"fmt"
	"os"
	"strconv"
	"time"
)

// Config 는 서버 전체 런타임 설정.
type Config struct {
	Port     string
	LogLevel string

	DatabaseURL string
	DBMaxConns  int32
	DBMinConns  int32

	RedisURL string

	JWTSecret   string
	JWTTTL      time.Duration

	HMACBaseKey       string
	BuildGUID         string
	HMACReplayWindow  time.Duration

	DevMockReceipt bool

	GamesConfigPath string
}

// Load 는 환경변수에서 설정을 읽어 Config 를 반환한다.
// 필수 값이 비어 있으면 error 를 반환한다.
func Load() (*Config, error) {
	cfg := &Config{
		Port:             getenvDefault("PORT", "8080"),
		LogLevel:         getenvDefault("LOG_LEVEL", "info"),
		DatabaseURL:      os.Getenv("DATABASE_URL"),
		DBMaxConns:       int32(getenvInt("DB_MAX_CONNS", 10)),
		DBMinConns:       int32(getenvInt("DB_MIN_CONNS", 2)),
		RedisURL:         os.Getenv("REDIS_URL"),
		JWTSecret:        os.Getenv("JWT_SECRET"),
		JWTTTL:           time.Duration(getenvInt("JWT_TTL_HOURS", 720)) * time.Hour,
		HMACBaseKey:      os.Getenv("HMAC_BASE_KEY"),
		BuildGUID:        os.Getenv("BUILD_GUID"),
		HMACReplayWindow: time.Duration(getenvInt("HMAC_REPLAY_WINDOW_SEC", 30)) * time.Second,
		DevMockReceipt:   getenvBool("DEV_MOCK_RECEIPT", false),
		GamesConfigPath:  getenvDefault("GAMES_CONFIG_PATH", "./config/games.yaml"),
	}

	if cfg.DatabaseURL == "" {
		return nil, fmt.Errorf("DATABASE_URL is required")
	}
	if cfg.RedisURL == "" {
		return nil, fmt.Errorf("REDIS_URL is required")
	}
	if cfg.JWTSecret == "" {
		return nil, fmt.Errorf("JWT_SECRET is required")
	}
	if cfg.HMACBaseKey == "" {
		return nil, fmt.Errorf("HMAC_BASE_KEY is required")
	}
	if cfg.BuildGUID == "" {
		return nil, fmt.Errorf("BUILD_GUID is required")
	}

	return cfg, nil
}

func getenvDefault(k, def string) string {
	if v := os.Getenv(k); v != "" {
		return v
	}
	return def
}

func getenvInt(k string, def int) int {
	v := os.Getenv(k)
	if v == "" {
		return def
	}
	n, err := strconv.Atoi(v)
	if err != nil {
		return def
	}
	return n
}

func getenvBool(k string, def bool) bool {
	v := os.Getenv(k)
	if v == "" {
		return def
	}
	return v == "true" || v == "1" || v == "yes"
}
