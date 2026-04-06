// Package auth 는 디바이스 토큰 → JWT 발급 + 검증을 담당한다.
package auth

import (
	"errors"
	"fmt"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"github.com/google/uuid"
)

// JWT 클레임 — sub: user uuid, ad_removed: 광고제거 구매 여부.
type Claims struct {
	UserID    uuid.UUID `json:"sub"`
	AdRemoved bool      `json:"ad_removed,omitempty"`
	jwt.RegisteredClaims
}

// JWTIssuer 는 토큰 발급/파싱.
type JWTIssuer struct {
	secret []byte
	ttl    time.Duration
}

// NewJWTIssuer 는 HS256 발급기를 만든다.
func NewJWTIssuer(secret string, ttl time.Duration) *JWTIssuer {
	return &JWTIssuer{secret: []byte(secret), ttl: ttl}
}

// Issue 는 새 토큰을 발급한다.
func (j *JWTIssuer) Issue(userID uuid.UUID, adRemoved bool) (string, error) {
	now := time.Now()
	claims := Claims{
		UserID:    userID,
		AdRemoved: adRemoved,
		RegisteredClaims: jwt.RegisteredClaims{
			Subject:   userID.String(),
			IssuedAt:  jwt.NewNumericDate(now),
			ExpiresAt: jwt.NewNumericDate(now.Add(j.ttl)),
			Issuer:    "shortgeta",
		},
	}
	tok := jwt.NewWithClaims(jwt.SigningMethodHS256, claims)
	return tok.SignedString(j.secret)
}

// 파싱 에러.
var (
	ErrTokenInvalid = errors.New("auth: token invalid")
	ErrTokenExpired = errors.New("auth: token expired")
)

// Parse 는 토큰을 검증·파싱한다.
func (j *JWTIssuer) Parse(tokenStr string) (Claims, error) {
	var claims Claims
	tok, err := jwt.ParseWithClaims(tokenStr, &claims, func(t *jwt.Token) (interface{}, error) {
		if _, ok := t.Method.(*jwt.SigningMethodHMAC); !ok {
			return nil, fmt.Errorf("unexpected signing method: %v", t.Header["alg"])
		}
		return j.secret, nil
	})
	if err != nil {
		if errors.Is(err, jwt.ErrTokenExpired) {
			return claims, ErrTokenExpired
		}
		return claims, ErrTokenInvalid
	}
	if !tok.Valid {
		return claims, ErrTokenInvalid
	}
	return claims, nil
}
