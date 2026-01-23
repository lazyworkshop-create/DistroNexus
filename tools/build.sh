#!/bin/bash
# Build script for DistroNexus GUI

set -e

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
PROJECT_ROOT=$(dirname "$SCRIPT_DIR")
SRC_DIR="$PROJECT_ROOT/src"
OUTPUT_DIR="$PROJECT_ROOT/build"

echo "=== DistroNexus Build Tool ==="
echo "Project Root: $PROJECT_ROOT"

# Ensure Go is in PATH (common install location)
export PATH=$PATH:/usr/local/go/bin:$(go env GOPATH)/bin

# Ensure Output Directory
mkdir -p "$OUTPUT_DIR"

# Check Go
if ! command -v go &> /dev/null; then
    echo "Error: Go is not installed."
    exit 1
fi

# Enter Source Directory
cd "$SRC_DIR"

echo "Tidying modules..."
go mod tidy

# --- Linux Build ---
# Skipped: WSL2 management is Windows-only
# if [[ "$OSTYPE" == "linux-gnu"* ]]; then
#     echo "Building for Linux (amd64)..."
#     go build -o "$OUTPUT_DIR/DistroNexus-Linux" ./cmd/gui/main.go
#     echo "Success: $OUTPUT_DIR/DistroNexus-Linux"
# fi

# --- Windows Build (Cross Compilation) ---
# Check for MinGW for Fyne Windows cross-compilation
if command -v x86_64-w64-mingw32-gcc &> /dev/null; then
    echo "Building for Windows (amd64)..."
    
    # Use 'fyne package' to embed icon if available, else standard go build
    if command -v fyne &> /dev/null; then
        echo "Using Fyne CLI for packaging (with icon)..."
        # fyne package must be run where go.mod is? or main? 
        # Typically run in project root context but pointing to src.
        
        # We need to run inside src because of go.mod?
        pushd "$SRC_DIR" > /dev/null
        
        # fyne package creates the .exe in current dir by default
        # We specify source dir content.
        # Actually easiest to run on main package directly?
        # fyne package -os windows -icon ../tools/icon.svg -name DistroNexus
        
        # Note: fyne package looks for main package in current dir unless specified?
        # Let's assume we run it in src/cmd/gui? But go.mod is in src.
        
        # Best approach: Run in src.
        # Use .png path for icon
        CC=x86_64-w64-mingw32-gcc CGO_ENABLED=1 fyne package -os windows -icon "$PROJECT_ROOT/tools/icon.png" -name DistroNexus --src ./cmd/gui
        
        # It seems fyne package outputs to the src directory if specified with --src?
        # Check where it fell.
        if [ -f "DistroNexus.exe" ]; then
             mv DistroNexus.exe "$OUTPUT_DIR/DistroNexus.exe"
        elif [ -f "cmd/gui/DistroNexus.exe" ]; then
             mv cmd/gui/DistroNexus.exe "$OUTPUT_DIR/DistroNexus.exe"
        fi
        popd > /dev/null
    else
        echo "Fyne CLI not found, falling back to standard go build (no exe icon)..."
        CC=x86_64-w64-mingw32-gcc CGO_ENABLED=1 GOOS=windows GOARCH=amd64 \
            go build -ldflags -H=windowsgui -o "$OUTPUT_DIR/DistroNexus.exe" ./cmd/gui/main.go
    fi

    echo "Copying resources..."
    cp -r "$PROJECT_ROOT/config" "$OUTPUT_DIR/"
    cp -r "$PROJECT_ROOT/scripts" "$OUTPUT_DIR/"
    
    echo "Success: $OUTPUT_DIR/DistroNexus.exe"
else
    echo "Skipping Windows cross-build: x86_64-w64-mingw32-gcc not found."
    echo "To enable Windows build on Linux, install mingw-w64."
    echo "  Ubuntu/Debian: sudo apt-get install gcc-mingw-w64"
fi

echo "=== Build Finished ==="
