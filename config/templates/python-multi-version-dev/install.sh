#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

SDK_PYTHON_CHANNEL="${SDK_PYTHON_CHANNEL:-3.12}"
SDK_PYTHON_VERSION="${SDK_PYTHON_VERSION:-}"
PYTHON_ENV_TOOL="${PYTHON_ENV_TOOL:-poetry}"
GENERATE_PYTHON_VERSION_FILE="${GENERATE_PYTHON_VERSION_FILE:-true}"

export SDK_PYTHON_CHANNEL
export SDK_PYTHON_VERSION
export PYTHON_ENV_TOOL
export GENERATE_PYTHON_VERSION_FILE

bash "${SCRIPT_DIR}/../python-dev/install.sh"

log_info "python-multi-version-dev completed"
