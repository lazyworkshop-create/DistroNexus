package ui

import (
	"context"
	"distronexus-gui/internal/logic"
	"fmt"
	"image/color"
	"path/filepath"
	"strings"
	"time"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/canvas"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/data/binding"
	"fyne.io/fyne/v2/dialog"
	"fyne.io/fyne/v2/layout"
	"fyne.io/fyne/v2/theme"
	"fyne.io/fyne/v2/widget"
)

// Helper for blocking progress dialog
func showBlockingProgress(title string, win fyne.Window, task func(func(string)) error, onDone func()) {
	logBinding := binding.NewString()
	logLabel := widget.NewLabelWithData(logBinding)
	logLabel.Wrapping = fyne.TextWrapBreak

	// Scroll container for log
	scroll := container.NewScroll(logLabel)
	scroll.SetMinSize(fyne.NewSize(500, 300))

	progressBar := widget.NewProgressBarInfinite()

	content := container.NewBorder(progressBar, nil, nil, nil, scroll)

	d := dialog.NewCustomWithoutButtons(title, content, win)
	d.Show()

	go func() {
		err := task(func(line string) {
			current, _ := logBinding.Get()
			// Keep last ~2000 chars to avoid memory issues if long running?
			// Or just append.
			logBinding.Set(current + line)
		})

		d.Hide()

		if err != nil {
			dialog.ShowError(err, win)
		} else {
			// Optional success message?
			// dialog.ShowInformation(title, "Completed Successfully", win)
		}

		if onDone != nil {
			onDone()
		}
	}()
}

func (mw *MainWindow) makeHomeTab() fyne.CanvasObject {
	// Header Label (Title)
	headerBinding := binding.NewString()
	headerBinding.Set("Installed Distributions")
	headerLabel := widget.NewLabelWithData(headerBinding)
	headerLabel.Alignment = fyne.TextAlignCenter
	headerLabel.TextStyle = fyne.TextStyle{Bold: true}

	// Content Container (VBox)
	listContent := container.NewVBox()
	scroll := container.NewVScroll(listContent)

	// Refresh Logic
	var refreshFunc func(force bool)
	refreshFunc = func(force bool) {
		if force {
			showBlockingProgress("Scanning...", mw.Window, func(log func(string)) error {
				srcUrl := mw.Settings.DistroSourceUrl // "" defaults to internal script default
				_ = logic.ScanDistros(context.Background(), mw.ProjectDir, nil)
				return logic.UpdateDistroList(context.Background(), mw.ProjectDir, srcUrl, nil)
			}, func() {
				// Determine content
				distros, _ := logic.ListDistros(mw.ProjectDir, false)
				mw.rebuildHomeList(listContent, distros)
			})
		} else {
			// Async load to prevent UI freeze
			listContent.Objects = []fyne.CanvasObject{
				container.NewCenter(widget.NewProgressBarInfinite()),
			}
			listContent.Refresh()

			go func() {
				distros, _ := logic.ListDistros(mw.ProjectDir, false)
				mw.rebuildHomeList(listContent, distros)
			}()
		}
	}

	btnRefresh := widget.NewButtonWithIcon("", theme.ViewRefreshIcon(), func() {
		refreshFunc(true)
	})

	mw.RefreshHomeList = func() {
		refreshFunc(false)
	}

	// Trigger initial load
	refreshFunc(false)

	background := canvas.NewRectangle(color.Transparent)
	headerToolbar := container.NewBorder(nil, nil, headerLabel, btnRefresh, background)

	return container.NewBorder(
		headerToolbar,
		nil, nil, nil,
		scroll, // Use scroll with vbox instead of List
	)
}

func (mw *MainWindow) rebuildHomeList(containerBox *fyne.Container, distros []logic.WslInstance) {
	var objects []fyne.CanvasObject
	for _, d := range distros {
		item := mw.createDistroItem(d)
		// Wrap in padding to creating margin around the card
		objects = append(objects, container.NewPadded(item))
	}
	containerBox.Objects = objects
	containerBox.Refresh()
}

func (mw *MainWindow) createDistroItem(d logic.WslInstance) fyne.CanvasObject {
	// --- Row 1: Name (State) | Buttons ---

	// Name & State
	nameSeg := &widget.TextSegment{Style: widget.RichTextStyleStrong, Text: d.Name}

	// Status text removed from UI as requested (redundant with buttons)
	// statusText := fmt.Sprintf(" (%s)", d.State) ...

	title := widget.NewRichText(nameSeg)
	title.Wrapping = fyne.TextWrapOff

	// Toolbar Buttons
	btnOpen := widget.NewButtonWithIcon("", theme.MediaPlayIcon(), nil) // Start (Background)
	btnOpen.Importance = widget.LowImportance

	// Terminal Icon: ComputerIcon is the standard terminal/computer icon.
	btnTerminal := widget.NewButtonWithIcon("", theme.ComputerIcon(), nil)
	btnTerminal.Importance = widget.LowImportance

	btnStop := widget.NewButtonWithIcon("", theme.MediaStopIcon(), nil) // Stop
	btnStop.Importance = widget.LowImportance

	// Move Button: StorageIcon implies disk location
	btnMove := widget.NewButtonWithIcon("", theme.StorageIcon(), nil)
	btnMove.Importance = widget.LowImportance
	btnRename := widget.NewButtonWithIcon("", theme.DocumentCreateIcon(), nil)
	btnRename.Importance = widget.LowImportance
	btnCreds := widget.NewButtonWithIcon("", theme.AccountIcon(), nil)
	btnCreds.Importance = widget.LowImportance
	btnDelete := widget.NewButtonWithIcon("", theme.DeleteIcon(), nil)
	btnDelete.Importance = widget.LowImportance

	isRunning := (d.State == "Running")

	// Visibility Logic
	if isRunning {
		btnOpen.Hide()
		btnTerminal.Show()
		btnStop.Show()
		btnMove.Hide()
		btnRename.Hide()
		btnCreds.Hide()
		btnDelete.Hide()
	} else {
		btnOpen.Show()
		btnTerminal.Hide()
		btnStop.Hide()
		btnMove.Show()
		btnRename.Show()
		btnCreds.Show()
		btnDelete.Show()
	}

	// Handlers
	btnOpen.OnTapped = func() {
		// Start in background
		dialog.ShowConfirm("Start Instance", fmt.Sprintf("Start '%s' in background?", d.Name), func(ok bool) {
			if ok {
				showBlockingProgress("Starting...", mw.Window, func(log func(string)) error {
					return logic.StartDistro(context.Background(), mw.ProjectDir, d.Name, false, "")
				}, func() {
					// Give a moment for the state to propagate before refreshing
					time.Sleep(500 * time.Millisecond)
					mw.RefreshHomeList()
				})
			}
		}, mw.Window)
	}

	btnTerminal.OnTapped = func() {
		// Open Terminal
		// Use DefaultTerminalStartPath from settings
		startPath := mw.Settings.DefaultTerminalStartPath
		err := logic.StartDistro(context.Background(), mw.ProjectDir, d.Name, true, startPath)
		if err != nil {
			dialog.ShowError(err, mw.Window)
		}
	}

	btnStop.OnTapped = func() {
		dialog.ShowConfirm("Stop Instance", "Are you sure you want to force stop this instance?", func(ok bool) {
			if ok {
				showBlockingProgress("Stopping...", mw.Window, func(log func(string)) error {
					return logic.StopDistro(context.Background(), mw.ProjectDir, d.Name, log)
				}, func() { mw.RefreshHomeList() })
			}
		}, mw.Window)
	}

	btnMove.OnTapped = func() {
		dialog.ShowFolderOpen(func(uri fyne.ListableURI, err error) {
			if err != nil || uri == nil {
				return
			}
			newPath := filepath.Join(uri.Path(), d.Name)

			dialog.ShowConfirm("Move Instance", fmt.Sprintf("Move to %s?", newPath), func(ok bool) {
				if ok {
					showBlockingProgress("Moving Instance...", mw.Window, func(log func(string)) error {
						return logic.MoveDistro(context.Background(), mw.ProjectDir, d.Name, newPath, log)
					}, func() { mw.RefreshHomeList() })
				}
			}, mw.Window)
		}, mw.Window)
	}

	btnRename.OnTapped = func() {
		input := widget.NewEntry()
		input.SetText(d.Name)
		content := container.NewPadded(container.NewVBox(widget.NewLabel("New Name:"), input))
		// Use dialog.NewCustomConfirm to allow resizing
		dlog := dialog.NewCustomConfirm("Rename Instance", "Rename", "Cancel", content, func(ok bool) {
			if ok {
				newName := input.Text
				if newName == "" || newName == d.Name {
					return
				}
				showBlockingProgress("Renaming...", mw.Window, func(log func(string)) error {
					return logic.RenameDistro(context.Background(), mw.ProjectDir, d.Name, newName, "", log)
				}, func() { mw.RefreshHomeList() })
			}
		}, mw.Window)
		dlog.Resize(fyne.NewSize(500, 200))
		dlog.Show()
	}

	btnCreds.OnTapped = func() {
		uEntry := widget.NewEntry()
		uEntry.SetText(d.User)
		pEntry := widget.NewPasswordEntry()

		items := []*widget.FormItem{
			widget.NewFormItem("Username", uEntry),
			widget.NewFormItem("Password", pEntry),
		}

		dlog := dialog.NewForm("Credentials", "Set", "Cancel", items, func(ok bool) {
			if ok {
				showBlockingProgress("Setting Credentials...", mw.Window, func(log func(string)) error {
					return logic.SetDistroCredentials(context.Background(), mw.ProjectDir, d.Name, uEntry.Text, pEntry.Text, log)
				}, func() { mw.RefreshHomeList() })
			}
		}, mw.Window)
		dlog.Resize(fyne.NewSize(500, 300))
		dlog.Show()
	}

	btnDelete.OnTapped = func() {
		dialog.ShowConfirm("Uninstall", "Permanently delete this distribution?", func(ok bool) {
			if ok {
				showBlockingProgress("Uninstalling...", mw.Window, func(log func(string)) error {
					return logic.UnregisterDistro(context.Background(), mw.ProjectDir, d.Name, true, log)
				}, func() { mw.RefreshHomeList() })
			}
		}, mw.Window)
	}

	// --- Layout Assembly ---

	// Buttons Container
	btnBox := container.NewHBox(
		btnOpen, btnTerminal, btnStop,
		btnMove, btnRename, btnCreds, btnDelete,
	)

	// Row 1
	// Using NewBorder puts title in Left (aligned center vertically by default in Border?)
	// To visually center the text vertically against the buttons, we can wrap the text in a center layout
	// or rely on Border. Fyne's Border layout usually fills height.
	// HBox usually centers vertically.
	// Let's try combining them in an HBox directly if we want tight packing, or use Border.
	// The user request "Vertical Center" suggests it might have been top-aligned.
	// RichText is often top-aligned.
	// We wrap RichText in a Center Layout.
	titleCentered := container.NewVBox(layout.NewSpacer(), title, layout.NewSpacer())

	row1 := container.NewBorder(nil, nil, titleCentered, btnBox)

	// Row 2: Distro Name + Install Time
	osName := d.Release
	if osName == "" {
		osName = "Unknown Distro"
	}
	installTime := d.InstallTime
	if installTime == "" {
		installTime = "Unknown Time"
	}
	row2Text := fmt.Sprintf("%s · %s", osName, installTime)

	// Row 3: Install Path (Size)
	displayPath := strings.TrimPrefix(d.BasePath, "\\\\?\\")
	size := d.DiskSize
	if size == "" {
		size = "Unknown Size"
	}
	pathText := fmt.Sprintf("%s (%s)", displayPath, size)

	// Using RichText for rows 2 & 3 to control color (make them look secondary)
	// User requested darker text as Disabled was too faint. Using default Foreground.
	infoRich := widget.NewRichText(
		&widget.TextSegment{Text: row2Text + "\n", Style: widget.RichTextStyle{Inline: true}},
		&widget.TextSegment{Text: pathText, Style: widget.RichTextStyle{Inline: true}},
	)

	// Main Content
	content := container.NewVBox(
		row1,
		infoRich,
	)

	card := widget.NewCard("", "", content)
	return card
}
