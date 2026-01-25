#!/bin/bash

# Define colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${GREEN}==> Checking Website Environment Prerequisites...${NC}"

# Function to check command existence
check_command() {
    if ! command -v "$1" &> /dev/null; then
        echo -e "${RED}Error: $1 could not be found.${NC}"
        return 1
    else
        echo -e "${GREEN}✓ $1 is installed ($(command -v $1))${NC}"
        # Print version if possible
        if [ "$1" == "node" ]; then
            echo "  Node version: $(node --version)"
        elif [ "$1" == "npm" ]; then
            echo "  NPM version: $(npm --version)"
        fi
        return 0
    fi
}

# Check for Node.js
if ! check_command "node"; then
    echo -e "${RED}Please install Node.js (Version 18 or higher recommended).${NC}"
    echo "On Ubuntu/Debian/WSL, you can run:"
    echo "  curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -"
    echo "  sudo apt-get install -y nodejs"
    exit 1
fi

# Check for NPM
if ! check_command "npm"; then
    echo -e "${RED}Please install NPM.${NC}"
    exit 1
fi

# Locate the website directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
WEBSITE_DIR="$PROJECT_ROOT/website"

if [ ! -d "$WEBSITE_DIR" ]; then
    echo -e "${RED}Error: Website directory not found at $WEBSITE_DIR${NC}"
    exit 1
fi

echo -e "\n${GREEN}==> Installing Project Dependencies...${NC}"
cd "$WEBSITE_DIR"

if [ -f "package.json" ]; then
    npm install
    if [ $? -eq 0 ]; then
        echo -e "\n${GREEN}✓ Dependencies installed successfully.${NC}"
        echo "You can now run the website using: ./website/start_dev.sh"
    else
        echo -e "\n${RED}Error: Failed to install dependencies.${NC}"
        exit 1
    fi
else
    echo -e "${RED}Error: package.json not found in $WEBSITE_DIR${NC}"
    exit 1
fi
