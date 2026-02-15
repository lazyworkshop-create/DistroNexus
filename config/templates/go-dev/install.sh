#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

GO_VERSION="${GO_VERSION:-1.23.6}"
GO_ARCH="${GO_ARCH:-linux-amd64}"

if command_exists go && go version | grep -q "go${GO_VERSION}"; then
  log_info "Go ${GO_VERSION} already installed"
  exit 0
fi

tmp_tar="/tmp/go${GO_VERSION}.${GO_ARCH}.tar.gz"
retry 3 3 wget "https://go.dev/dl/go${GO_VERSION}.${GO_ARCH}.tar.gz" -O "${tmp_tar}"
run_with_privilege rm -rf /usr/local/go
run_with_privilege tar -C /usr/local -xzf "${tmp_tar}"
rm -f "${tmp_tar}"

append_line_if_missing 'export PATH=$PATH:/usr/local/go/bin' "$HOME/.bashrc"
export PATH=$PATH:/usr/local/go/bin

go version
log_info "go-dev completed"
