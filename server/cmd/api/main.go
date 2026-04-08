// Command api 는 숏게타 게임 서버 진입점.
//
// 모든 환경변수 분기 / Mock 결정은 이 파일에서만 이루어진다.
// 비즈니스 로직(internal/*)에는 os.Getenv 호출이 없다.
package main

import (
	"context"
	"errors"
	"log/slog"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/ansrivas/fiberprometheus/v2"
	"github.com/gofiber/fiber/v2"
	"github.com/gofiber/fiber/v2/middleware/cors"
	"github.com/gofiber/fiber/v2/middleware/recover"
	"github.com/redis/go-redis/v9"

	"github.com/gensdeis/SGT/server/internal/admin"
	"github.com/gensdeis/SGT/server/internal/analytics"
	"github.com/gensdeis/SGT/server/internal/anticheat"
	"github.com/gensdeis/SGT/server/internal/auth"
	"github.com/gensdeis/SGT/server/internal/bundles"
	"github.com/gensdeis/SGT/server/internal/config"
	"github.com/gensdeis/SGT/server/internal/game"
	"github.com/gensdeis/SGT/server/internal/migrate"
	"github.com/gensdeis/SGT/server/internal/missions"
	"github.com/gensdeis/SGT/server/internal/profile"
	"github.com/gensdeis/SGT/server/internal/share"
	"github.com/gensdeis/SGT/server/internal/purchase"
	"github.com/gensdeis/SGT/server/internal/purchase/playstore"
	"github.com/gensdeis/SGT/server/internal/ranking"
	"github.com/gensdeis/SGT/server/internal/ratelimit"
	"github.com/gensdeis/SGT/server/internal/session"
	"github.com/gensdeis/SGT/server/internal/storage"
	"github.com/gensdeis/SGT/server/pkg/db"
	hmacpkg "github.com/gensdeis/SGT/server/pkg/hmac"
)

func main() {
	// === 1. 로거 ===
	logger := slog.New(slog.NewJSONHandler(os.Stdout, &slog.HandlerOptions{Level: parseLogLevel(os.Getenv("LOG_LEVEL"))}))
	slog.SetDefault(logger)

	// === 2. 설정 ===
	cfg, err := config.Load()
	if err != nil {
		slog.Error("config load failed", "error", err)
		os.Exit(1)
	}
	games, err := config.LoadGames(cfg.GamesConfigPath)
	if err != nil {
		slog.Error("games yaml load failed", "error", err)
		os.Exit(1)
	}
	slog.Info("games loaded", "count", len(games.All()))

	// === 3. DB ===
	rootCtx, rootCancel := context.WithCancel(context.Background())
	defer rootCancel()

	pool, err := db.NewPool(rootCtx, db.PoolConfig{
		DatabaseURL: cfg.DatabaseURL,
		MaxConns:    cfg.DBMaxConns,
		MinConns:    cfg.DBMinConns,
	})
	if err != nil {
		slog.Error("db pool init failed", "error", err)
		os.Exit(1)
	}
	defer pool.Close()
	go db.StartMetricsLoop(rootCtx, pool, 5*time.Second)

	// Auto-migrate (goose 라이브러리)
	if cfg.AutoMigrate {
		if err := migrate.Up(rootCtx, pool, cfg.MigrationsPath); err != nil {
			slog.Error("auto-migrate failed", "error", err)
			os.Exit(1)
		}
	} else {
		slog.Info("AUTO_MIGRATE=false — 마이그레이션 건너뜀")
	}

	store := storage.New(pool)

	// Iter UI v1.3: yaml → DB sync (games 테이블 비어있으면 FK 에러)
	for _, g := range games.All() {
		if err := store.UpsertGame(rootCtx, storage.UpsertGameParams{
			ID:            g.ID,
			Title:         g.Title,
			CreatorID:     g.CreatorID,
			TimeLimitSec:  int32(g.TimeLimitSec),
			Tags:          g.Tags,
			BundleURL:     g.BundleURL,
			BundleVersion: g.BundleVersion,
			BundleHash:    g.BundleHash,
		}); err != nil {
			slog.Warn("games yaml→DB upsert failed", "id", g.ID, "error", err)
		}
	}
	slog.Info("games synced to DB", "count", len(games.All()))

	// === 4. Redis ===
	redisOpts, err := redis.ParseURL(cfg.RedisURL)
	if err != nil {
		slog.Error("redis url parse failed", "error", err)
		os.Exit(1)
	}
	rdb := redis.NewClient(redisOpts)
	defer rdb.Close()
	if err := rdb.Ping(rootCtx).Err(); err != nil {
		slog.Warn("redis ping failed (continuing)", "error", err)
	}

	// === 5. 도메인 컴포넌트 (DI) ===
	jwtIssuer := auth.NewJWTIssuer(cfg.JWTSecret, cfg.JWTTTL)
	authSvc := auth.NewService(store, jwtIssuer)
	authHandler := auth.NewHandler(authSvc)
	authMW := auth.RequireAuth(jwtIssuer)

	gameSvc := game.NewService(games, store)
	gameHandler := game.NewHandler(gameSvc)

	hmacVerifier := hmacpkg.NewVerifier(cfg.HMACBaseKey, cfg.BuildGUID, cfg.HMACReplayWindow)
	limiter := ratelimit.NewRedisLimiter(rdb)
	validator := anticheat.NewValidator(hmacVerifier, games, limiter)

	rankingSvc := ranking.NewService(store)
	rankingWorker := ranking.NewWorker(store, 500, 3)
	rankingWorker.Start(rootCtx)
	rankingHandler := ranking.NewHandler(rankingSvc)

	analyticsWorker := analytics.NewWorker(store, 1000, 5, 100, 5*time.Second)
	analyticsWorker.Start(rootCtx)
	analyticsHandler := analytics.NewHandler(analyticsWorker)

	// Iter 4a: 운영툴 admin
	adminIssuer := admin.NewJWTIssuer(cfg.AdminJWTSecret, cfg.AdminJWTTTL)
	adminHandler := admin.NewHandler(store, adminIssuer)
	admin.BootstrapAdmin(rootCtx, store, cfg.AdminBootstrapLogin, cfg.AdminBootstrapPassword)

	// Iter 3: profile / missions / share
	profileHandler := profile.NewHandler(store)
	missionsSvc := missions.NewService(store)
	missionsHandler := missions.NewHandler(missionsSvc)
	shareSvc := share.NewService(store)
	shareHandler := share.NewHandler(shareSvc)

	recommender := session.NewRecommender(games, store)
	sessionSvc := session.NewService(store, recommender, validator, rankingWorker, missionsSvc)
	sessionHandler := session.NewHandler(sessionSvc)

	// Purchase: 환경변수 분기 (Mock vs Real)
	var verifier playstore.Verifier
	if cfg.DevMockReceipt {
		slog.Warn("DEV_MOCK_RECEIPT=true — playstore Mock 사용 (개발 전용)")
		verifier = playstore.Mock{}
	} else {
		slog.Info("playstore Real verifier 사용")
		verifier = &playstore.Real{PackageName: "com.shortgeta.app"}
	}
	purchaseSvc := purchase.NewService(store, verifier)
	purchaseHandler := purchase.NewHandler(purchaseSvc)

	// === 6. Fiber ===
	app := fiber.New(fiber.Config{
		DisableStartupMessage: true,
		AppName:               "shortgeta-server",
		ReadTimeout:           10 * time.Second,
		WriteTimeout:          10 * time.Second,
	})
	app.Use(recover.New())
	// Iter 4a: 운영툴 (Next.js dev) 용 CORS — dev 한정 와일드카드
	app.Use(cors.New(cors.Config{
		AllowOrigins: "*",
		AllowHeaders: "Origin, Content-Type, Accept, Authorization",
		AllowMethods: "GET,POST,PUT,PATCH,DELETE,OPTIONS",
	}))

	// Prometheus 미들웨어
	prom := fiberprometheus.New("shortgeta_server")
	prom.RegisterAt(app, "/metrics")
	app.Use(prom.Middleware)

	// 헬스체크
	app.Get("/health", func(c *fiber.Ctx) error {
		return c.JSON(fiber.Map{"status": "ok"})
	})
	app.Get("/ready", func(c *fiber.Ctx) error {
		ctx, cancel := context.WithTimeout(c.UserContext(), 2*time.Second)
		defer cancel()
		if err := store.Ping(ctx); err != nil {
			return fiber.NewError(fiber.StatusServiceUnavailable, "db not ready")
		}
		return c.JSON(fiber.Map{"status": "ready"})
	})

	// v1 라우트
	v1 := app.Group("/v1")
	authHandler.Register(v1)
	gameHandler.Register(v1, authMW)
	sessionHandler.Register(v1, authMW)
	rankingHandler.Register(v1, authMW)
	analyticsHandler.Register(v1, authMW)
	purchaseHandler.Register(v1, authMW)
	profileHandler.Register(v1, authMW)
	adminHandler.Register(v1)
	missionsHandler.Register(v1, authMW)
	shareHandler.Register(v1, authMW)

	// Iter 2C'': Addressables remote bundle 정적 파일 (인증 없음)
	bundlesHandler := bundles.NewHandler(cfg.BundlesDir)
	bundlesHandler.Register(v1)
	slog.Info("bundles handler registered", "dir", cfg.BundlesDir)

	// === 7. 시작 ===
	go func() {
		addr := ":" + cfg.Port
		slog.Info("server starting", "addr", addr)
		if err := app.Listen(addr); err != nil && !errors.Is(err, context.Canceled) {
			slog.Error("listen error", "error", err)
		}
	}()

	// === 8. Graceful Shutdown ===
	quit := make(chan os.Signal, 1)
	signal.Notify(quit, syscall.SIGINT, syscall.SIGTERM)
	<-quit
	slog.Info("shutdown signal received, draining...")

	shutdownCtx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
	defer cancel()

	if err := app.ShutdownWithContext(shutdownCtx); err != nil {
		slog.Error("fiber shutdown error", "error", err)
	}
	analyticsWorker.Shutdown(shutdownCtx)
	rankingWorker.Shutdown(shutdownCtx)
	rootCancel()
	slog.Info("server shutdown complete")
}

func parseLogLevel(s string) slog.Level {
	switch s {
	case "debug":
		return slog.LevelDebug
	case "warn":
		return slog.LevelWarn
	case "error":
		return slog.LevelError
	default:
		return slog.LevelInfo
	}
}
