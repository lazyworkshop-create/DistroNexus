#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

RUST_CHANNEL="${RUST_CHANNEL:-stable}"

if ! command_exists rustup; then
  retry 3 3 bash -c "curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y"
fi

# shellcheck source=/dev/null
source "$HOME/.cargo/env"

rustup toolchain install "${RUST_CHANNEL}"
rustup default "${RUST_CHANNEL}"

rustc --version
cargo --version
log_info "rust-dev completed"
