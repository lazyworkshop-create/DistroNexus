package logic

import (
	"bufio"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"strings"
	"syscall"
)

type WslInstance struct {
	Name        string `json:"Name"`
	BasePath    string `json:"BasePath"`
	State       string `json:"State"`
	WslVer      string `json:"WslVer"`
	Release     string `json:"Release,omitempty"`
	User        string `json:"User,omitempty"`
	InstallTime string `json:"InstallTime,omitempty"`
	DiskSize    string `json:"DiskSize,omitempty"`
}

// Check if a directory is empty (or doesn't exist which is also 'clean' for us)
// Returns error if directory exists and is NOT empty
func ValidateInstallPath(path string) error {
	f, err := os.Open(path)
	if err != nil {
		if os.IsNotExist(err) {
			return nil // Doesn't exist, we can create it
		}
		return fmt.Errorf("cannot verify path: %w", err)
	}
	defer f.Close()

	_, err = f.Readdirnames(1)
	if err == io.EOF {
		return nil // Empty
	}
	return fmt.Errorf("directory is not empty")
}

// ValidateDistroName checks for invalid characters in the name
func ValidateDistroName(name string) error {
	// Prohibit chars that are invalid in Windows filenames or might confuse CLI
	// < > : " / \ | ? * and control chars
	matched, _ := regexp.MatchString(`[<>:"/\\|?*\x00-\x1f]`, name)
	if matched {
		return fmt.Errorf("name contains invalid characters")
	}
	if strings.TrimSpace(name) == "" {
		return fmt.Errorf("name cannot be empty")
	}
	return nil
}

// IsDistroRegistered checks if a distro with the given name already exists
func IsDistroRegistered(projectRoot string, name string) (bool, error) {
	// We reuse ListDistros for consistency and safety against encoding issues
	distros, err := ListDistros(projectRoot, false)
	if err != nil {
		return false, err
	}
	for _, d := range distros {
		if strings.EqualFold(d.Name, name) {
			return true, nil
		}
	}
	return false, nil
}

// RunPowerShellScript runs a script located in /scripts with the given arguments
// It streams output to onOutput if provided, otherwise returns nil on success
func RunPowerShellScript(ctx context.Context, projectRoot string, scriptName string, args []string, onOutput func(string)) error {
	scriptPath := filepath.Join(projectRoot, "scripts", scriptName)

	fullArgs := append([]string{
		"-NoProfile",
		"-ExecutionPolicy", "Bypass",
		"-File", scriptPath,
	}, args...)

	cmd := exec.CommandContext(ctx, "powershell.exe", fullArgs...)
	prepareCmd(cmd)

	if onOutput == nil {
		// Simple run, capture error only
		output, err := cmd.CombinedOutput()
		if err != nil {
			return fmt.Errorf("script failed: %s (%w)", string(output), err)
		}
		return nil
	}

	// Streaming run
	stdout, err := cmd.StdoutPipe()
	if err != nil {
		return err
	}
	stderr, err := cmd.StderrPipe()
	if err != nil {
		return err
	}

	if err := cmd.Start(); err != nil {
		return err
	}

	// Capture output
	go func() {
		scanner := bufio.NewScanner(io.MultiReader(stdout, stderr))
		for scanner.Scan() {
			onOutput(scanner.Text() + "\n")
		}
	}()

	err = cmd.Wait()
	if err != nil {
		if exitErr, ok := err.(*exec.ExitError); ok {
			if status, ok := exitErr.Sys().(syscall.WaitStatus); ok {
				return fmt.Errorf("script exited with code %d", status.ExitStatus())
			}
		}
		return err
	}

	return nil
}

// ListDistros calls the PowerShell script to get installed WSL instances
func ListDistros(projectRoot string, forceUpdate bool) ([]WslInstance, error) {
	scriptPath := filepath.Join(projectRoot, "scripts", "list_distros.ps1")

	args := []string{"-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath}
	if forceUpdate {
		args = append(args, "-ForceUpdate")
	}

	cmd := exec.Command("powershell.exe", args...)
	prepareCmd(cmd) // Hide window on Windows

	output, err := cmd.Output()
	if err != nil {
		return nil, fmt.Errorf("failed to execute list script: %w", err)
	}

	var distros []WslInstance
	// If output is empty or just whitespace, return empty
	if len(output) == 0 {
		return distros, nil
	}

	// PowerShell might return a single object or array.
	// If single object, it might fail if we try to unmarshal to slice.
	// But usually ConvertTo-Json with array input returns array.
	// If only 1 item, PS sometimes returns object. We can try unmarshal to slice, if fail try unmarshal to single.

	err = json.Unmarshal(output, &distros)
	if err != nil {
		// Try single object
		var single WslInstance
		if err2 := json.Unmarshal(output, &single); err2 == nil {
			distros = append(distros, single)
		} else {
			return nil, fmt.Errorf("failed to parse json: %w (output: %s)", err, string(output))
		}
	}

	// Enrich with Release Info (Fallback if script didn't provide it, though script should now)
	for i := range distros {
		// If script returned empty release but it's running, we might still try?
		// But new script logic should handle it.
		// We keep this as fallback for now or remove?
		// Let's rely on script for now to avoid duplicate work.
		if distros[i].State == "Running" && distros[i].Release == "" {
			distros[i].Release = GetDistroReleaseInfo(distros[i].Name, distros[i].State)
		}
	}

	return distros, nil
}

// UnregisterDistro calls the uninstall script
func UnregisterDistro(ctx context.Context, projectRoot string, name string, force bool, onOutput func(string)) error {
	args := []string{"-DistroName", name}
	if force {
		args = append(args, "-Force")
	}
	// We map Unregister to Uninstall Custom script
	return RunPowerShellScript(ctx, projectRoot, "uninstall_wsl_custom.ps1", args, onOutput)
}

// DeleteDistroFiles removes the directory
func DeleteDistroFiles(path string) error {
	if path == "" {
		return fmt.Errorf("path is empty")
	}
	return os.RemoveAll(path)
}

// GetDistroSize calculates the size of the instance's storage
func GetDistroSize(basePath string) (string, error) {
	vhdxPath := filepath.Join(basePath, "ext4.vhdx")
	info, err := os.Stat(vhdxPath)
	if err != nil {
		return "Unknown", err
	}
	size := info.Size()

	const unit = 1024
	if size < unit {
		return fmt.Sprintf("%d B", size), nil
	}
	div, exp := int64(unit), 0
	for n := size / unit; n >= unit; n /= unit {
		div *= unit
		exp++
	}
	return fmt.Sprintf("%.1f %cB", float64(size)/float64(div), "KMGTPE"[exp]), nil
}

// StopDistro terminates the instance using stop_instance.ps1
func StopDistro(ctx context.Context, projectRoot, name string, onOutput func(string)) error {
	return RunPowerShellScript(ctx, projectRoot, "stop_instance.ps1", []string{"-DistroName", name}, onOutput)
}

// ScanDistros calls scan_wsl_instances.ps1
func ScanDistros(ctx context.Context, projectRoot string, onOutput func(string)) error {
	return RunPowerShellScript(ctx, projectRoot, "scan_wsl_instances.ps1", []string{}, onOutput)
}

// RenameDistro calls rename_instance.ps1
func RenameDistro(ctx context.Context, projectRoot, oldName, newName, newPath string, onOutput func(string)) error {
	args := []string{"-OldName", oldName, "-NewName", newName}
	if newPath != "" {
		args = append(args, "-NewPath", newPath)
	}
	return RunPowerShellScript(ctx, projectRoot, "rename_instance.ps1", args, onOutput)
}

// UpdateDistroList runs the update_distros.ps1 script
func UpdateDistroList(ctx context.Context, projectRoot, sourceUrl string, onOutput func(string)) error {
	args := []string{}
	if sourceUrl != "" {
		args = append(args, "-SourceUrl", sourceUrl)
	}
	return RunPowerShellScript(ctx, projectRoot, "update_distros.ps1", args, onOutput)
}

// MoveDistro calls move_instance.ps1
func MoveDistro(ctx context.Context, projectRoot, name, newBasePath string, onOutput func(string)) error {
	return RunPowerShellScript(ctx, projectRoot, "move_instance.ps1", []string{"-DistroName", name, "-NewPath", newBasePath}, onOutput)
}

// SetUserPassword calls set_credentials.ps1
func SetDistroCredentials(ctx context.Context, projectRoot, distroName, user, password string, onOutput func(string)) error {
	args := []string{"-DistroName", distroName, "-UserName", user}
	if password != "" {
		args = append(args, "-Password", password)
	}
	return RunPowerShellScript(ctx, projectRoot, "set_credentials.ps1", args, onOutput)
}

// StartDistro starts the instance using start_instance.ps1
// if openTerminal is true, it launches a new window.
// if false, it runs in background.
func StartDistro(ctx context.Context, projectRoot, name string, openTerminal bool, startPath string) error {
	scriptPath := filepath.Join(projectRoot, "scripts", "start_instance.ps1")
	args := []string{"-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath, "-DistroName", name}

	if openTerminal {
		args = append(args, "-OpenTerminal")
		if startPath != "" {
			args = append(args, "-StartPath", startPath)
		}
		// Launch in new window via cmd /c start
		// "start" is a cmd builtin that opens a new window
		cmdArgs := append([]string{"/c", "start", "powershell.exe"}, args...)
		cmd := exec.Command("cmd.exe", cmdArgs...)
		return cmd.Start()
	} else {
		// Run in background (hidden window normally handled by prepareCmd or just non-console exec)
		// We use powershell directly
		cmd := exec.CommandContext(ctx, "powershell.exe", args...)
		prepareCmd(cmd) // Sets SysProcAttr to hide window on Windows
		return cmd.Run()
	}
}

// GetDistroReleaseInfo fetches PRETTY_NAME from /etc/os-release
// Used mainly by fallback or cache refresh
func GetDistroReleaseInfo(name, state string) string {
	// Logic remains similar but could be moved to script if strictly needed
	// Keeping for now as simple read helper
	if state != "Running" {
		return ""
	}
	cmd := exec.Command("wsl", "-d", name, "cat", "/etc/os-release")
	prepareCmd(cmd)
	out, err := cmd.Output()
	if err != nil {
		return ""
	}
	// Parse PRETTY_NAME
	re := regexp.MustCompile(`PRETTY_NAME="?([^"
]+)"?`)
	matches := re.FindSubmatch(out)
	if len(matches) > 1 {
		return string(matches[1])
	}
	return "Linux"
}

// DownloadDistroOnly downloads the distro package without installing it
func DownloadDistroOnly(ctx context.Context, projectRoot, family, version string, onOutput func(string)) error {
	args := []string{"-SelectFamily", family, "-SelectVersion", version}
	return RunPowerShellScript(ctx, projectRoot, "download_all_distros.ps1", args, onOutput)
}
