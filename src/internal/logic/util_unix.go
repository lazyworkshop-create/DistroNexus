//go:build !windows

package logic

import (
	"os/exec"
)

func prepareCmd(cmd *exec.Cmd) {
	// No-op for non-Windows systems
}
