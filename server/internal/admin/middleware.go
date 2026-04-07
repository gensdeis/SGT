package admin

import (
	"strings"

	"github.com/gofiber/fiber/v2"
	"github.com/google/uuid"
)

const claimsKey = "admin_claims"

// RequireAdmin 은 Authorization: Bearer <admin jwt> 검증.
func RequireAdmin(issuer *JWTIssuer) fiber.Handler {
	return func(c *fiber.Ctx) error {
		h := c.Get("Authorization")
		if h == "" || !strings.HasPrefix(h, "Bearer ") {
			return fiber.NewError(fiber.StatusUnauthorized, "missing admin bearer")
		}
		claims, err := issuer.Parse(strings.TrimPrefix(h, "Bearer "))
		if err != nil {
			return fiber.NewError(fiber.StatusUnauthorized, err.Error())
		}
		c.Locals(claimsKey, claims)
		return c.Next()
	}
}

func ClaimsFrom(c *fiber.Ctx) (Claims, bool) {
	v := c.Locals(claimsKey)
	cl, ok := v.(Claims)
	return cl, ok
}

// AdminIDFrom 은 핸들러에서 admin uuid 추출.
func AdminIDFrom(c *fiber.Ctx) (uuid.UUID, bool) {
	cl, ok := ClaimsFrom(c)
	if !ok {
		return uuid.Nil, false
	}
	return cl.AdminID, true
}
