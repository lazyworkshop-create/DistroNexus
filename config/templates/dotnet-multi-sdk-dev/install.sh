#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

SDK_DOTNET_CHANNEL="${SDK_DOTNET_CHANNEL:-LTS}"
SDK_DOTNET_VERSION="${SDK_DOTNET_VERSION:-8.0}"

case "${SDK_DOTNET_CHANNEL}" in
  LTS)
    SDK_DOTNET_VERSION="${SDK_DOTNET_VERSION:-8.0}"
    ;;
  STS|Current)
    SDK_DOTNET_VERSION="${SDK_DOTNET_VERSION:-10.0}"
    ;;
  SpecificVersion)
    SDK_DOTNET_VERSION="${SDK_DOTNET_VERSION:-8.0}"
    ;;
  *)
    log_warn "Unknown SDK_DOTNET_CHANNEL=${SDK_DOTNET_CHANNEL}, fallback to LTS"
    SDK_DOTNET_VERSION="8.0"
    ;;
esac

export SDK_DOTNET_VERSION
bash "${SCRIPT_DIR}/../dotnet-dev/install.sh"

if [ "${GENERATE_GLOBAL_JSON:-false}" = "true" ] && [ ! -f global.json ]; then
cat > global.json <<EOF
{
  "sdk": {
    "version": "${SDK_DOTNET_VERSION}.0"
  }
}
EOF
  log_info "Generated global.json"
fi

dotnet --list-sdks || true
log_info "dotnet-multi-sdk-dev completed"
