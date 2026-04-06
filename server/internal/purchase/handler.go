package purchase

import (
	"github.com/gensdeis/SGT/server/internal/auth"
	"github.com/gofiber/fiber/v2"
)

// Handler 는 /v1/purchases/* 라우트.
type Handler struct {
	svc *Service
}

func NewHandler(svc *Service) *Handler {
	return &Handler{svc: svc}
}

// Register 등록 (auth 보호).
func (h *Handler) Register(r fiber.Router, authMW fiber.Handler) {
	g := r.Group("/purchases")
	if authMW != nil {
		g.Use(authMW)
	}
	g.Post("/verify", h.verify)
}

type verifyReq struct {
	ProductID string `json:"product_id"`
	Token     string `json:"token"`
}

func (h *Handler) verify(c *fiber.Ctx) error {
	uid, ok := auth.UserIDFrom(c)
	if !ok {
		return fiber.NewError(fiber.StatusUnauthorized, "no user")
	}
	var req verifyReq
	if err := c.BodyParser(&req); err != nil {
		return fiber.NewError(fiber.StatusBadRequest, "invalid body")
	}
	res, err := h.svc.Verify(c.UserContext(), uid, req.ProductID, req.Token)
	if err != nil {
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	return c.JSON(res)
}
