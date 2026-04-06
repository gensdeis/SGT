package session

import (
	"github.com/gensdeis/SGT/server/internal/auth"
	"github.com/gofiber/fiber/v2"
	"github.com/google/uuid"
)

// Handler 는 /v1/sessions* 라우트.
type Handler struct {
	svc *Service
}

func NewHandler(svc *Service) *Handler {
	return &Handler{svc: svc}
}

// Register 등록 (auth 보호).
func (h *Handler) Register(r fiber.Router, authMW fiber.Handler) {
	g := r.Group("/sessions")
	if authMW != nil {
		g.Use(authMW)
	}
	g.Post("", h.start)
	g.Post("/:id/end", h.end)
}

func (h *Handler) start(c *fiber.Ctx) error {
	uid, ok := auth.UserIDFrom(c)
	if !ok {
		return fiber.NewError(fiber.StatusUnauthorized, "no user")
	}
	res, err := h.svc.Start(c.UserContext(), uid)
	if err != nil {
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	return c.JSON(res)
}

type endReq struct {
	Scores []EndScore `json:"scores"`
}

func (h *Handler) end(c *fiber.Ctx) error {
	uid, ok := auth.UserIDFrom(c)
	if !ok {
		return fiber.NewError(fiber.StatusUnauthorized, "no user")
	}
	sessID, err := uuid.Parse(c.Params("id"))
	if err != nil {
		return fiber.NewError(fiber.StatusBadRequest, "invalid session id")
	}
	var req endReq
	if err := c.BodyParser(&req); err != nil {
		return fiber.NewError(fiber.StatusBadRequest, "invalid body")
	}
	res, err := h.svc.End(c.UserContext(), uid, sessID, req.Scores)
	if err != nil {
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	return c.JSON(res)
}
