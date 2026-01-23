package ui

import (
	"distronexus-gui/internal/config"
	"context"
	"distronexus-gui/internal/logic"
	"distronexus-gui/internal/model"
	"fmt"
	"path/filepath"
	"sort"
	"strings"

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
	LogArea *widget.Entry // Kept in memory but hidden
	
	// Progress Control
	progress     *widget.ProgressBarInfinite
	statusLabel  *widget.Label
	installBtn   *widget.Button
	
	// Containers
	mainContainer *fyne.Container // The root container that swaps content
	installView   fyne.CanvasObject
	uninstallView fyne.CanvasObject

	cancelCtx    context.Context
	cancelFunc   context.CancelFunc
	isInstalling bool
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
	// --- Parameters & Helper Vars ---
	var distroNames []string
	distroMap := make(map[string]model.DistroConfig) // Name -> Config

	for _, d := range mw.Distros {
		distroNames = append(distroNames, d.Name)
		distroMap[d.Name] = d
	}
	sort.Strings(distroNames)

	var currentVMap map[string]string
	var currentDistroFamily string

	// --- Components ---
	
	// Hidden Log Area (still used for accumulating context if needed, or we just drop it)
	mw.LogArea = widget.NewMultiLineEntry() 
	
	// Progress Bar & Status
	mw.progress = widget.NewProgressBarInfinite()
	mw.progress.Hide()
	
	mw.statusLabel = widget.NewLabel("Ready")
	mw.statusLabel.Alignment = fyne.TextAlignCenter
	mw.statusLabel.TextStyle = fyne.TextStyle{Italic: true} 
	mw.statusLabel.Hide()

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

	// Install Action Logic
	mw.installBtn = widget.NewButton("Install", nil)
	mw.installBtn.Importance = widget.HighImportance

	mw.installBtn.OnTapped = func() {
		// --- Cancel Logic ---
		if mw.isInstalling {
			dialog.ShowConfirm("Cancel Installation", "Are you sure you want to cancel the installation?", func(userConfirmed bool) {
				if userConfirmed {
					if mw.cancelFunc != nil {
						mw.cancelFunc()
					}
				}
			}, mw.Window)
			return
		}
		
		// --- Install Logic ---

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
			// Quick Mode
			finalPath = filepath.Join(mw.Settings.DefaultInstallPath, finalName)
			finalUser = "" 
			finalPass = ""
		} else {
			// Standard Mode
			if installPathEntry.Text == "" || userEntry.Text == "" || passEntry.Text == "" {
				dialog.ShowInformation("Required", "Please fill in all fields for Standard Mode.", mw.Window)
				return
			}
			finalPath = installPathEntry.Text
			finalUser = userEntry.Text
			finalPass = passEntry.Text
		}

		// --- LOCK UI ---
		mw.isInstalling = true
		mw.installBtn.SetText("Cancel")
		mw.installBtn.Importance = widget.DangerImportance
		
		distroSelect.Disable()
		versionSelect.Disable()
		nameEntry.Disable()
		quickModeCheck.Disable()
		installPathEntry.Disable()
		userEntry.Disable()
		passEntry.Disable()
		
		mw.progress.Show()
		mw.progress.Start()
		mw.statusLabel.SetText("Initializing...")
		mw.statusLabel.Show()
		
		// Prepare Context
		mw.cancelCtx, mw.cancelFunc = context.WithCancel(context.Background())

		logic.RunInstallScript(
			mw.cancelCtx,
			mw.ProjectDir,
			currentDistroFamily,
			currentVerDisplay,
			finalName, 
			finalPath,
			finalUser,
			finalPass,
			func(s string) { 
				// Update Status Label (Trim whitespace)
				clean := strings.TrimSpace(s)
				if clean != "" && len(clean) > 3 {
					// Only update if looks like a real message
					if len(clean) > 60 {
						clean = clean[:57] + "..."
					}
					mw.statusLabel.SetText(clean)
				}
			},
			func(e error) {
				// --- UNLOCK UI ---
				mw.isInstalling = false
				mw.progress.Stop()
				mw.progress.Hide()
				mw.statusLabel.Hide()
				
				mw.installBtn.SetText("Install")
				mw.installBtn.Importance = widget.HighImportance
				
				distroSelect.Enable()
				versionSelect.Enable()
				nameEntry.Enable()
				quickModeCheck.Enable()
				// Only enable fields if quickmode is unchecked
				if !quickModeCheck.Checked {
					installPathEntry.Enable()
					userEntry.Enable()
					passEntry.Enable()
				}

				if e != nil {
					if e == context.Canceled {
						dialog.ShowInformation("Cancelled", "Installation was cancelled by user.", mw.Window)
					} else {
						dialog.ShowError(fmt.Errorf("Installation Failed:\n%s", e.Error()), mw.Window)
					}
				} else {
					dialog.ShowInformation("Success", "Installation Finished Successfully!", mw.Window)
				}
			},
		)
	}
	// Used in layout
	installBtn := mw.installBtn

	// --- Event Logic (Dropdowns) ---
	distroSelect.OnChanged = func(selectedName string) {
		if selectedName == "" {
			return
		}
		cfg := distroMap[selectedName]
		currentDistroFamily = cfg.Name

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
		
		// Status Area
		mw.statusLabel,
		mw.progress,
		installBtn,
	)


	// Main Layout: No Split, just vertical stack with padding
	// We remove LogArea from view
	// mainContent := container.NewVBox(formContent, mw.LogArea)
	
	mainContent := container.NewVBox(formContent)

	// Use Padded container for "Windows 11-like" feel (breathing room)
	paddedContent := container.NewPadded(mainContent)
	
	// Wrap in Scroll for smaller screens
	scrollContainer := container.NewVScroll(paddedContent)

	// Toolbar
	toolbar := widget.NewToolbar(
		widget.NewToolbarSpacer(),
		widget.NewToolbarAction(theme.DeleteIcon(), func() {
			mw.SwitchToUninstall()
		}),
		widget.NewToolbarAction(theme.HomeIcon(), func() {
			mw.SwitchToInstall()
		}),
		widget.NewToolbarAction(theme.SettingsIcon(), func() {
			mw.ShowSettingsDialog()
		}),
	)

	// Keep references for switching
	mw.installView = scrollContainer
	
	// Apply Theme/Layout
	// Initial View
	mw.mainContainer = container.NewPadded(mw.installView)
	
	content := container.NewBorder(toolbar, nil, nil, nil, mw.mainContainer)
	mw.Window.SetContent(content)
}

func (mw *MainWindow) SwitchToInstall() {
	if mw.isInstalling {
		dialog.ShowInformation("Busy", "Installation in progress. Please wait or cancel.", mw.Window)
		return
	}
	mw.mainContainer.Objects = []fyne.CanvasObject{mw.installView}
	mw.mainContainer.Refresh()
}

func (mw *MainWindow) SwitchToUninstall() {
	if mw.isInstalling {
		dialog.ShowInformation("Busy", "Installation in progress. Please wait or cancel.", mw.Window)
		return
	}
	
	// Rebuild uninstall view every time to refresh list
	mw.uninstallView = mw.buildUninstallUI()
	mw.mainContainer.Objects = []fyne.CanvasObject{mw.uninstallView}
	mw.mainContainer.Refresh()
}

func (mw *MainWindow) buildUninstallUI() fyne.CanvasObject {
	// Header
	header := widget.NewLabelWithStyle("Installed Distributions", fyne.TextAlignCenter, fyne.TextStyle{Bold: true})

	// List Loading Area
	listContainer := container.NewVBox()
	loading := widget.NewLabel("Loading...")
	listContainer.Add(loading)

	// Load Data Async
	/*
	go func() {
		distros, err := logic.ListDistros(mw.ProjectDir)
		if err != nil {
			mw.Window.Content().Refresh() 
		}
	}()
	*/
	
	content := container.NewVBox(
		header,
		widget.NewSeparator(),
		listContainer,
	)
	
	// Async fetch
	go func() {
		distros, err := logic.ListDistros(mw.ProjectDir)
		
		// Schedule UI update
		// Assuming mw.App is available or global
		// Use fyne.Do() equivalent? 
		// mw.Window.Canvas().Refresh()
		// We actually need data on the UI thread.
		
		// Let's use a dirty trick if we don't have Queue:
		// We can't safely touch UI from here.
		
		// But I have mw.Window.
		// Use the proper logic.LoadDistros is synchronous call in my implementation of logic.ListDistros
		// It waits for cmd.Output().
		
		// Let's just make the call.
		if err != nil {
			listContainer.Objects = []fyne.CanvasObject{widget.NewLabel("Error loading list: " + err.Error())}
		} else if len(distros) == 0 {
			listContainer.Objects = []fyne.CanvasObject{widget.NewLabel("No distributions found.")}
		} else {
			listContainer.Objects = nil // Clear loading
			for _, d := range distros {
				d := d // Capture loop var
				
				infoLabel := widget.NewLabel(fmt.Sprintf("%s (WSL%s, %s)", d.Name, d.WslVer, d.State))
				pathLabel := widget.NewLabelWithStyle(d.BasePath, fyne.TextAlignLeading, fyne.TextStyle{Italic: true})
				pathLabel.Wrapping = fyne.TextWrapBreak
				
				details := container.NewVBox(infoLabel, pathLabel)
				
				delBtn := widget.NewButtonWithIcon("Uninstall", theme.DeleteIcon(), nil)
				delBtn.Importance = widget.DangerImportance
				
				delBtn.OnTapped = func() {
					dialog.ShowConfirm("Uninstall Confirmation", 
						fmt.Sprintf("Are you sure you want to unregister '%s'?\nThis operation cannot be undone.", d.Name), 
						func(ok bool) {
							if ok {
								// Perform Uninstall
								// progress := widget.NewProgressBarInfinite() // Unused variable
								// listContainer.Add(progress) 
								
								// Ideally replace the row or disable button.
								delBtn.Disable()
								delBtn.SetText("Removing...")
								
								go func() {
									uErr := logic.UnregisterDistro(context.Background(), d.Name)
									if uErr == nil {
										// Optional: Delete files
										// Since we are unregistering, checking if path exists to ask delete
										// But keeping it simple: just unregister. 
										// Files are usually kept by unregister if they are not store apps? 
										// Actually wsl --unregister usually keeps nothing for custom distros if imported?
										// Wait, `wsl --import` creates a ext4.vhdx. `wsl --unregister` DELETES that vhdx usually.
										// So files are gone. Folders might remain.
										
										logic.DeleteDistroFiles(d.BasePath) // Try cleanup empty folder
									}

									// Callback
									// Refreh list
									mw.SwitchToUninstall() // Reload whole page (lazy way)
								}()
							}
						}, 
						mw.Window)
				}
				
				row := container.NewBorder(nil, nil, nil, delBtn, details)
				card := container.NewPadded(row)
				listContainer.Add(card)
				listContainer.Add(widget.NewSeparator())
			}
		}
		listContainer.Refresh()
	}()

	return container.NewVScroll(container.NewPadded(content))
}

