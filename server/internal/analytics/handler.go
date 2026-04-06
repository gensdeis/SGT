package analytics

import (
	"encoding/json"

	"github.com/gensdeis/SGT/server/internal/auth"
	"github.com/gofiber/fiber/v2"
)

// Handler 는 /v1/analytics/event 라우트.
type Handler struct {
	worker *Worker
}

func NewHandler(w *Worker) *Handler {
	return &Handler{worker: w}
}

// Register 등록.
func (h *Handler) Register(r fiber.Router, authMW fiber.Handler) {
	g := r.Group("/analytics")
	if authMW != nil {
		g.Use(authMW)
	}
	g.Post("/event", h.event)
}

type eventReq struct {
	GameID    string         `json:"game_id"`
	EventType string         `json:"event_type"`
	Payload   map[string]any `json:"payload"`
}

func (h *Handler) event(c *fiber.Ctx) error {
	uid, ok := auth.UserIDFrom(c)
	if !ok {
		return fiber.NewError(fiber.StatusUnauthorized, "no user")
	}
	var req eventReq
	if err := c.BodyParser(&req); err != nil {
		return fiber.NewError(fiber.StatusBadRequest, "invalid body")
	}
	if req.GameID == "" || req.EventType == "" {
		return fiber.NewError(fiber.StatusBadRequest, "game_id and event_type required")
	}
	payload := []byte("{}")
	if req.Payload != nil {
		b, _ := json.Marshal(req.Payload)
		if len(b) > 0 {
			payload = b
		}
	}
	h.worker.Enqueue(Event{
		UserID:    uid,
		GameID:    req.GameID,
		EventType: req.EventType,
		Payload:   payload,
	})
	return c.SendStatus(fiber.StatusAccepted)
}
