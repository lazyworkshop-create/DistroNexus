#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

SDK_VERSION="${SDK_DOTNET_VERSION:-8.0}"
DISTRO_INFO="$(detect_distro)"
log_info "Installing .NET SDK ${SDK_VERSION} on ${DISTRO_INFO}"

if command_exists dotnet && dotnet --list-sdks | grep -q "^${SDK_VERSION}"; then
	log_info ".NET SDK ${SDK_VERSION} already installed"
	exit 0
fi

if [ ! -f /etc/apt/sources.list.d/microsoft-prod.list ] && [ ! -f /etc/apt/trusted.gpg.d/microsoft.gpg ]; then
	tmp_deb="/tmp/packages-microsoft-prod.deb"
	retry 3 3 wget "https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb" -O "${tmp_deb}"
	sudo dpkg -i "${tmp_deb}"
	rm -f "${tmp_deb}"
fi

ensure_apt_updated
retry 3 3 sudo apt-get install -y "dotnet-sdk-${SDK_VERSION}"

dotnet --list-sdks || true
log_info ".NET installed successfully"
