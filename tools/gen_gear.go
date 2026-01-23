package main

import (
	"image"
	"image/color"
	"image/png"
	"math"
	"os"
)

func main() {
	w, h := 256, 256
	dc := image.NewRGBA(image.Rect(0, 0, w, h))

	cx, cy := float64(w)/2, float64(h)/2
	innerHole := 40.0
	radius := 90.0
	teeth := 8
	toothHeight := 25.0

	// Dark Grey Color for the gear
	col := color.RGBA{60, 60, 60, 255}

	for y := 0; y < h; y++ {
		for x := 0; x < w; x++ {
			dx := float64(x) - cx
			dy := float64(y) - cy
			dist := math.Sqrt(dx*dx + dy*dy)
			angle := math.Atan2(dy, dx)

			// Hole in middle
			if dist < innerHole {
				continue
			}

			// Base circle
			if dist < radius {
				dc.Set(x, y, col)
				continue
			}

			// Teeth
			if dist < radius+toothHeight {
				// Normalize angle 0..1
				normAngle := (angle + math.Pi) / (2 * math.Pi)
				// 8 teeth
				toothPhase := math.Mod(normAngle*float64(teeth), 1.0)

				// Simple block teeth
				if toothPhase > 0.25 && toothPhase < 0.75 {
					dc.Set(x, y, col)
				}
			}
		}
	}

	f, _ := os.Create("icon.png")
	defer f.Close()
	png.Encode(f, dc)
}
