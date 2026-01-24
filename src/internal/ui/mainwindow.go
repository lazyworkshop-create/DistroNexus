package ui

import (
	"context"
	"distronexus-gui/internal/config"
	"distronexus-gui/internal/model"
	"fmt"

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

	// Progress Control
	progress    *widget.ProgressBarInfinite
	statusLabel *widget.Label
	installBtn  *widget.Button

	mainContent *fyne.Container // container for swapping views

	cancelCtx       context.Context
	cancelFunc      context.CancelFunc
	isInstalling    bool
	RefreshHomeList func()
}

func NewMainWindow(app fyne.App, projectDir string) *MainWindow {
	mw := &MainWindow{
		App:        app,
		Window:     app.NewWindow("DistroNexus - The WSL Distro Manager"),
		ProjectDir: projectDir,
		Config:     config.NewLoader(projectDir),
	}
	mw.Window.Resize(fyne.NewSize(900, 650))
	return mw
}

func (mw *MainWindow) Init() {
	mw.Window.CenterOnScreen()

	var err error
	mw.Distros, err = mw.Config.LoadDistros()
	if err != nil {
		dialog.ShowError(err, mw.Window)
		mw.Distros = make(map[string]model.DistroConfig) // Prevent nil map
	}
	mw.Settings, err = mw.Config.LoadSettings()
	if err != nil {
		fmt.Println("Warning loading settings:", err)
		// Fallback to defaults if nil
		mw.Settings = &model.GlobalSettings{
			DefaultInstallPath: "D:\\WSL",
			DefaultDistro:      "Ubuntu-24.04",
			DistroCachePath:    "distro_cache",
		}
	} else if mw.Settings == nil {
		// Just in case LoadSettings returns nil, nil
		mw.Settings = &model.GlobalSettings{
			DefaultInstallPath: "D:\\WSL",
			DefaultDistro:      "Ubuntu-24.04",
			DistroCachePath:    "distro_cache",
		}
	}

	mw.buildUI()
	mw.Window.Show()
}

func (mw *MainWindow) buildUI() {
	// Root Container
	mw.mainContent = container.NewStack()

	// Create Views
	homeView := mw.makeHomeTab()

	// Set Initial View
	mw.mainContent.Objects = []fyne.CanvasObject{homeView}

	// Toolbar
	btnHome := widget.NewButtonWithIcon("", theme.HomeIcon(), func() {
		mw.mainContent.Objects = []fyne.CanvasObject{mw.makeHomeTab()}
		mw.mainContent.Refresh()
	})

	btnPackages := widget.NewButtonWithIcon("", theme.StorageIcon(), func() {
		mw.mainContent.Objects = []fyne.CanvasObject{mw.makePackageTab()}
		mw.mainContent.Refresh()
	})

	btnInstall := widget.NewButtonWithIcon("", theme.ContentAddIcon(), func() {
		mw.ShowInstallDialog("", "")
	})
	btnInstall.Importance = widget.HighImportance

	btnSettings := widget.NewButtonWithIcon("", theme.SettingsIcon(), func() {
		mw.ShowSettingsDialog()
	})

	toolbar := container.NewHBox(
		btnHome,
		btnPackages,
		layout.NewSpacer(),
		btnInstall,
		btnSettings,
	)

	rootLayout := container.NewBorder(toolbar, nil, nil, nil, mw.mainContent)
	mw.Window.SetContent(rootLayout)
}
