#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

PYTHON_CHANNEL="${SDK_PYTHON_CHANNEL:-3.12}"
PYTHON_VERSION="${SDK_PYTHON_VERSION:-$PYTHON_CHANNEL}"
PYTHON_ENV_TOOL="${PYTHON_ENV_TOOL:-poetry}"

log_info "Installing Python development environment (${PYTHON_VERSION})"

ensure_apt_updated
ensure_package build-essential
ensure_package curl
ensure_package git
ensure_package libssl-dev
ensure_package zlib1g-dev
ensure_package libbz2-dev
ensure_package libreadline-dev
ensure_package libsqlite3-dev
ensure_package libffi-dev
ensure_package xz-utils
ensure_package tk-dev
ensure_package pipx

if [ ! -d "$HOME/.pyenv" ]; then
  retry 3 3 bash -c "curl -fsSL https://pyenv.run | bash"
fi

append_line_if_missing 'export PYENV_ROOT="$HOME/.pyenv"' "$HOME/.bashrc"
append_line_if_missing 'export PATH="$PYENV_ROOT/bin:$PATH"' "$HOME/.bashrc"
append_line_if_missing 'eval "$(pyenv init -)"' "$HOME/.bashrc"

export PYENV_ROOT="$HOME/.pyenv"
export PATH="$PYENV_ROOT/bin:$PATH"
if command_exists pyenv; then
  eval "$(pyenv init -)"
fi

if ! pyenv versions --bare | grep -q "^${PYTHON_VERSION}$"; then
  pyenv install -s "${PYTHON_VERSION}"
fi
pyenv global "${PYTHON_VERSION}"

if [ "${GENERATE_PYTHON_VERSION_FILE:-false}" = "true" ] && [ ! -f .python-version ]; then
  echo "${PYTHON_VERSION}" > .python-version
  log_info "Generated .python-version"
fi

case "${PYTHON_ENV_TOOL}" in
  poetry)
    pipx list | grep -q poetry || pipx install poetry
    ;;
  pipenv)
    pipx list | grep -q pipenv || pipx install pipenv
    ;;
  none)
    ;;
  *)
    log_warn "Unknown PYTHON_ENV_TOOL: ${PYTHON_ENV_TOOL}"
    ;;
esac

python --version
log_info "Python environment installed"
