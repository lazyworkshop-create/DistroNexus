package main

import (
	"distronexus-gui/internal/ui"
	"os"
	"path/filepath"

	"fyne.io/fyne/v2/app"
	"fyne.io/fyne/v2/theme"
)

func main() {
	// Create application instance
	a := app.New()
	a.SetIcon(theme.SettingsIcon())

	// Determine Project Root based on Executable location
	ex, err := os.Executable()
	if err != nil {
		ex, _ = os.Getwd()
	}
	exPath := filepath.Dir(ex)

	projectRoot := exPath

	// If running with 'go run', executable is in /tmp, so we might need CWD fallback
	if _, err := os.Stat(filepath.Join(projectRoot, "config")); os.IsNotExist(err) {
		cwd, _ := os.Getwd()
		if _, err := os.Stat(filepath.Join(cwd, "config")); err == nil {
			projectRoot = cwd
		}
	}

	mw := ui.NewMainWindow(a, projectRoot)
	mw.Init()

	a.Run()
}
