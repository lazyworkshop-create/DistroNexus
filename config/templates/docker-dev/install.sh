#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

RUNTIME_MODE="${CONTAINER_RUNTIME_MODE:-docker-engine}"

log_info "Installing container runtime mode: ${RUNTIME_MODE}"

case "${RUNTIME_MODE}" in
  docker-desktop)
    if command_exists docker; then
      log_info "Docker CLI already available via Docker Desktop integration"
    else
      log_warn "Docker Desktop integration mode selected but docker CLI was not found in this distro"
    fi
    ;;
  podman)
    ensure_apt_updated
    ensure_package podman
    podman --version
    ;;
  docker-engine|*)
    if command_exists docker; then
      log_info "Docker already installed"
      docker --version
      exit 0
    fi

    ensure_apt_updated
    ensure_package ca-certificates
    ensure_package curl
    ensure_package gnupg
    sudo install -m 0755 -d /etc/apt/keyrings

    if [ ! -f /etc/apt/keyrings/docker.gpg ]; then
      retry 3 3 bash -c "curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg"
      sudo chmod a+r /etc/apt/keyrings/docker.gpg
    fi

    if [ ! -f /etc/apt/sources.list.d/docker.list ]; then
      echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
    fi

    ensure_apt_updated
    retry 3 3 sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

    if [ -n "${SUDO_USER:-}" ]; then
      sudo usermod -aG docker "$SUDO_USER" || true
    fi

    docker --version
    ;;
esac

log_info "Container runtime setup completed"
