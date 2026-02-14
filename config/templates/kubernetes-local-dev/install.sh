#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

K8S_CLUSTER_MODE="${K8S_CLUSTER_MODE:-kind}"

ensure_apt_updated
ensure_package curl

if ! command_exists kubectl; then
  KUBECTL_VERSION="$(curl -L -s https://dl.k8s.io/release/stable.txt)"
  retry 3 3 curl -LO "https://dl.k8s.io/release/${KUBECTL_VERSION}/bin/linux/amd64/kubectl"
  chmod +x kubectl
  sudo mv kubectl /usr/local/bin/
fi

case "${K8S_CLUSTER_MODE}" in
  kind)
    if ! command_exists kind; then
      retry 3 3 curl -Lo ./kind https://kind.sigs.k8s.io/dl/v0.25.0/kind-linux-amd64
      chmod +x ./kind
      sudo mv ./kind /usr/local/bin/kind
    fi
    ;;
  k3d)
    if ! command_exists k3d; then
      retry 3 3 bash -c "curl -s https://raw.githubusercontent.com/k3d-io/k3d/main/install.sh | bash"
    fi
    ;;
  microk8s)
    if ! command_exists systemctl || ! systemctl status >/dev/null 2>&1; then
      log_error "microk8s requires systemd. Enable systemd in /etc/wsl.conf first."
      exit 1
    fi
    if ! command_exists snap; then
      log_error "snap is required for microk8s and is not available in this distro setup."
      exit 1
    fi
    sudo snap install microk8s --classic
    ;;
  *)
    log_error "Unknown K8S_CLUSTER_MODE: ${K8S_CLUSTER_MODE}"
    exit 1
    ;;
esac

kubectl version --client || true
log_info "kubernetes-local-dev completed"
