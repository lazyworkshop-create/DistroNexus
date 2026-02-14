#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

JAVA_SDKMAN_VERSION="${JAVA_SDKMAN_VERSION:-21.0.5-tem}"
GENERATE_SDKMANRC="${GENERATE_SDKMANRC:-true}"

ensure_apt_updated
ensure_package unzip
ensure_package zip

if [ ! -d "$HOME/.sdkman" ]; then
  retry 3 3 bash -c "curl -s https://get.sdkman.io | bash"
fi

sdk_available=false
if [ -f "$HOME/.sdkman/bin/sdkman-init.sh" ]; then
  # shellcheck source=/dev/null
  set +e
  set +u
  source "$HOME/.sdkman/bin/sdkman-init.sh"
  set -u
  set -e
  if command_exists sdk; then
    sdk_available=true
  fi
fi

if [ "$sdk_available" = true ]; then
  set +u
  if ! sdk list java | grep -q "${JAVA_SDKMAN_VERSION}"; then
    log_warn "Requested Java version may not be listed by SDKMAN: ${JAVA_SDKMAN_VERSION}"
  fi

  sdk install java "${JAVA_SDKMAN_VERSION}" || sdk use java "${JAVA_SDKMAN_VERSION}" || true
  set -u
else
  log_warn "SDKMAN unavailable, using apt-based OpenJDK fallback"
  ensure_apt_updated
  ensure_package openjdk-21-jdk
fi

if [ "${GENERATE_SDKMANRC}" = "true" ] && [ ! -f .sdkmanrc ]; then
  cat > .sdkmanrc <<EOF
java=${JAVA_SDKMAN_VERSION}
EOF
  log_info "Generated .sdkmanrc"
fi

java -version || true
log_info "java-jvm-dev completed"
