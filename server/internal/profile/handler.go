// Package profile 은 /v1/me 엔드포인트.
package profile

import (
	"github.com/gofiber/fiber/v2"

	"github.com/gensdeis/SGT/server/internal/auth"
	"github.com/gensdeis/SGT/server/internal/storage"
)

type Handler struct {
	store *storage.Store
}

func NewHandler(store *storage.Store) *Handler {
	return &Handler{store: store}
}

func (h *Handler) Register(r fiber.Router, authMW fiber.Handler) {
	g := r.Group("/me")
	if authMW != nil {
		g.Use(authMW)
	}
	g.Get("", h.get)
	g.Patch("", h.patch)
	g.Post("", h.patch) // 클라가 PATCH helper 가 없어 POST 도 허용
}

type meResponse struct {
	UserID   string `json:"user_id"`
	Nickname string `json:"nickname"`
	AvatarID int32  `json:"avatar_id"`
	Coins    int32  `json:"coins"`
}

func (h *Handler) get(c *fiber.Ctx) error {
	uid, ok := auth.UserIDFrom(c)
	if !ok {
		return fiber.NewError(fiber.StatusUnauthorized, "no user")
	}
	p, err := h.store.GetProfile(c.UserContext(), uid)
	if err != nil {
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	return c.JSON(meResponse{
		UserID: p.UserID.String(), Nickname: p.Nickname, AvatarID: p.AvatarID, Coins: p.Coins,
	})
}

type patchReq struct {
	Nickname string `json:"nickname"`
	AvatarID int32  `json:"avatar_id"`
}

func (h *Handler) patch(c *fiber.Ctx) error {
	uid, ok := auth.UserIDFrom(c)
	if !ok {
		return fiber.NewError(fiber.StatusUnauthorized, "no user")
	}
	var req patchReq
	if err := c.BodyParser(&req); err != nil {
		return fiber.NewError(fiber.StatusBadRequest, "invalid body")
	}
	if len(req.Nickname) > 32 {
		return fiber.NewError(fiber.StatusBadRequest, "nickname too long")
	}
	if err := h.store.UpdateProfile(c.UserContext(), uid, req.Nickname, req.AvatarID); err != nil {
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	return c.JSON(fiber.Map{"ok": true})
}
