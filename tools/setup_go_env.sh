#!/bin/bash
# One-time setup script for DistroNexus Go UI development environment

set -e

# 1. Check Golang installation
echo "Checking Go installation..."
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
    if [ -w /usr/local ]; then
        rm -rf /usr/local/go && tar -C /usr/local -xzf "/tmp/$TAR_NAME"
    else
        sudo rm -rf /usr/local/go && sudo tar -C /usr/local -xzf "/tmp/$TAR_NAME"
    fi
    
    rm "/tmp/$TAR_NAME"
    
    # Add to current PATH
    export PATH=$PATH:/usr/local/go/bin
    
    # Persist in .bashrc
    PROFILE_FILE="$HOME/.bashrc"
    if [ -f "$PROFILE_FILE" ] && ! grep -q "/usr/local/go/bin" "$PROFILE_FILE"; then
        echo 'export PATH=$PATH:/usr/local/go/bin' >> "$PROFILE_FILE"
        echo "Added Go to PATH in $PROFILE_FILE"
    fi
    
    echo "Go installed successfully."
fi
GO_VERSION=$(go version)
echo "Found: $GO_VERSION"

# 2. Check Fyne system dependencies (Linux only)
# Fyne requires C compiler and graphics libraries on Linux
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    echo "Checking Linux system dependencies for Fyne..."
    
    MISSING_DEPS=()
    
    # Check for GCC
    if ! command -v gcc &> /dev/null; then
        MISSING_DEPS+=("gcc")
    fi

    # Quick check for common package managers to suggest commands
    if command -v dpkg &> /dev/null; then
        # Debian/Ubuntu
        dpkg -s libgl1-mesa-dev &> /dev/null || MISSING_DEPS+=("libgl1-mesa-dev")
        dpkg -s xorg-dev &> /dev/null || MISSING_DEPS+=("xorg-dev")
    elif command -v rpm &> /dev/null; then
        # Fedora/RHEL (approximate check)
        echo "Note: On Fedora/RHEL, ensure 'libX11-devel libXcursor-devel libXrandr-devel libXinerama-devel mesa-libGL-devel libXi-devel' are installed."
    fi

    if [ ${#MISSING_DEPS[@]} -ne 0 ]; then
        echo "----------------------------------------------------------------"
        echo "WARNING: Missing system dependencies likely needed for Fyne:"
        printf " - %s\n" "${MISSING_DEPS[@]}"
        echo ""
        if command -v apt-get &> /dev/null; then
            echo "Try running:"
            echo "sudo apt-get install gcc libgl1-mesa-dev xorg-dev"
        fi
        echo "----------------------------------------------------------------"
        read -p "Continue anyway? (y/N) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            exit 1
        fi
    else
        echo "System dependencies look OK."
    fi
fi

# 3. Initialize Go Module
echo "Checking Go Module..."
if [ ! -f "go.mod" ]; then
    echo "Initializing new module 'distronexus-gui'..."
    go mod init distronexus-gui
else
    echo "go.mod already exists. Skipping init."
fi

# 4. Install Fyne Library
echo "Downloading Fyne toolkit (v2)..."
go get fyne.io/fyne/v2
go mod tidy

# 5. Install Fyne CLI tool (optional, for bundling)
echo "Installing Fyne CLI helper..."
go install fyne.io/fyne/v2/cmd/fyne@latest

echo "------------------------------------------------"
echo "Setup Complete!"
echo "You can now verify the setup by running a test."
echo "------------------------------------------------"
