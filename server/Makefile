.PHONY: run build test test-race vet lint docker compose-up compose-down sqlc migrate-up migrate-down clean

run:
	go run ./cmd/api

build:
	CGO_ENABLED=0 go build -o bin/api ./cmd/api
	CGO_ENABLED=0 go build -o bin/worker ./cmd/worker

test:
	go test ./...

test-race:
	go test ./... -race -coverprofile=coverage.out

vet:
	go vet ./...

lint: vet
	@echo "Add staticcheck/golangci-lint as needed"

docker:
	docker build -t shortgeta-server:dev .

compose-up:
	docker compose up -d

compose-down:
	docker compose down

sqlc:
	sqlc generate

migrate-up:
	goose -dir db/migrations postgres "$$DATABASE_URL" up

migrate-down:
	goose -dir db/migrations postgres "$$DATABASE_URL" down

clean:
	rm -rf bin/ coverage.out coverage.html
