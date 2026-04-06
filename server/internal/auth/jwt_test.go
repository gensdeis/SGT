package auth

import (
	"errors"
	"testing"
	"time"

	"github.com/google/uuid"
)

func TestJWTIssuer_RoundTrip(t *testing.T) {
	j := NewJWTIssuer("super-secret", time.Hour)
	uid := uuid.New()
	tok, err := j.Issue(uid, true)
	if err != nil {
		t.Fatalf("issue: %v", err)
	}
	claims, err := j.Parse(tok)
	if err != nil {
		t.Fatalf("parse: %v", err)
	}
	if claims.UserID != uid {
		t.Fatalf("expected uid %v, got %v", uid, claims.UserID)
	}
	if !claims.AdRemoved {
		t.Fatal("expected ad_removed=true")
	}
}

func TestJWTIssuer_InvalidToken(t *testing.T) {
	j := NewJWTIssuer("secret", time.Hour)
	_, err := j.Parse("not.a.token")
	if !errors.Is(err, ErrTokenInvalid) {
		t.Fatalf("expected ErrTokenInvalid, got %v", err)
	}
}

func TestJWTIssuer_WrongSecret(t *testing.T) {
	a := NewJWTIssuer("a-secret", time.Hour)
	b := NewJWTIssuer("b-secret", time.Hour)
	tok, _ := a.Issue(uuid.New(), false)
	if _, err := b.Parse(tok); !errors.Is(err, ErrTokenInvalid) {
		t.Fatalf("expected ErrTokenInvalid, got %v", err)
	}
}

func TestJWTIssuer_Expired(t *testing.T) {
	j := NewJWTIssuer("secret", -time.Hour) // 이미 만료
	tok, _ := j.Issue(uuid.New(), false)
	_, err := j.Parse(tok)
	if !errors.Is(err, ErrTokenExpired) {
		t.Fatalf("expected ErrTokenExpired, got %v", err)
	}
}
