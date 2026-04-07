// Package bundles 는 Addressables remote bundle 정적 파일 서빙을 담당한다.
//
// 라우트: GET /v1/bundles/*filepath
// 설정: BUNDLES_DIR 환경변수 (기본 ./bundles)
//
// 보안:
//   - filepath 의 ".." 시퀀스 거부
//   - filepath.Clean 후 baseDir prefix 검사
//   - 디렉토리 listing 차단
//   - 심볼릭 링크 추적 안 함
package bundles

import (
	"log/slog"
	"os"
	"path/filepath"
	"strings"

	"github.com/gofiber/fiber/v2"
)

// Handler 는 정적 파일 핸들러.
type Handler struct {
	baseDir string
}

func NewHandler(baseDir string) *Handler {
	abs, err := filepath.Abs(baseDir)
	if err != nil {
		abs = baseDir
	}
	return &Handler{baseDir: abs}
}

// Register 는 Fiber 라우터에 GET /v1/bundles/* 등록.
func (h *Handler) Register(r fiber.Router) {
	r.Get("/bundles/*", h.serve)
}

func (h *Handler) serve(c *fiber.Ctx) error {
	rel := c.Params("*")
	if rel == "" {
		return fiber.NewError(fiber.StatusNotFound, "no path")
	}

	// 보안: ".." 차단
	if strings.Contains(rel, "..") {
		slog.Warn("bundles: path traversal attempt", "raw", rel, "ip", c.IP())
		return fiber.NewError(fiber.StatusBadRequest, "invalid path")
	}

	// Clean + 결합
	cleaned := filepath.Clean(rel)
	full := filepath.Join(h.baseDir, cleaned)

	// 결과가 baseDir 안인지 재확인
	absFull, err := filepath.Abs(full)
	if err != nil {
		return fiber.NewError(fiber.StatusBadRequest, "invalid path")
	}
	if !strings.HasPrefix(absFull, h.baseDir+string(os.PathSeparator)) && absFull != h.baseDir {
		slog.Warn("bundles: outside base", "abs", absFull, "base", h.baseDir)
		return fiber.NewError(fiber.StatusForbidden, "forbidden")
	}

	// 파일 존재 확인 + 디렉토리 차단
	info, err := os.Stat(absFull)
	if err != nil {
		if os.IsNotExist(err) {
			return fiber.NewError(fiber.StatusNotFound, "not found")
		}
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	if info.IsDir() {
		return fiber.NewError(fiber.StatusForbidden, "directory listing disabled")
	}

	// 캐시: bundle 은 immutable 가정 (Iter 2C''' 에서 hash 검증)
	c.Set("Cache-Control", "public, max-age=31536000, immutable")
	return c.SendFile(absFull, false)
}
