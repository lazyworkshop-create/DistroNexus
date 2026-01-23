package logic

import (
	"bufio"
	"context"
	"fmt"
	"os/exec"
	"path/filepath"
	"strings"
	"syscall"
)

// RunInstallScript executes the PowerShell installation script
func RunInstallScript(ctx context.Context, projectRoot string, familyName string, versionName string, distroName string, installPath string, user string, pass string, onLog func(string), onFinish func(error)) {
	go func() {
		scriptPath := filepath.Join(projectRoot, "scripts", "install_wsl_custom.ps1")

		// Construct the PowerShell command
		// We use -File to run the script file directly with parameters
		args := []string{
			"-NoProfile",
			"-ExecutionPolicy", "Bypass",
			"-File", scriptPath,
			"-SelectFamily", familyName,
			"-SelectVersion", versionName,
			"-DistroName", distroName,
			"-InstallPath", installPath,
			"-user", user,
			"-pass", pass,
		}

		cmd := exec.CommandContext(ctx, "powershell.exe", args...)
		prepareCmd(cmd)

		// Setup logging pipes
		stdout, err := cmd.StdoutPipe()
		if err != nil {
			onFinish(fmt.Errorf("failed to get stdout: %w", err))
			return
		}
		stderr, err := cmd.StderrPipe()
		if err != nil {
			onFinish(fmt.Errorf("failed to get stderr: %w", err))
			return
		}

		// Start the process
		if err := cmd.Start(); err != nil {
			// Fallback: Try "pwsh" (PowerShell Core) if "powershell" fails
			cmd = exec.CommandContext(ctx, "pwsh", args...)
			prepareCmd(cmd)
			stdout, _ = cmd.StdoutPipe()
			stderr, _ = cmd.StderrPipe()
			if err := cmd.Start(); err != nil {
				onFinish(fmt.Errorf("failed to start PowerShell: %w", err))
				return
			}
		}

		onLog(fmt.Sprintf("--- Starting Installation: %s ---\n", distroName))
		onLog(fmt.Sprintf("Command: %s %s\n", cmd.Path, strings.Join(args, " ")))

		// Read output asynchronously
		go scanOutput(stdout, onLog)
		go scanOutput(stderr, onLog)

		// Wait for completion
		err = cmd.Wait()

		// Check exit code
		if err != nil {
			if exitErr, ok := err.(*exec.ExitError); ok {
				// The program has exited with an exit code != 0
				if status, ok := exitErr.Sys().(syscall.WaitStatus); ok {
					onLog(fmt.Sprintf("\nProcess finished with exit code: %d\n", status.ExitStatus()))
				}
			}
			onFinish(err)
		} else {
			onLog("\n--- Installation Completed Successfully! ---\n")
			onFinish(nil)
		}
	}()
}

func scanOutput(reader interface {
	Read(p []byte) (n int, err error)
}, logFunc func(string)) {
	scanner := bufio.NewScanner(reader.(interface{ Read([]byte) (int, error) }))
	for scanner.Scan() {
		text := scanner.Text()
		logFunc(text + "\n")
	}
}
