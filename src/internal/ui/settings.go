package ui

import (
	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/dialog"
	"fyne.io/fyne/v2/theme"
	"fyne.io/fyne/v2/widget"
)

// ShowSettingsDialog displays a modal dialog for editing global settings
func (mw *MainWindow) ShowSettingsDialog() {
	// Create entry fields and pickers
	installPathEntry := widget.NewEntry()
	installPathEntry.SetText(mw.Settings.DefaultInstallPath)
	btnPickRec := widget.NewButtonWithIcon("", theme.FolderOpenIcon(), func() {
		dialog.ShowFolderOpen(func(uri fyne.ListableURI, err error) {
			if uri != nil {
				installPathEntry.SetText(uri.Path())
			}
		}, mw.Window)
	})
	installPathContainer := container.NewBorder(nil, nil, nil, btnPickRec, installPathEntry)

	distroCachePathEntry := widget.NewEntry()
	distroCachePathEntry.SetText(mw.Settings.DistroCachePath)
	btnPickCache := widget.NewButtonWithIcon("", theme.FolderOpenIcon(), func() {
		dialog.ShowFolderOpen(func(uri fyne.ListableURI, err error) {
			if uri != nil {
				distroCachePathEntry.SetText(uri.Path())
			}
		}, mw.Window)
	})
	cachePathContainer := container.NewBorder(nil, nil, nil, btnPickCache, distroCachePathEntry)

	defaultDistroEntry := widget.NewEntry()
	defaultDistroEntry.SetText(mw.Settings.DefaultDistro)

	distroSourceEntry := widget.NewEntry()
	distroSourceEntry.SetPlaceHolder("Default: Microsoft Official GitHub")
	distroSourceEntry.SetText(mw.Settings.DistroSourceUrl)

	terminalPathEntry := widget.NewEntry()
	terminalPathEntry.SetPlaceHolder("Default: ~ (User Home)")
	terminalPathEntry.SetText(mw.Settings.DefaultTerminalStartPath)
	btnPickTerminal := widget.NewButtonWithIcon("", theme.FolderOpenIcon(), func() {
		dialog.ShowFolderOpen(func(uri fyne.ListableURI, err error) {
			if uri != nil {
				terminalPathEntry.SetText(uri.Path())
			}
		}, mw.Window)
	})
	terminalPathContainer := container.NewBorder(nil, nil, nil, btnPickTerminal, terminalPathEntry)

	// Reset Button
	btnReset := widget.NewButton("Reset to Defaults", func() {
		dialog.ShowConfirm("Reset Settings", "Are you sure you want to restore default settings?", func(ok bool) {
			if ok {
				// Restore defaults matching mainwindow.go logic
				installPathEntry.SetText("D:\\WSL")
				distroCachePathEntry.SetText("distro_cache")
				defaultDistroEntry.SetText("Ubuntu-24.04")
				distroSourceEntry.SetText("") // Empty defaults to MS Official in logic
				terminalPathEntry.SetText("") // Empty defaults to ~
			}
		}, mw.Window)
	})
	btnReset.Importance = widget.DangerImportance

	// Create a custom content form
	items := []*widget.FormItem{
		widget.NewFormItem("Default Install Path", installPathContainer),
		widget.NewFormItem("Distro Cache Path", cachePathContainer),
		widget.NewFormItem("Default Quick Distro", defaultDistroEntry),
		widget.NewFormItem("Update Source URL", distroSourceEntry),
		widget.NewFormItem("Default Terminal Path", terminalPathContainer),
		widget.NewFormItem("", btnReset),
	}

	// Create and show dialog
	d := dialog.NewForm("Global Settings", "Save", "Cancel", items, func(confirm bool) {
		if confirm {
			// update struct
			mw.Settings.DefaultInstallPath = installPathEntry.Text
			mw.Settings.DistroCachePath = distroCachePathEntry.Text
			mw.Settings.DefaultDistro = defaultDistroEntry.Text
			mw.Settings.DistroSourceUrl = distroSourceEntry.Text
			mw.Settings.DefaultTerminalStartPath = terminalPathEntry.Text

			// Persist to disk
			err := mw.Config.SaveSettings(mw.Settings)
			if err != nil {
				dialog.ShowError(err, mw.Window)
			} else {
				// Optional: Show success or just log
				if mw.LogArea != nil {
					mw.LogArea.Append("Settings saved successfully.\n")
				}
			}
		}
	}, mw.Window)

	d.Resize(fyne.NewSize(600, 500))
	d.Show()
}
