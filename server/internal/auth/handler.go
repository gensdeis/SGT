package auth

import (
	"github.com/gofiber/fiber/v2"
)

// Handler 는 /v1/auth/* 라우트를 다룬다.
type Handler struct {
	svc *Service
}

func NewHandler(svc *Service) *Handler {
	return &Handler{svc: svc}
}

// Register 는 라우터에 엔드포인트를 등록한다.
func (h *Handler) Register(r fiber.Router) {
	r.Post("/auth/device", h.deviceLogin)
}

type deviceLoginReq struct {
	DeviceID string `json:"device_id"`
}

func (h *Handler) deviceLogin(c *fiber.Ctx) error {
	var req deviceLoginReq
	if err := c.BodyParser(&req); err != nil {
		return fiber.NewError(fiber.StatusBadRequest, "invalid body")
	}
	if req.DeviceID == "" {
		return fiber.NewError(fiber.StatusBadRequest, "device_id required")
	}
	res, err := h.svc.LoginByDevice(c.UserContext(), req.DeviceID)
	if err != nil {
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	return c.JSON(res)
}
