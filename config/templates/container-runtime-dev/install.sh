#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

CONTAINER_RUNTIME_MODE="${CONTAINER_RUNTIME_MODE:-docker-engine}"
export CONTAINER_RUNTIME_MODE

bash "${SCRIPT_DIR}/../docker-dev/install.sh"

if [ "${CONTAINER_RUNTIME_MODE}" = "docker-desktop" ] && command_exists docker; then
  docker context ls || true
fi

if [ "${CONTAINER_RUNTIME_MODE}" = "docker-engine" ] && command_exists docker; then
  run_with_privilege service docker start || true
  docker info >/dev/null 2>&1 || log_warn "Docker daemon not running yet"
fi

if [ "${CONTAINER_RUNTIME_MODE}" = "podman" ] && command_exists podman; then
  podman info >/dev/null 2>&1 || log_warn "Podman info command failed"
fi

log_info "container-runtime-dev completed"
