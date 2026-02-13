#!/bin/bash
set -e

echo "Installing Python development environment..."
sudo apt-get update
sudo apt-get install -y python3 python3-pip python3-venv pipx

if ! command -v poetry >/dev/null 2>&1; then
  pipx install poetry
fi

echo "Python environment installed"
