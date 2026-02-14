#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

NVM_VERSION="${NVM_VERSION:-v0.40.4}"
NODE_CHANNEL="${SDK_NODE_CHANNEL:-lts/*}"
NODE_SPECIFIC_VERSION="${SDK_NODE_VERSION:-}"
NODE_TARGET="${NODE_SPECIFIC_VERSION:-$NODE_CHANNEL}"
PKG_MANAGERS="${NODE_PACKAGE_MANAGERS:-npm,pnpm,yarn}"

log_info "Installing Node.js with target '${NODE_TARGET}'"

if [ ! -s "$HOME/.nvm/nvm.sh" ]; then
	retry 3 3 bash -c "curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/${NVM_VERSION}/install.sh | bash"
fi

export NVM_DIR="$HOME/.nvm"
# shellcheck source=/dev/null
[ -s "$NVM_DIR/nvm.sh" ] && . "$NVM_DIR/nvm.sh"

if ! nvm ls | grep -q "$NODE_TARGET"; then
	nvm install "$NODE_TARGET"
fi
nvm use "$NODE_TARGET"

IFS=',' read -r -a managers <<< "$PKG_MANAGERS"
for manager in "${managers[@]}"; do
	case "${manager}" in
		npm)
			;;
		pnpm|yarn)
			npm list -g "${manager}" >/dev/null 2>&1 || npm install -g "${manager}"
			;;
		*)
			log_warn "Unknown package manager option: ${manager}"
			;;
	esac
done

if [ "${GENERATE_NVMRC:-false}" = "true" ] && [ ! -f .nvmrc ]; then
	echo "${NODE_TARGET}" > .nvmrc
	log_info "Generated .nvmrc"
fi

node -v
npm -v
log_info "Node.js environment installed"
