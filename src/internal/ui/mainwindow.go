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
	// --- Left Sidebar: Distro List ---
	var distroNames []string
	distroMap := make(map[string]model.DistroConfig) // Name -> Config

	// Convert map map to flat list for simplicity or sorted list
	for _, d := range mw.Distros {
		distroNames = append(distroNames, d.Name)
		distroMap[d.Name] = d
	}
	sort.Strings(distroNames)

	// --- Log Area (Bottom) ---
	mw.LogArea = widget.NewMultiLineEntry()
	// mw.LogArea.Disable() // Disabled usually makes text too light. Keep enabled for readability.
	mw.LogArea.TextStyle = fyne.TextStyle{Monospace: true}

	// --- Right Side: Details & Form ---
	// Variables for Form
	selectedDistroLabel := widget.NewLabel("Select a distribution")
	selectedDistroLabel.TextStyle = fyne.TextStyle{Bold: true}

	versionSelect := widget.NewSelect([]string{}, nil)
	versionSelect.PlaceHolder = "Select Version"

	nameEntry := widget.NewEntry()
	nameEntry.SetPlaceHolder("Instance Name (e.g. Ubuntu-Work)")

	installPathEntry := widget.NewEntry()
	installPathEntry.SetText(mw.Settings.DefaultInstallPath)

	userEntry := widget.NewEntry()
	userEntry.SetPlaceHolder("username")

	passEntry := widget.NewPasswordEntry()
	passEntry.SetPlaceHolder("password")

	quickModeCheck := widget.NewCheck("Quick Mode (Auto Path, Root User)", nil)
	quickModeCheck.OnChanged = func(checked bool) {
		if checked {
			installPathEntry.Disable()
			userEntry.Disable()
			passEntry.Disable()
		} else {
			installPathEntry.Enable()
			userEntry.Enable()
			passEntry.Enable()
		}
	}

	installBtn := widget.NewButton("Install", nil) // Logic defined later
	installBtn.Importance = widget.HighImportance

	// Form Container
	form := container.NewVBox(
		widget.NewLabel("Version:"),
		versionSelect,
		quickModeCheck,
		widget.NewLabel("Instance Name:"),
		nameEntry,
		widget.NewLabel("Install Path:"),
		installPathEntry,
		widget.NewLabel("New Username:"),
		userEntry,
		widget.NewLabel("New Password:"),
		passEntry,
		layout.NewSpacer(),
		installBtn,
	)

	detailsContainer := container.NewBorder(
		container.NewVBox(selectedDistroLabel, widget.NewSeparator()),
		nil, nil, nil,
		form,
	)

	// List Handling
	distroList := widget.NewList(
		func() int { return len(distroNames) },
		func() fyne.CanvasObject { return widget.NewLabel("Template") },
		func(i int, o fyne.CanvasObject) {
			o.(*widget.Label).SetText(distroNames[i])
		},
	)

	distroList.OnSelected = func(id widget.ListItemID) {
		selectedName := distroNames[id]
		cfg := distroMap[selectedName]

		selectedDistroLabel.SetText(cfg.Name)

		// Update Version Select options
		var versions []string
		vMap := make(map[string]string) // "Ubuntu 22.04" -> "Ubuntu-22.04"

		for _, v := range cfg.Versions {
			vMap[v.Name] = v.DefaultName
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
			nameEntry.SetText(vMap[versionSelect.Selected])
		}
		versionSelect.Refresh()

		// Update name when version changes
		versionSelect.OnChanged = func(s string) {
			if val, ok := vMap[s]; ok {
				nameEntry.SetText(val)
			}
		}

		// Hacky fix for scope: Redefine the install button action or use a closure variable
		installBtn.OnTapped = func() {
			currentVerDisplay := versionSelect.Selected
			if currentVerDisplay == "" {
				dialog.ShowInformation("Required", "Please select a version.", mw.Window)
				return
			}

			// e.g., "Ubuntu-22.04"
			realDistroID := vMap[currentVerDisplay] 

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

			mw.LogArea.SetText(fmt.Sprintf("Preparing to install: %s (%s)...\n", currentVerDisplay, realDistroID))
			mw.LogArea.Append(fmt.Sprintf("Instance Name: %s\n", finalName))
			mw.LogArea.Append(fmt.Sprintf("Destination:   %s\n", finalPath))

			logic.RunInstallScript(
				mw.ProjectDir,
				cfg.Name,
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
		}
	}

	// Layout Assembly
	// Split: Left (List 30%), Right (Form 70%)
	split := container.NewHSplit(distroList, detailsContainer)
	split.SetOffset(0.3)

	// Main Layout: Top Split, Bottom Log
	// Using SplitVertical for resizable log area
	mainSplit := container.NewVSplit(split, mw.LogArea)
	mainSplit.SetOffset(0.7) // 70% top, 30% log

	// Toolbar
	toolbar := widget.NewToolbar(
		widget.NewToolbarSpacer(),
		widget.NewToolbarAction(theme.SettingsIcon(), func() {
			mw.ShowSettingsDialog()
		}),
	)

	// Apply Theme/Layout
	content := container.NewBorder(toolbar, nil, nil, nil, mainSplit)
	mw.Window.SetContent(content)
}
