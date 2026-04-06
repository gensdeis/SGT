package auth

import (
	"strings"

	"github.com/gofiber/fiber/v2"
	"github.com/google/uuid"
)

// userIDKey 는 fiber Locals 키.
const userIDKey = "user_id"
const claimsKey = "claims"

// RequireAuth 는 Authorization: Bearer <jwt> 헤더를 검증해 user_id 를 c.Locals 에 주입한다.
func RequireAuth(issuer *JWTIssuer) fiber.Handler {
	return func(c *fiber.Ctx) error {
		h := c.Get("Authorization")
		if h == "" || !strings.HasPrefix(h, "Bearer ") {
			return fiber.NewError(fiber.StatusUnauthorized, "missing bearer token")
		}
		tokenStr := strings.TrimPrefix(h, "Bearer ")
		claims, err := issuer.Parse(tokenStr)
		if err != nil {
			return fiber.NewError(fiber.StatusUnauthorized, err.Error())
		}
		c.Locals(userIDKey, claims.UserID)
		c.Locals(claimsKey, claims)
		return c.Next()
	}
}

// UserIDFrom 는 핸들러에서 인증된 user uuid 를 꺼낸다.
func UserIDFrom(c *fiber.Ctx) (uuid.UUID, bool) {
	v := c.Locals(userIDKey)
	id, ok := v.(uuid.UUID)
	return id, ok
}

// ClaimsFrom 은 토큰 클레임을 꺼낸다.
func ClaimsFrom(c *fiber.Ctx) (Claims, bool) {
	v := c.Locals(claimsKey)
	cl, ok := v.(Claims)
	return cl, ok
}
