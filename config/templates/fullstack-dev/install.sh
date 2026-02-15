#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

log_info "Installing fullstack environment..."

bash "$SCRIPT_DIR/../dotnet-dev/install.sh"
bash "$SCRIPT_DIR/../nodejs-dev/install.sh"
bash "$SCRIPT_DIR/../python-dev/install.sh"
bash "$SCRIPT_DIR/../docker-dev/install.sh"

log_info "Fullstack environment installed"
