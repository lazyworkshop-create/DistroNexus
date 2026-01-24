package ui

import (
	"context"
	"distronexus-gui/internal/logic"
	"distronexus-gui/internal/model"
	"fmt"
	"os"
	"sort"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/dialog"
	"fyne.io/fyne/v2/layout"
	"fyne.io/fyne/v2/theme"
	"fyne.io/fyne/v2/widget"
)

func (mw *MainWindow) makePackageTab() fyne.CanvasObject {
	// Root Container
	listContent := container.NewVBox()

	// Define the refresh logic
	// We use a channel or just re-execute if we want recursion,
	// but simple let-binding works if we structure it right.

	// We need to declare the variable before assigning the function to it so it can call itself if needed,
	// or be called by buttons.
	var refreshFunc func()

	refreshFunc = func() {
		// Reload Distros to get latest LocalPaths
		if d, err := mw.Config.LoadDistros(); err == nil {
			mw.Distros = d
		}

		listContent.Objects = nil // Clear

		// 1. Prepare Data
		var families []string
		for k := range mw.Distros {
			families = append(families, k)
		}
		sort.Strings(families)

		// Helper to check status (relies on LocalPath now)
		isCached := func(localPath string) (bool, string) {
			if localPath == "" {
				return false, ""
			}
			info, err := os.Stat(localPath)
			if err == nil && !info.IsDir() {
				sizeMB := float64(info.Size()) / 1024.0 / 1024.0
				return true, fmt.Sprintf("%.1f MB", sizeMB)
			}
			return false, ""
		}

		// 2. Iterate
		for _, fam := range families {
			fam := fam // Capture for closure
			dCfg := mw.Distros[fam]
			listContent.Add(widget.NewLabelWithStyle(dCfg.Name, fyne.TextAlignLeading, fyne.TextStyle{Bold: true}))

			// Sort versions
			var vKeys []string
			if dCfg.Versions != nil {
				for k := range dCfg.Versions {
					vKeys = append(vKeys, k)
				}
				sort.Strings(vKeys)
			}

			for _, vKey := range vKeys {
				vKey := vKey // Capture
				ver := dCfg.Versions[vKey]
				cached, sizeStr := isCached(ver.LocalPath)

				nameLabel := widget.NewLabel(ver.Name)

				sourceTxt := ver.Source
				if sourceTxt == "" {
					sourceTxt = "Official"
				}

				statusTxt := sourceTxt
				statusIcon := theme.DownloadIcon()
				if cached {
					statusTxt += " | Cached (" + sizeStr + ")"
					statusIcon = theme.FileIcon()
				}
				statusLabel := widget.NewLabelWithStyle(statusTxt, fyne.TextAlignTrailing, fyne.TextStyle{Italic: true})

				var actionContainer *fyne.Container

				if cached {
					btnInstall := widget.NewButtonWithIcon("Install", theme.ContentAddIcon(), func() {
						// Open standard install dialog pre-filled
						// We convert ID to Name if needed, but here we used 'fam' which is the key (Distro Name usually)
						// and 'ver' which is the Version struct.

						// Note: ShowInstallDialog expects (FamilyName, VersionName)
						// Ensure 'fam' matches what's in the Select options (dCfg.Name)
						mw.ShowInstallDialog(dCfg.Name, ver.Name)
					})
					btnInstall.Importance = widget.LowImportance

					btnDelete := widget.NewButtonWithIcon("", theme.DeleteIcon(), func() {
						dialog.ShowConfirm("Delete Cache", "Remove file "+ver.Filename+"?", func(ok bool) {
							if ok {
								os.Remove(ver.LocalPath)
								// Update Config
								ver.LocalPath = ""
								dCfg.Versions[vKey] = ver
								mw.Distros[fam] = dCfg
								mw.Config.SaveDistros(mw.Distros)
								refreshFunc()
							}
						}, mw.Window)
					})
					btnDelete.Importance = widget.LowImportance

					btnRedownload := widget.NewButtonWithIcon("", theme.ViewRefreshIcon(), func() {
						dialog.ShowConfirm("Redownload", "Replace existing file?", func(ok bool) {
							if ok {
								os.Remove(ver.LocalPath)
								showBlockingProgress("Downloading "+ver.Name+"...", mw.Window, func(log func(string)) error {
									return logic.DownloadDistroOnly(context.Background(), mw.ProjectDir, fam, vKey, log)
								}, refreshFunc)
							}
						}, mw.Window)
					})
					btnRedownload.Importance = widget.LowImportance

					actionContainer = container.NewHBox(btnInstall, btnRedownload, btnDelete)
				} else {
					btnDownload := widget.NewButtonWithIcon("", theme.DownloadIcon(), func() {
						showBlockingProgress("Downloading "+ver.Name+"...", mw.Window, func(log func(string)) error {
							return logic.DownloadDistroOnly(context.Background(), mw.ProjectDir, fam, vKey, log)
						}, refreshFunc)
					})
					btnDownload.Importance = widget.LowImportance
					actionContainer = container.NewHBox(btnDownload)
				}

				row := container.NewHBox(
					widget.NewIcon(statusIcon),
					nameLabel,
					layout.NewSpacer(),
					statusLabel,
					actionContainer,
				)

				// Wrap in Card for spacing and visual separation
				card := widget.NewCard("", "", container.NewPadded(row))

				// Add margin around card via container.NewPadded
				listContent.Add(container.NewPadded(card))
			}
		}

		// 3. Custom
		if len(mw.Settings.CustomPackages) > 0 {
			listContent.Add(widget.NewLabelWithStyle("Custom Sources", fyne.TextAlignLeading, fyne.TextStyle{Bold: true}))
			for _, cp := range mw.Settings.CustomPackages {
				delBtn := widget.NewButtonWithIcon("", theme.DeleteIcon(), func() {
					// Remove from slice logic needed
					var newSlice []model.CustomPackage
					for _, item := range mw.Settings.CustomPackages {
						if item.Name != cp.Name {
							newSlice = append(newSlice, item)
						}
					}
					mw.Settings.CustomPackages = newSlice
					mw.Config.SaveSettings(mw.Settings)
					refreshFunc()
				})

				row := container.NewHBox(
					widget.NewIcon(theme.FileIcon()),
					widget.NewLabel(cp.Name+" "+cp.Version),
					layout.NewSpacer(),
					widget.NewLabelWithStyle("User", fyne.TextAlignTrailing, fyne.TextStyle{Italic: true}),
					delBtn,
				)
				listContent.Add(row)
			}
		}
		listContent.Refresh()
	}

	refreshFunc()

	btnRefreshList := widget.NewButtonWithIcon("", theme.ViewRefreshIcon(), refreshFunc)

	// Update Sources Icon: Using SearchReplaceIcon (magnifier with arrows) to imply "Checking/Syncing updates"
	btnUpdateSources := widget.NewButtonWithIcon("", theme.SearchReplaceIcon(), func() {
		showBlockingProgress("Updating Sources...", mw.Window, func(log func(string)) error {
			srcUrl := mw.Settings.DistroSourceUrl
			log("Fetching distribution info from source...\n")
			return logic.UpdateDistroList(context.Background(), mw.ProjectDir, srcUrl, log)
		}, func() {
			// Reload distros in memory
			d, err := mw.Config.LoadDistros()
			if err == nil {
				mw.Distros = d
			}
			// Refresh UI
			refreshFunc()
			dialog.ShowInformation("Success", "Distribution list updated successfully.", mw.Window)
		})
	})

	// Download All Button
	btnDownloadAll := widget.NewButtonWithIcon("", theme.DownloadIcon(), func() {
		dialog.ShowConfirm("Download All", "Download all official distributions? This may take a long time and require significant disk space.", func(ok bool) {
			if ok {
				showBlockingProgress("Downloading All...", mw.Window, func(log func(string)) error {
					// We invoke the scripts/download_all_distros.ps1 via logic helper or direct exec
					// Since logic package handles downloads, we can implement a loop there or just call the PS script.
					// Let's iterate over known distros and call logic.DownloadDistroOnly sequentially to get better progress report.

					// Re-load distros in case logic state is stale
					distros := mw.Distros
					var downloadTasks []struct{ Fam, Ver string }

					for fam, dCfg := range distros {
						for vKey, ver := range dCfg.Versions {
							if ver.LocalPath == "" {
								downloadTasks = append(downloadTasks, struct{ Fam, Ver string }{fam, vKey})
							}
						}
					}

					for i, task := range downloadTasks {
						log(fmt.Sprintf("[%d/%d] Downloading %s...\n", i+1, len(downloadTasks), task.Ver))
						err := logic.DownloadDistroOnly(context.Background(), mw.ProjectDir, task.Fam, task.Ver, log)
						if err != nil {
							log(fmt.Sprintf("Error downloading %s: %v\n", task.Ver, err))
							// Continue or stop? Let's continue.
						}
					}
					return nil
				}, refreshFunc)
			}
		}, mw.Window)
	})

	btnAddCustom := widget.NewButtonWithIcon("", theme.ContentAddIcon(), func() {
		n := widget.NewEntry()
		n.SetPlaceHolder("Name")
		v := widget.NewEntry()
		v.SetPlaceHolder("Version")
		p := widget.NewEntry()
		p.SetPlaceHolder("Path/URL")
		dialog.ShowCustomConfirm("Add Custom", "Add", "Cancel", container.NewVBox(n, v, p), func(ok bool) {
			if ok {
				mw.Settings.CustomPackages = append(mw.Settings.CustomPackages, model.CustomPackage{Name: n.Text, Version: v.Text, PathOrUrl: p.Text})
				mw.Config.SaveSettings(mw.Settings)
				refreshFunc()
			}
		}, mw.Window)
	})

	headerToolbar := container.NewHBox(
		widget.NewLabelWithStyle("Package Library", fyne.TextAlignCenter, fyne.TextStyle{Bold: true}),
		layout.NewSpacer(),
		btnUpdateSources,
		btnDownloadAll,
		btnAddCustom,
		btnRefreshList,
	)

	scroll := container.NewVScroll(listContent)

	return container.NewBorder(headerToolbar, nil, nil, nil, scroll)
}
