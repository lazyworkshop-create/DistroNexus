#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

INFRA_TOOLSET="${INFRA_TOOLSET:-kubectl,helm,terraform,jq,yq}"

ensure_apt_updated
ensure_package ca-certificates
ensure_package curl
ensure_package gnupg
ensure_package lsb-release

ensure_package jq

install_yq() {
  if command_exists yq; then
    log_info "yq already installed"
    return
  fi

  local yq_version="v4.44.3"
  retry 3 3 curl -L "https://github.com/mikefarah/yq/releases/download/${yq_version}/yq_linux_amd64" -o /tmp/yq
  chmod +x /tmp/yq
  run_with_privilege mv /tmp/yq /usr/local/bin/yq
}

install_kubectl() {
  if command_exists kubectl; then
    log_info "kubectl already installed"
    return
  fi

  local kubectl_version
  kubectl_version="$(curl -L -s https://dl.k8s.io/release/stable.txt)"
  retry 3 3 curl -LO "https://dl.k8s.io/release/${kubectl_version}/bin/linux/amd64/kubectl"
  chmod +x kubectl
  run_with_privilege mv kubectl /usr/local/bin/kubectl
}

install_helm() {
  if command_exists helm; then
    log_info "helm already installed"
    return
  fi

  local arch
  arch="$(dpkg --print-architecture)"
  local codename
  codename="$(. /etc/os-release && echo "${VERSION_CODENAME:-bookworm}")"

  retry 3 3 curl -fsSL https://baltocdn.com/helm/signing.asc | run_with_privilege gpg --dearmor -o /usr/share/keyrings/helm.gpg
  echo "deb [arch=${arch} signed-by=/usr/share/keyrings/helm.gpg] https://baltocdn.com/helm/stable/debian/ all main" | run_with_privilege tee /etc/apt/sources.list.d/helm-stable-debian.list >/dev/null
  ensure_apt_updated
  ensure_package helm
}

install_terraform() {
  if command_exists terraform; then
    log_info "terraform already installed"
    return
  fi

  local arch
  arch="$(dpkg --print-architecture)"
  local codename
  codename="$(. /etc/os-release && echo "${VERSION_CODENAME:-bookworm}")"

  retry 3 3 curl -fsSL https://apt.releases.hashicorp.com/gpg | run_with_privilege gpg --dearmor -o /usr/share/keyrings/hashicorp-archive-keyring.gpg
  echo "deb [arch=${arch} signed-by=/usr/share/keyrings/hashicorp-archive-keyring.gpg] https://apt.releases.hashicorp.com ${codename} main" | run_with_privilege tee /etc/apt/sources.list.d/hashicorp.list >/dev/null
  ensure_apt_updated
  ensure_package terraform
}

IFS=',' read -r -a requested_tools <<< "${INFRA_TOOLSET}"
for raw_tool in "${requested_tools[@]}"; do
  tool="$(echo "${raw_tool}" | xargs | tr '[:upper:]' '[:lower:]')"
  case "${tool}" in
    kubectl)
      install_kubectl
      ;;
    helm)
      install_helm
      ;;
    terraform)
      install_terraform
      ;;
    jq)
      ensure_package jq
      ;;
    yq)
      install_yq
      ;;
    "")
      ;;
    *)
      log_warn "Unknown tool in INFRA_TOOLSET ignored: ${tool}"
      ;;
  esac
done

for verify_tool in jq yq kubectl helm terraform; do
  if command_exists "${verify_tool}"; then
    log_info "${verify_tool} ready"
  fi
done

log_info "infra-cli-toolbox completed"
