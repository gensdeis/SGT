package game

import (
	"errors"
	"strings"

	"github.com/gofiber/fiber/v2"
)

// Handler 는 /v1/games* 라우트.
type Handler struct {
	svc *Service
}

func NewHandler(svc *Service) *Handler {
	return &Handler{svc: svc}
}

// Register 는 라우트를 등록한다. authMW 가 nil 이 아니면 보호된다.
func (h *Handler) Register(r fiber.Router, authMW fiber.Handler) {
	g := r.Group("/games")
	if authMW != nil {
		g.Use(authMW)
	}
	g.Get("", h.list)
	g.Get("/:id", h.get)
}

func (h *Handler) list(c *fiber.Ctx) error {
	tagsParam := c.Query("tags")
	var tags []string
	if tagsParam != "" {
		tags = strings.Split(tagsParam, ",")
	}
	views, err := h.svc.List(c.UserContext(), tags)
	if err != nil {
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	return c.JSON(fiber.Map{"games": views})
}

func (h *Handler) get(c *fiber.Ctx) error {
	id := c.Params("id")
	v, err := h.svc.Get(c.UserContext(), id)
	if err != nil {
		if errors.Is(err, ErrNotFound) {
			return fiber.NewError(fiber.StatusNotFound, "game not found")
		}
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	return c.JSON(v)
}
