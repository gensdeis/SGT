package share

import (
	"github.com/gofiber/fiber/v2"

	"github.com/gensdeis/SGT/server/internal/auth"
)

type Handler struct {
	svc *Service
}

func NewHandler(svc *Service) *Handler { return &Handler{svc: svc} }

func (h *Handler) Register(r fiber.Router, authMW fiber.Handler) {
	g := r.Group("/share")
	if authMW != nil {
		g.Use(authMW)
	}
	g.Post("/claim", h.claim)
}

type claimReq struct {
	Platform     string `json:"platform"`
	HighlightTag string `json:"highlight_tag"`
}

func (h *Handler) claim(c *fiber.Ctx) error {
	uid, ok := auth.UserIDFrom(c)
	if !ok {
		return fiber.NewError(fiber.StatusUnauthorized, "no user")
	}
	var req claimReq
	_ = c.BodyParser(&req) // 본 iter 는 필드 미사용 (logging only)
	res, err := h.svc.Claim(c.UserContext(), uid)
	if err != nil {
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	return c.JSON(res)
}
