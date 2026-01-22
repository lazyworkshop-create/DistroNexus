package config

import (
	"distronexus-gui/internal/model"
	"encoding/json"
	"fmt"
	"os"
)

// Loader handles configuration loading and saving
type Loader struct {
	BaseDir string
}

// NewLoader creates a new config loader with the project root directory
func NewLoader(baseDir string) *Loader {
	return &Loader{BaseDir: baseDir}
}

// LoadDistros reads the distros.json file
func (l *Loader) LoadDistros() (map[string]model.DistroConfig, error) {
	path := l.getPath("distros.json")
	data, err := os.ReadFile(path)
	if err != nil {
		return nil, fmt.Errorf("failed to read distros.json at %s: %w", path, err)
	}

	var distros map[string]model.DistroConfig
	if err := json.Unmarshal(data, &distros); err != nil {
		return nil, fmt.Errorf("failed to parse distros.json: %w", err)
	}
	return distros, nil
}

// LoadSettings reads the settings.json file
func (l *Loader) LoadSettings() (*model.GlobalSettings, error) {
	path := l.getPath("settings.json")
	var settings model.GlobalSettings

	// If settings don't exist, return defaults but don't error
	if _, err := os.Stat(path); os.IsNotExist(err) {
		return &model.GlobalSettings{
			DefaultInstallPath: "D:\\WSL",
			DefaultDistro:      "Ubuntu-24.04",
			DistroCachePath:    "..\\..\\distro",
		}, nil
	}

	data, err := os.ReadFile(path)
	if err != nil {
		return nil, fmt.Errorf("failed to read settings.json: %w", err)
	}

	if err := json.Unmarshal(data, &settings); err != nil {
		return nil, fmt.Errorf("failed to parse settings.json: %w", err)
	}
	return &settings, nil
}

// SaveSettings writes the settings.json file
func (l *Loader) SaveSettings(settings *model.GlobalSettings) error {
	path := l.getPath("settings.json")
	data, err := json.MarshalIndent(settings, "", "    ")
	if err != nil {
		return err
	}
	return os.WriteFile(path, data, 0644)
}

func (l *Loader) getPath(filename string) string {
	// Assumes config is strictly in <root>/config/
	// If BaseDir is the project root
	return fmt.Sprintf("%s/config/%s", l.BaseDir, filename)
}
