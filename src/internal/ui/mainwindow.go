package ui

import (
	"distronexus-gui/internal/config"
	"distronexus-gui/internal/logic"
	"distronexus-gui/internal/model"
	"fmt"
	"path/filepath"
	"sort"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/dialog"
	"fyne.io/fyne/v2/layout"
	"fyne.io/fyne/v2/theme"
	"fyne.io/fyne/v2/widget"
)

type MainWindow struct {
	App        fyne.App
	Window     fyne.Window
	Config     *config.Loader
	Distros    map[string]model.DistroConfig
	Settings   *model.GlobalSettings
	ProjectDir string

	// UI Components
	LogArea *widget.Entry
}

func NewMainWindow(app fyne.App, projectDir string) *MainWindow {
	mw := &MainWindow{
		App:        app,
		Window:     app.NewWindow("DistroNexus Installer"),
		ProjectDir: projectDir,
		Config:     config.NewLoader(projectDir),
	}
	return mw
}

func (mw *MainWindow) Init() {
	// Load Initial Data
	var err error
	mw.Distros, err = mw.Config.LoadDistros()
	if err != nil {
		dialog.ShowError(err, mw.Window)
	}
	mw.Settings, err = mw.Config.LoadSettings()
	if err != nil {
		fmt.Println("Warning loading settings:", err)
	}

	mw.buildUI()
	mw.Window.Resize(fyne.NewSize(800, 600))
	mw.Window.CenterOnScreen()
	mw.Window.Show()
}

func (mw *MainWindow) buildUI() {
	// --- Data Setup ---
	var distroNames []string
	distroMap := make(map[string]model.DistroConfig) // Name -> Config

	for _, d := range mw.Distros {
		distroNames = append(distroNames, d.Name)
		distroMap[d.Name] = d
	}
	sort.Strings(distroNames)

	// Shared State for Logic
	var currentVMap map[string]string
	var currentDistroFamily string

	// --- Log Area (Bottom) ---
	mw.LogArea = widget.NewMultiLineEntry()
	// mw.LogArea.Disable() // Disabled usually makes text too light. Keep enabled for readability.
	mw.LogArea.TextStyle = fyne.TextStyle{Monospace: true}

	// --- Form Widgets ---
	distroSelect := widget.NewSelect(distroNames, nil)
	distroSelect.PlaceHolder = "Select Family"

	versionSelect := widget.NewSelect([]string{}, nil)
	versionSelect.PlaceHolder = "Select Version"

	nameEntry := widget.NewEntry()
	nameEntry.PlaceHolder = "Instance Name (e.g. Ubuntu-Work)"

	// Standard Mode Fields
	installPathEntry := widget.NewEntry()
	installPathEntry.SetText(mw.Settings.DefaultInstallPath)

	userEntry := widget.NewEntry()
	userEntry.SetPlaceHolder("username")

	passEntry := widget.NewPasswordEntry()
	passEntry.SetPlaceHolder("password")

	standardFields := container.NewVBox(
		widget.NewLabel("Install Path:"),
		installPathEntry,
		widget.NewLabel("New Username:"),
		userEntry,
		widget.NewLabel("New Password:"),
		passEntry,
	)

	// Quick Mode Toggle
	quickModeCheck := widget.NewCheck("Quick Mode (Auto Path, Root Only)", nil)
	quickModeCheck.OnChanged = func(checked bool) {
		if checked {
			standardFields.Hide()
		} else {
			standardFields.Show()
		}
	}

	// Install Button
	installBtn := widget.NewButton("Install", func() {
		// Validation
		if distroSelect.Selected == "" {
			dialog.ShowInformation("Required", "Please select a distribution family.", mw.Window)
			return
		}
		if versionSelect.Selected == "" {
			dialog.ShowInformation("Required", "Please select a version.", mw.Window)
			return
		}

		currentVerDisplay := versionSelect.Selected
		realDistroID := currentVMap[currentVerDisplay]

		// Determine Parameters
		var finalName, finalPath, finalUser, finalPass string

		finalName = nameEntry.Text
		if finalName == "" {
			finalName = realDistroID
		}

		if quickModeCheck.Checked {
			// Quick Mode: Auto-Calculate everything
			finalPath = filepath.Join(mw.Settings.DefaultInstallPath, finalName)
			finalUser = "" // Skip user creation logic in PS script
			finalPass = ""
		} else {
			// Standard Mode: Validate Inputs
			if installPathEntry.Text == "" || userEntry.Text == "" || passEntry.Text == "" {
				dialog.ShowInformation("Required", "Please fill in all fields for Standard Mode.", mw.Window)
				return
			}
			finalPath = installPathEntry.Text
			finalUser = userEntry.Text
			finalPass = passEntry.Text
		}

		mw.LogArea.SetText(fmt.Sprintf("Preparing to install: %s\n", finalName))
		mw.LogArea.Append(fmt.Sprintf("Source:      %s (%s)\n", currentVerDisplay, currentDistroFamily))
		if quickModeCheck.Checked {
			mw.LogArea.Append("Mode:        Quick (Root/No-Password)\n")
		}
		mw.LogArea.Append(fmt.Sprintf("Destination: %s\n", finalPath))

		logic.RunInstallScript(
			mw.ProjectDir,
			currentDistroFamily,
			currentVerDisplay,
			finalName, // Pass custom name as -DistroName
			finalPath,
			finalUser,
			finalPass,
			func(s string) { mw.LogArea.Append(s) },
			func(e error) {
				if e != nil {
					dialog.ShowError(e, mw.Window)
				} else {
					dialog.ShowInformation("Done", "Installation completed.", mw.Window)
				}
			},
		)
	})
	installBtn.Importance = widget.HighImportance

	// --- Event Logic ---
	distroSelect.OnChanged = func(selectedName string) {
		if selectedName == "" {
			return
		}
		cfg := distroMap[selectedName]
		currentDistroFamily = cfg.Name

		// Update Version Select options
		currentVMap = make(map[string]string)
		var versions []string

		for _, v := range cfg.Versions {
			currentVMap[v.Name] = v.DefaultName
			versions = append(versions, v.Name)
		}
		sort.Sort(sort.Reverse(sort.StringSlice(versions)))

		versionSelect.Options = versions
		if len(versions) > 0 {
			versionSelect.Selected = versions[0]
		} else {
			versionSelect.Selected = ""
		}

		// Trigger initial update of Name Entry based on selection
		if versionSelect.Selected != "" {
			nameEntry.SetText(currentVMap[versionSelect.Selected])
		}
		versionSelect.Refresh()
	}

	versionSelect.OnChanged = func(s string) {
		if val, ok := currentVMap[s]; ok {
			nameEntry.SetText(val)
		}
	}

	// --- Layout Assembly ---
	
	// Row 1: Distro & Version Selection
	selectionRow := container.NewGridWithColumns(2,
		container.NewVBox(
			widget.NewLabelWithStyle("Distribution Family", fyne.TextAlignLeading, fyne.TextStyle{Bold: true}),
			distroSelect,
		),
		container.NewVBox(
			widget.NewLabelWithStyle("Version", fyne.TextAlignLeading, fyne.TextStyle{Bold: true}),
			versionSelect,
		),
	)

	// Configuration Section
	configLabel := widget.NewLabelWithStyle("Configuration", fyne.TextAlignLeading, fyne.TextStyle{Bold: true})
	
	formContent := container.NewVBox(
		selectionRow,
		widget.NewSeparator(),
		
		configLabel,
		widget.NewLabel("Instance Name:"),
		nameEntry,
		
		quickModeCheck,
		standardFields, // Contains Path, User, Pass; toggled by check
		
		layout.NewSpacer(),
		installBtn,
		widget.NewSeparator(),
		widget.NewLabelWithStyle("Installation Log", fyne.TextAlignLeading, fyne.TextStyle{Bold: true}),
	)

	// Combine Form and Log into a single scrolling container
	// We set a minimum size for the log area
	mw.LogArea.SetMinRowsVisible(10) 

	// Main Layout: No Split, just vertical stack with padding
	mainContent := container.NewVBox(
		formContent,
		mw.LogArea,
	)

	// Use Padded container for "Windows 11-like" feel (breathing room)
	paddedContent := container.NewPadded(mainContent)
	
	// Wrap in Scroll for smaller screens
	scrollContainer := container.NewVScroll(paddedContent)

	// Toolbar
	toolbar := widget.NewToolbar(
		widget.NewToolbarSpacer(),
		widget.NewToolbarAction(theme.SettingsIcon(), func() {
			mw.ShowSettingsDialog()
		}),
	)

	// Apply Theme/Layout
	content := container.NewBorder(toolbar, nil, nil, nil, scrollContainer)
	mw.Window.SetContent(content)
}
