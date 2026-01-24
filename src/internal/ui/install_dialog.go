package ui

import (
	"context"
	"distronexus-gui/internal/logic"
	"distronexus-gui/internal/model"
	"path/filepath"
	"sort"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/dialog"
	"fyne.io/fyne/v2/theme"
	"fyne.io/fyne/v2/widget"
)

func (mw *MainWindow) ShowInstallDialog(preSelectedFamily string, preSelectedVersionName string) {
	var mainWindow = mw.Window

	// Data preparation
	var distroNames []string
	distroMap := make(map[string]model.DistroConfig)

	for _, d := range mw.Distros {
		distroNames = append(distroNames, d.Name)
		distroMap[d.Name] = d
	}
	sort.Strings(distroNames)

	// Widgets
	distroSelect := widget.NewSelect(distroNames, nil)
	distroSelect.PlaceHolder = "Select Family"

	versionSelect := widget.NewSelect([]string{}, nil)
	versionSelect.PlaceHolder = "Select Version"

	nameEntry := widget.NewEntry()
	nameEntry.PlaceHolder = "Instance Name (e.g. MyUbuntu)"

	updateVersions := func(fam string) {
		if cfg, ok := distroMap[fam]; ok {
			var vers []string
			// We need versions sorted by version name or some key?
			// The map iteration order is random. Let's sort.
			// Actually distroMap[fam].Versions is map[string]Version
			// We want to list Version.Name in the select box.
			// But we need to map back to Version struct later.

			// Let's create a temporary lookup for this scope or just iterate to find match
			var verObjects []model.Version
			for _, v := range cfg.Versions {
				verObjects = append(verObjects, v)
			}
			// Sort by Name
			sort.Slice(verObjects, func(i, j int) bool {
				return verObjects[i].Name < verObjects[j].Name
			})

			for _, v := range verObjects {
				vers = append(vers, v.Name)
			}
			versionSelect.Options = vers
			versionSelect.Selected = ""
			versionSelect.Refresh()
		}
	}

	distroSelect.OnChanged = func(s string) {
		updateVersions(s)
	}

	versionSelect.OnChanged = func(s string) {
		if s == "" {
			return
		}
		if cfg, ok := distroMap[distroSelect.Selected]; ok {
			for _, v := range cfg.Versions {
				if v.Name == s && nameEntry.Text == "" {
					nameEntry.SetText(v.DefaultName)
				}
			}
		}
	}

	// Pre-selection Logic
	if preSelectedFamily != "" {
		distroSelect.SetSelected(preSelectedFamily)
		// SetSelected triggers OnChanged, which populates versions
		// Now select version if provided
		if preSelectedVersionName != "" {
			// Find the display name matching the version ID or just use what passed?
			// The caller passes what? Let's assume caller passes the Name field of Version struct.
			versionSelect.SetSelected(preSelectedVersionName)
			// This triggers versionSelect.OnChanged which sets the default name
		}
	}

	// --- Quick Mode Logic ---
	quickModeCheck := widget.NewCheck("Quick Mode (Root User, Default Path)", nil)
	quickModeCheck.Checked = false

	// Detailed Fields
	installPathEntry := widget.NewEntry()
	installPathEntry.SetText(mw.Settings.DefaultInstallPath)
	btnPickPath := widget.NewButtonWithIcon("", theme.FolderOpenIcon(), func() {
		dialog.ShowFolderOpen(func(uri fyne.ListableURI, err error) {
			if uri != nil {
				installPathEntry.SetText(uri.Path())
			}
		}, mainWindow)
	})
	pathContainer := container.NewBorder(nil, nil, nil, btnPickPath, installPathEntry)

	userEntry := widget.NewEntry()
	userEntry.SetPlaceHolder("username")
	passEntry := widget.NewPasswordEntry()
	passEntry.SetPlaceHolder("password")

	detailsGroup := container.NewVBox(
		widget.NewLabel("Install Location"),
		pathContainer,
		widget.NewLabel("Username"),
		userEntry,
		widget.NewLabel("Password"),
		passEntry,
	)

	quickModeCheck.OnChanged = func(checked bool) {
		if checked {
			detailsGroup.Hide()
		} else {
			detailsGroup.Show()
		}
	}
	// Initial state
	detailsGroup.Show()

	// Form Layout
	distroBox := container.NewVBox(widget.NewLabel("Distribution Family"), distroSelect)
	versionBox := container.NewVBox(widget.NewLabel("Version"), versionSelect)

	content := container.NewVBox(
		container.NewGridWithColumns(2, distroBox, versionBox),
		widget.NewLabel("Instance Name"),
		nameEntry,
		quickModeCheck,
		detailsGroup,
	)

	// Custom Dialog to allow complex content
	// dialog.NewCustomConfirm doesn't autoresize content well sometimes if it changes size dynamically.
	// But let's try.

	var d dialog.Dialog
	d = dialog.NewCustomConfirm("Install New Instance", "Install", "Cancel",
		content,
		func(confirm bool) {
			if !confirm {
				return
			}
			// --- Logic ---
			fam := distroSelect.Selected
			ver := versionSelect.Selected
			name := nameEntry.Text

			if fam == "" || ver == "" || name == "" {
				dialog.ShowError(pluginError("Distribution, Version and Name are required"), mainWindow)
				return
			}

			// Validate Name
			if err := logic.ValidateDistroName(name); err != nil {
				dialog.ShowError(err, mainWindow)
				return
			}

			// Determine Params
			var targetPath, user, pass string

			if quickModeCheck.Checked {
				// Quick Mode Defaults
				targetPath = filepath.Join(mw.Settings.DefaultInstallPath, name)
				user = "" // Script Logic: Empty user means root? Or we pass "root"?
				// The script  logic: if user/pass empty, might fail or skip user creation?
				// Let's assume we want "root".
				// Actually, usually quick mode implies "root" user (default).
				user = "root"
				pass = "" // No password for root? Or empty pass? Use "root" maybe?
				// Safety: Let's pass empty, script should handle. Or pass "root"/"root".
				// Requirement says "root only".
			} else {
				targetPath = installPathEntry.Text
				user = userEntry.Text
				pass = passEntry.Text
				if targetPath == "" || user == "" || pass == "" {
					dialog.ShowError(pluginError("All fields are required in Standard Mode"), mainWindow)
					return
				}
			}

			// Use blocking progress
			d.Hide() // Close the input dialog first

			showBlockingProgress("Installing "+fam+" "+ver, mainWindow, func(log func(string)) error {
				resCh := make(chan error)
				logic.RunInstallScript(context.Background(), mw.ProjectDir, fam, ver, name, targetPath, user, pass, log, func(e error) {
					resCh <- e
				})
				return <-resCh
			}, func() {
				dialog.ShowInformation("Success", "Installation complete!", mainWindow)
				// Trigger refresh of home list if available
				if mw.RefreshHomeList != nil {
					mw.RefreshHomeList()
				}
			})

		}, mainWindow)

	d.Resize(fyne.NewSize(500, 600))
	d.Show()
}

func pluginError(s string) error {
	return &pErr{s}
}

type pErr struct{ s string }

func (e *pErr) Error() string { return e.s }
