package anticheat

import "os"

// osWrite 는 테스트 헬퍼. validator_test.go 에서 사용.
func osWrite(path string, data []byte) error {
	return os.WriteFile(path, data, 0o644)
}
