package ranking

import (
	"strconv"
	"time"

	"github.com/gofiber/fiber/v2"
)

// Handler 는 /v1/rankings/* 라우트.
type Handler struct {
	svc *Service
}

func NewHandler(svc *Service) *Handler {
	return &Handler{svc: svc}
}

// Register 는 라우트를 등록한다 (auth 보호).
func (h *Handler) Register(r fiber.Router, authMW fiber.Handler) {
	g := r.Group("/rankings")
	if authMW != nil {
		g.Use(authMW)
	}
	g.Get("/global", h.global)
	g.Get("/:gameId", h.byGame)
}

func (h *Handler) global(c *fiber.Ctx) error {
	limit := parseLimit(c.Query("limit"), 100)
	rows, err := h.svc.GlobalTop(c.UserContext(), time.Time{}, limit)
	if err != nil {
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	return c.JSON(fiber.Map{"rankings": rows})
}

func (h *Handler) byGame(c *fiber.Ctx) error {
	gameID := c.Params("gameId")
	limit := parseLimit(c.Query("limit"), 100)
	rows, err := h.svc.TopByGame(c.UserContext(), gameID, limit)
	if err != nil {
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	return c.JSON(fiber.Map{"game_id": gameID, "rankings": rows})
}

func parseLimit(s string, def int) int {
	if s == "" {
		return def
	}
	n, err := strconv.Atoi(s)
	if err != nil || n <= 0 || n > 1000 {
		return def
	}
	return n
}
