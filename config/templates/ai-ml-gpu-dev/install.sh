#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

ML_PROFILE="${ML_PROFILE:-CPU}"

ensure_apt_updated
ensure_package python3
ensure_package python3-pip
ensure_package python3-venv

python3 -m venv "$HOME/.venvs/ml" || true
# shellcheck source=/dev/null
source "$HOME/.venvs/ml/bin/activate"

pip install --upgrade pip

case "${ML_PROFILE}" in
  CPU)
    pip install numpy pandas scikit-learn
    ;;
  NVIDIA-CUDA)
    if ! command_exists nvidia-smi; then
      log_warn "nvidia-smi not found. Falling back to CPU profile."
      pip install numpy pandas scikit-learn
    else
      pip install numpy pandas
      log_info "NVIDIA profile selected. Install framework-specific CUDA wheels as needed."
    fi
    ;;
  DirectML-Python)
    pip install torch-directml || {
      log_warn "DirectML package install failed. Falling back to CPU profile."
      pip install numpy pandas scikit-learn
    }
    ;;
  *)
    log_warn "Unknown ML_PROFILE: ${ML_PROFILE}. Falling back to CPU profile."
    pip install numpy pandas scikit-learn
    ;;
esac

python -c "import sys; print('Python:', sys.version)"
log_info "ai-ml-gpu-dev completed"
