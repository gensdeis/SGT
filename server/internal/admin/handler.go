package admin

import (
	"context"
	"log/slog"

	"github.com/gofiber/fiber/v2"

	"github.com/gensdeis/SGT/server/internal/storage"
)

type Handler struct {
	store  *storage.Store
	issuer *JWTIssuer
}

func NewHandler(store *storage.Store, issuer *JWTIssuer) *Handler {
	return &Handler{store: store, issuer: issuer}
}

// Register 는 /v1/admin/* 등록. login 만 public, 나머지는 RequireAdmin.
func (h *Handler) Register(r fiber.Router) {
	g := r.Group("/admin")
	g.Post("/login", h.login)

	auth := g.Group("", RequireAdmin(h.issuer))
	auth.Get("/me", h.me)
	auth.Get("/users", h.searchUsers)
}

type loginReq struct {
	Login    string `json:"login"`
	Password string `json:"password"`
}

type loginResp struct {
	Token string `json:"token"`
	Login string `json:"login"`
	Role  string `json:"role"`
}

func (h *Handler) login(c *fiber.Ctx) error {
	var req loginReq
	if err := c.BodyParser(&req); err != nil || req.Login == "" || req.Password == "" {
		return fiber.NewError(fiber.StatusBadRequest, "login + password required")
	}
	a, err := h.store.GetAdminByLogin(c.UserContext(), req.Login)
	if err != nil {
		return fiber.NewError(fiber.StatusUnauthorized, "invalid credentials")
	}
	if !CheckPassword(a.PasswordHash, req.Password) {
		return fiber.NewError(fiber.StatusUnauthorized, "invalid credentials")
	}
	tok, err := h.issuer.Issue(a.ID, a.Login, a.Role)
	if err != nil {
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	return c.JSON(loginResp{Token: tok, Login: a.Login, Role: a.Role})
}

func (h *Handler) me(c *fiber.Ctx) error {
	cl, _ := ClaimsFrom(c)
	return c.JSON(fiber.Map{
		"admin_id": cl.AdminID,
		"login":    cl.Login,
		"role":     cl.Role,
	})
}

func (h *Handler) searchUsers(c *fiber.Ctx) error {
	q := c.Query("q")
	rows, err := h.store.SearchUsers(c.UserContext(), q, 50)
	if err != nil {
		return fiber.NewError(fiber.StatusInternalServerError, err.Error())
	}
	return c.JSON(fiber.Map{"users": rows})
}

// BootstrapAdmin 은 ADMIN_BOOTSTRAP_LOGIN/PASSWORD 가 있으면 idempotent insert.
func BootstrapAdmin(ctx context.Context, store *storage.Store, login, password string) {
	if login == "" || password == "" {
		slog.Info("admin bootstrap skipped — env vars empty")
		return
	}
	hash, err := HashPassword(password)
	if err != nil {
		slog.Warn("admin bootstrap hash failed", "error", err)
		return
	}
	if err := store.EnsureAdmin(ctx, login, hash, "admin"); err != nil {
		slog.Warn("admin bootstrap insert failed", "error", err)
		return
	}
	slog.Info("admin bootstrap ensured", "login", login)
}
