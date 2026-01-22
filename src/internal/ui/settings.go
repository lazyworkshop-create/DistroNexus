package ui

import (
	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/dialog"
	"fyne.io/fyne/v2/widget"
)

// ShowSettingsDialog displays a modal dialog for editing global settings
func (mw *MainWindow) ShowSettingsDialog() {
	// Create entry fields
	installPathEntry := widget.NewEntry()
	installPathEntry.SetText(mw.Settings.DefaultInstallPath)

	distroCachePathEntry := widget.NewEntry()
	distroCachePathEntry.SetText(mw.Settings.DistroCachePath)
	
	defaultDistroEntry := widget.NewEntry()
	defaultDistroEntry.SetText(mw.Settings.DefaultDistro)

	// Create a custom content form
	items := []*widget.FormItem{
		widget.NewFormItem("Default Install Path", installPathEntry),
		widget.NewFormItem("Distro Cache Path", distroCachePathEntry),
		widget.NewFormItem("Default Quick Distro", defaultDistroEntry),
	}

	// Create and show dialog
	d := dialog.NewForm("Global Settings", "Save", "Cancel", items, func(confirm bool) {
		if confirm {
			// update struct
			mw.Settings.DefaultInstallPath = installPathEntry.Text
			mw.Settings.DistroCachePath = distroCachePathEntry.Text
			mw.Settings.DefaultDistro = defaultDistroEntry.Text

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
	
	d.Resize(fyne.NewSize(400, 300))
	d.Show()
}
