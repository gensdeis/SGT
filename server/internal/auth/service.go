package auth

import (
	"context"
	"errors"

	"github.com/gensdeis/SGT/server/internal/storage"
	"github.com/google/uuid"
)

// Service 는 디바이스 로그인 + JWT 발급 흐름을 캡슐화한다.
type Service struct {
	store  *storage.Store
	issuer *JWTIssuer
}

// NewService 는 의존성을 주입받는다.
func NewService(store *storage.Store, issuer *JWTIssuer) *Service {
	return &Service{store: store, issuer: issuer}
}

// LoginResult 는 LoginByDevice 응답.
type LoginResult struct {
	UserID    uuid.UUID `json:"user_id"`
	Token     string    `json:"token"`
	AdRemoved bool      `json:"ad_removed"`
}

// LoginByDevice 는 device_id 로 사용자를 조회/생성하고 JWT 를 발급한다.
func (s *Service) LoginByDevice(ctx context.Context, deviceID string) (LoginResult, error) {
	if deviceID == "" {
		return LoginResult{}, errors.New("auth: device_id required")
	}

	user, err := s.store.GetUserByDeviceID(ctx, deviceID)
	if err != nil && !errors.Is(err, storage.ErrNotFound) {
		return LoginResult{}, err
	}
	if errors.Is(err, storage.ErrNotFound) {
		user, err = s.store.CreateUser(ctx, deviceID)
		if err != nil {
			return LoginResult{}, err
		}
	} else {
		_ = s.store.UpdateLastSeen(ctx, user.ID)
	}

	// 광고제거 영수증 보유 여부 (JWT 클레임에 박아 클라이언트 캐싱 가능)
	adRemoved, _ := s.store.HasAdRemoval(ctx, user.ID, "ad_removal")

	token, err := s.issuer.Issue(user.ID, adRemoved)
	if err != nil {
		return LoginResult{}, err
	}
	return LoginResult{UserID: user.ID, Token: token, AdRemoved: adRemoved}, nil
}
