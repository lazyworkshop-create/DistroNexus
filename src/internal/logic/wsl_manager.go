package logic

import (
	"context"
	"encoding/json"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
)

type WslInstance struct {
	Name     string `json:"Name"`
	BasePath string `json:"BasePath"`
	State    string `json:"State"`
	WslVer   string `json:"WslVer"`
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
