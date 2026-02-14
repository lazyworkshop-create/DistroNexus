#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

SDK_NODE_CHANNEL="${SDK_NODE_CHANNEL:-lts/*}"
SDK_NODE_VERSION="${SDK_NODE_VERSION:-}"
GENERATE_NVMRC="${GENERATE_NVMRC:-true}"
NODE_PACKAGE_MANAGERS="${NODE_PACKAGE_MANAGERS:-npm,pnpm,yarn}"

export SDK_NODE_CHANNEL
export SDK_NODE_VERSION
export GENERATE_NVMRC
export NODE_PACKAGE_MANAGERS

bash "${SCRIPT_DIR}/../nodejs-dev/install.sh"

log_info "nodejs-multi-version-dev completed"
