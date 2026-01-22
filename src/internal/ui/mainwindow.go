package ui

import (
	"distronexus-gui/internal/config"
	"distronexus-gui/internal/logic"
	"distronexus-gui/internal/model"
	"fmt"
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
	mw.LogArea.Disable() // Read-only
	mw.LogArea.TextStyle = fyne.TextStyle{Monospace: true}
	
	// --- Right Side: Details & Form ---
	// Variables for Form
	selectedDistroLabel := widget.NewLabel("Select a distribution")
	selectedDistroLabel.TextStyle = fyne.TextStyle{Bold: true}
	
	versionSelect := widget.NewSelect([]string{}, nil)
	versionSelect.PlaceHolder = "Select Version"
	
	installPathEntry := widget.NewEntry()
	installPathEntry.SetText(mw.Settings.DefaultInstallPath)
	
	userEntry := widget.NewEntry()
	userEntry.SetPlaceHolder("username")
	
	passEntry := widget.NewPasswordEntry()
	passEntry.SetPlaceHolder("password")

	installBtn := widget.NewButton("Install", func() {
		// Validation
		if versionSelect.Selected == "" {
			dialog.ShowInformation("Required", "Please select a version.", mw.Window)
			return
		}
		if installPathEntry.Text == "" || userEntry.Text == "" || passEntry.Text == "" {
			dialog.ShowInformation("Required", "Please fill in all fields.", mw.Window)
			return
		}

		// Disable button
		// (In a real app, use binding or state to disable)

		mw.LogArea.SetText("") // Clear log
		mw.LogArea.Refresh()

		// Logic Call
		logic.RunInstallScript(
			mw.ProjectDir,
			versionSelect.Selected,    // DistroName (e.g. Ubuntu-22.04) logic needs mapping to correct param
			installPathEntry.Text,
			userEntry.Text,
			passEntry.Text,
			func(logMsg string) {
				mw.LogArea.Append(logMsg)
				mw.LogArea.Refresh() // Force repaint
			},
			func(err error) {
				if err != nil {
					mw.LogArea.Append(fmt.Sprintf("\nError: %s\n", err.Error()))
				} else {
					dialog.ShowInformation("Success", "Installation Finished!", mw.Window)
				}
			},
		)
	})
	installBtn.Importance = widget.HighImportance

	// Form Container
	form := container.NewVBox(
		widget.NewLabel("Version:"),
		versionSelect,
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
		// Also need to map "Display Name" back to actual Distro ID/DefaultName if needed
		// For now, let's just use the values found in Versions map
		
		// Note: The json structure is a bit mapped by ID "1", "2".
		// But here we are iterating.
		// Let's create a map for selection
		
		vMap := make(map[string]string) // "Ubuntu 22.04" -> "Ubuntu-22.04"
		
		for _, v := range cfg.Versions {
			vMap[v.Name] = v.DefaultName
			versions = append(versions, v.Name)
		}
		sort.Sort(sort.Reverse(sort.StringSlice(versions))) // Newest first usually
		
		versionSelect.Options = versions
		versionSelect.Selected = ""
		versionSelect.Refresh()
		
		// Update selection logic to pass the 'DefaultName' (ID) to the script
		versionSelect.OnChanged = func(s string) {
			// When user picks "Ubuntu 22.04 LTS", we actually want "Ubuntu-22.04"
			// But for now, we just store it in the select widget.
			// Ideally we store the ID in a separate var.
			// Let's cheat and create a lookup here or modify the install click handler.
			// For simplicity: Update the install click handler to lookup from `vMap`?
			// Scope issue.
		}
		
		// Hacky fix for scope: Redefine the install button action or use a closure variable
		installBtn.OnTapped = func() {
			currentVerDisplay := versionSelect.Selected
			if currentVerDisplay == "" {
				dialog.ShowInformation("Required", "Please select a version.", mw.Window)
				return
			}
			
			realDistroID := vMap[currentVerDisplay] // e.g., "Ubuntu-22.04"
			
			if installPathEntry.Text == "" || userEntry.Text == "" || passEntry.Text == "" {
				dialog.ShowInformation("Required", "Please fill in all fields.", mw.Window)
				return
			}

			mw.LogArea.SetText(fmt.Sprintf("Preparing to install: %s (%s)...\n", currentVerDisplay, realDistroID))
			
			logic.RunInstallScript(
				mw.ProjectDir,
				realDistroID,
				installPathEntry.Text,
				userEntry.Text,
				passEntry.Text,
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
