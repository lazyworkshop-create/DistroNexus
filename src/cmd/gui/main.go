package main

import (
	"distronexus-gui/internal/ui"
	"os"
	"path/filepath"

	"fyne.io/fyne/v2/app"
)

func main() {
	// Create application instance
	a := app.New()
	
	// Determine Project Root (Assuming we run from root or src/..)
	// For dev environment, if run with 'go run src/cmd/gui/main.go', CWD is root.
	cwd, _ := os.Getwd()
	
	// Simple heuristic: look for config folder
	projectRoot := cwd
	if _, err := os.Stat(filepath.Join(cwd, "config")); os.IsNotExist(err) {
		// Example check: if running inside src/cmd/gui built binary?
		// For now assume CWD is correct as per instructions
	}

	mw := ui.NewMainWindow(a, projectRoot)
	mw.Init()
	
	a.Run()
}
