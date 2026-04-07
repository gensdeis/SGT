package missions

import (
	"github.com/gofiber/fiber/v2"

	"github.com/gensdeis/SGT/server/internal/auth"
)

type Handler struct {
	svc *Service
}

func NewHandler(svc *Service) *Handler { return &Handler{svc: svc} }

func (h *Handler) Register(r fiber.Router, authMW fiber.Handler) {
	g := r.Group("/missions")
	if authMW != nil {
		g.Use(authMW)
	}
	g.Get("/today", h.today)
	g.Post("/claim", h.claim)
}

type missionView struct {
	MissionID   string `json:"mission_id"`
	Title       string `json:"title"`
	Progress    int32  `json:"progress"`
	Target      int32  `json:"target"`
	Reward      int32  `json:"reward"`
	Completed   bool   `json:"completed"`
	Claimed     bool   `json:"claimed"`
}

func (h *Handler) today(c *fiber.Ctx) error {
	uid, ok := auth.UserIDFrom(c)
	if !ok {
		return fiber.NewError(fiber.StatusUnauthorized, "no user")
	}
	rows, err := h.svc.EnsureToday(c.UserContext(), uid)
	if err != nil {
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	out := make([]missionView, 0, len(rows))
	for _, r := range rows {
		def, _ := FindByID(r.MissionID)
		out = append(out, missionView{
			MissionID: r.MissionID,
			Title:     def.Title,
			Progress:  r.Progress,
			Target:    r.Target,
			Reward:    def.Reward,
			Completed: r.CompletedAt != nil,
			Claimed:   r.ClaimedAt != nil,
		})
	}
	return c.JSON(fiber.Map{"missions": out})
}

type claimReq struct {
	MissionID string `json:"mission_id"`
}

func (h *Handler) claim(c *fiber.Ctx) error {
	uid, ok := auth.UserIDFrom(c)
	if !ok {
		return fiber.NewError(fiber.StatusUnauthorized, "no user")
	}
	var req claimReq
	if err := c.BodyParser(&req); err != nil || req.MissionID == "" {
		return fiber.NewError(fiber.StatusBadRequest, "mission_id required")
	}
	res, err := h.svc.Claim(c.UserContext(), uid, req.MissionID)
	if err != nil {
		return fiber.NewError(fiber.StatusBadRequest, err.Error())
	}
	return c.JSON(res)
}
