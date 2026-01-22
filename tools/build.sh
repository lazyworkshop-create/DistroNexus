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
export PATH=$PATH:/usr/local/go/bin

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
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    echo "Building for Linux (amd64)..."
    go build -o "$OUTPUT_DIR/DistroNexus-Linux" ./cmd/gui/main.go
    echo "Success: $OUTPUT_DIR/DistroNexus-Linux"
fi

# --- Windows Build (Cross Compilation) ---
# Check for MinGW for Fyne Windows cross-compilation
if command -v x86_64-w64-mingw32-gcc &> /dev/null; then
    echo "Building for Windows (amd64)..."
    CC=x86_64-w64-mingw32-gcc CGO_ENABLED=1 GOOS=windows GOARCH=amd64 \
        go build -ldflags -H=windowsgui -o "$OUTPUT_DIR/DistroNexus.exe" ./cmd/gui/main.go
    echo "Success: $OUTPUT_DIR/DistroNexus.exe"
else
    echo "Skipping Windows cross-build: x86_64-w64-mingw32-gcc not found."
    echo "To enable Windows build on Linux, install mingw-w64."
    echo "  Ubuntu/Debian: sudo apt-get install gcc-mingw-w64"
fi

echo "=== Build Finished ==="
