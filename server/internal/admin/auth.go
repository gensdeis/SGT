// Package admin 은 운영툴 (ops-tool) 백엔드.
// 일반 유저용 auth 와 별도의 admin JWT + sha256+salt 비밀번호.
//
// NOTE: bcrypt 대신 sha256+salt 사용 (의존성 추가 회피).
// 운영툴은 내부 사용자 1~3인 가정 + IP whitelist + Traefik basic auth 가
// 외부 노출 방어층. 정식 출시 전 bcrypt/argon2 로 전환 권장.
package admin

import (
	"crypto/rand"
	"crypto/sha256"
	"crypto/subtle"
	"encoding/hex"
	"errors"
	"strings"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"github.com/google/uuid"
)

// JWTIssuer 는 admin 토큰 발급/파싱.
type JWTIssuer struct {
	secret []byte
	ttl    time.Duration
}

func NewJWTIssuer(secret string, ttl time.Duration) *JWTIssuer {
	return &JWTIssuer{secret: []byte(secret), ttl: ttl}
}

type Claims struct {
	AdminID uuid.UUID `json:"admin_id"`
	Login   string    `json:"login"`
	Role    string    `json:"role"`
	jwt.RegisteredClaims
}

func (j *JWTIssuer) Issue(adminID uuid.UUID, login, role string) (string, error) {
	now := time.Now()
	c := Claims{
		AdminID: adminID,
		Login:   login,
		Role:    role,
		RegisteredClaims: jwt.RegisteredClaims{
			IssuedAt:  jwt.NewNumericDate(now),
			ExpiresAt: jwt.NewNumericDate(now.Add(j.ttl)),
			Issuer:    "shortgeta-admin",
		},
	}
	tk := jwt.NewWithClaims(jwt.SigningMethodHS256, c)
	return tk.SignedString(j.secret)
}

func (j *JWTIssuer) Parse(tokenStr string) (Claims, error) {
	var c Claims
	tk, err := jwt.ParseWithClaims(tokenStr, &c, func(t *jwt.Token) (interface{}, error) {
		if _, ok := t.Method.(*jwt.SigningMethodHMAC); !ok {
			return nil, errors.New("unexpected signing method")
		}
		return j.secret, nil
	})
	if err != nil || !tk.Valid {
		return Claims{}, errors.New("invalid admin token")
	}
	return c, nil
}

// HashPassword 는 sha256(salt|pw) — "salt$hex" 형식 반환.
func HashPassword(pw string) (string, error) {
	salt := make([]byte, 16)
	if _, err := rand.Read(salt); err != nil {
		return "", err
	}
	h := sha256.Sum256(append(salt, []byte(pw)...))
	return hex.EncodeToString(salt) + "$" + hex.EncodeToString(h[:]), nil
}

// CheckPassword 는 stored "salt$hash" vs 평문 비교.
func CheckPassword(stored, pw string) bool {
	parts := strings.SplitN(stored, "$", 2)
	if len(parts) != 2 {
		return false
	}
	salt, err := hex.DecodeString(parts[0])
	if err != nil {
		return false
	}
	want, err := hex.DecodeString(parts[1])
	if err != nil {
		return false
	}
	got := sha256.Sum256(append(salt, []byte(pw)...))
	return subtle.ConstantTimeCompare(got[:], want) == 1
}
