#!/bin/bash
set -e

echo "Installing fullstack environment..."
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

bash "$SCRIPT_DIR/../dotnet-dev/install.sh"
bash "$SCRIPT_DIR/../nodejs-dev/install.sh"
bash "$SCRIPT_DIR/../python-dev/install.sh"
bash "$SCRIPT_DIR/../docker-dev/install.sh"

echo "Fullstack environment installed"
