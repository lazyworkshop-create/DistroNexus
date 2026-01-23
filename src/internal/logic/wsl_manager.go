package logic

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"strings"
)

type WslInstance struct {
	Name     string `json:"Name"`
	BasePath string `json:"BasePath"`
	State    string `json:"State"`
	WslVer   string `json:"WslVer"`
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
	distros, err := ListDistros(projectRoot)
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

// ListDistros calls the PowerShell script to get installed WSL instances
func ListDistros(projectRoot string) ([]WslInstance, error) {
	scriptPath := filepath.Join(projectRoot, "scripts", "list_distros.ps1")

	cmd := exec.Command("powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath)
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

	return distros, nil
}

// UnregisterDistro calls wsl --unregister
func UnregisterDistro(ctx context.Context, name string) error {
	cmd := exec.CommandContext(ctx, "wsl", "--unregister", name)
	prepareCmd(cmd)
	output, err := cmd.CombinedOutput()
	if err != nil {
		return fmt.Errorf("wsl unregister failed: %s (%w)", string(output), err)
	}
	return nil
}

// DeleteDistroFiles removes the directory
func DeleteDistroFiles(path string) error {
	if path == "" {
		return fmt.Errorf("path is empty")
	}
	return os.RemoveAll(path)
}
