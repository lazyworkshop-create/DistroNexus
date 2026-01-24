#!/bin/bash
# One-time setup script for DistroNexus Go UI development environment

set -e

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
PROJECT_ROOT=$(dirname "$SCRIPT_DIR")
SRC_DIR="$PROJECT_ROOT/src"

echo "=== DistroNexus Environment Setup ==="

# 1. Check Golang installation
echo "[1/4] Checking Go installation..."
if ! command -v go &> /dev/null; then
    echo "Go is not installed. Attempting automatic installation..."
    
    # Define version to install
    GO_DL_VERSION="1.22.0"
    OS_TYPE=$(uname -s | tr '[:upper:]' '[:lower:]')
    ARCH_TYPE=$(uname -m)
    
    case "$ARCH_TYPE" in
        x86_64) GO_ARCH="amd64" ;;
        aarch64) GO_ARCH="arm64" ;;
        *) echo "Unsupported architecture: $ARCH_TYPE"; exit 1 ;;
    esac
    
    TAR_NAME="go$GO_DL_VERSION.$OS_TYPE-$GO_ARCH.tar.gz"
    URL="https://go.dev/dl/$TAR_NAME"
    
    echo "Downloading Go $GO_DL_VERSION ($OS_TYPE/$GO_ARCH)..."
    if command -v wget &> /dev/null; then
        wget -q "$URL" -O "/tmp/$TAR_NAME"
    elif command -v curl &> /dev/null; then
        curl -s -L "$URL" -o "/tmp/$TAR_NAME"
    else
        echo "Error: Neither wget nor curl found. Cannot download Go."
        exit 1
    fi
    
    echo "Installing to /usr/local/go (requires root/sudo)..."
    # Remove existing install if present
    if [ -w /usr/local ]; then
        rm -rf /usr/local/go && tar -C /usr/local -xzf "/tmp/$TAR_NAME"
    else
        sudo rm -rf /usr/local/go && sudo tar -C /usr/local -xzf "/tmp/$TAR_NAME"
    fi
    
    rm "/tmp/$TAR_NAME"
    
    # Add to current PATH for this session
    export PATH=$PATH:/usr/local/go/bin
    
    # Persist in .bashrc if not already there
    PROFILE_FILE="$HOME/.bashrc"
    if [ -f "$PROFILE_FILE" ] && ! grep -q "/usr/local/go/bin" "$PROFILE_FILE"; then
        echo 'export PATH=$PATH:/usr/local/go/bin' >> "$PROFILE_FILE"
        echo "Added Go to PATH in $PROFILE_FILE"
    fi
    
    echo "Go installed successfully."
fi

# Ensure Go is in PATH for this script execution
export PATH=$PATH:/usr/local/go/bin

GO_VERSION=$(go version)
echo "Found: $GO_VERSION"

# 2. Check Fyne system dependencies (Linux only)
echo "[2/4] Checking System Dependencies..."
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    # Standard Fyne deps + Cross compilation deps + Packaging tools
    REQUIRED_PACKAGES="gcc libgl1-mesa-dev xorg-dev gcc-mingw-w64 zip"
    MISSING_PACKAGES=""
    
    if command -v dpkg &> /dev/null; then
        for pkg in $REQUIRED_PACKAGES; do
            if ! dpkg -s "$pkg" &> /dev/null 2>&1; then
                MISSING_PACKAGES="$MISSING_PACKAGES $pkg"
            fi
        done
        
        if [ -n "$MISSING_PACKAGES" ]; then
            echo "Missing packages:$MISSING_PACKAGES"
            echo "Installing via apt-get..."
            
            if [ -w /etc/apt ]; then
                 apt-get update && apt-get install -y $MISSING_PACKAGES
            else
                 sudo apt-get update && sudo apt-get install -y $MISSING_PACKAGES
            fi
        else
            echo "All system dependencies installed."
        fi
    else
        echo "Warning: Not a Debian/Ubuntu system. Automatic dependency installation skipped."
        echo "Please ensure you have: $REQUIRED_PACKAGES"
    fi
else
    echo "Not Linux, skipping system dependency check."
fi

# 3. Initialize/Update Go Module
echo "[3/4] Configuring Go Module..."
# Ensure src directory exists
mkdir -p "$SRC_DIR"
cd "$SRC_DIR"

if [ ! -f "go.mod" ]; then
    echo "Initializing new module 'distronexus-gui' in src/..."
    go mod init distronexus-gui
else
    echo "go.mod found in src/."
fi

# 4. Install Fyne Library & Tools
echo "[4/4] Installing/Updating Fyne..."
go get fyne.io/fyne/v2
go mod tidy

# Install Fyne CLI tool (optional, for bundling)
if ! command -v fyne &> /dev/null; then
    echo "Installing Fyne CLI helper..."
    # Old path: go install fyne.io/fyne/v2/cmd/fyne@latest
    # New path per deprecation warning:
    go install fyne.io/tools/cmd/fyne@latest
fi

# Suggest PATH update if needed
if ! command -v fyne &> /dev/null; then
    GOPATH_BIN="$(go env GOPATH)/bin"
    echo "WARNING: 'fyne' command installed to $GOPATH_BIN but not found in PATH."
    echo "Please add it to your PATH: export PATH=\$PATH:$GOPATH_BIN"
fi

echo "------------------------------------------------"
echo "Setup Complete!"
echo "Run './tools/build.sh' to compile the project."
echo "------------------------------------------------"
