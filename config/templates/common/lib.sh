#!/bin/bash
set -euo pipefail

log_info() {
  echo "[INFO] $*"
}

log_warn() {
  echo "[WARN] $*"
}

log_error() {
  echo "[ERROR] $*" >&2
}

command_exists() {
  command -v "$1" >/dev/null 2>&1
}

run_with_privilege() {
  if [ "$(id -u)" -eq 0 ]; then
    "$@"
  else
    sudo "$@"
  fi
}

detect_distro() {
  if [ -f /etc/os-release ]; then
    . /etc/os-release
    echo "${ID:-unknown}:${VERSION_CODENAME:-unknown}"
    return 0
  fi
  echo "unknown:unknown"
}

retry() {
  local attempts="${1:-3}"
  local delay="${2:-2}"
  shift 2

  local i=1
  until "$@"; do
    if [ "$i" -ge "$attempts" ]; then
      log_error "Command failed after ${attempts} attempts: $*"
      return 1
    fi
    log_warn "Attempt ${i}/${attempts} failed. Retrying in ${delay}s: $*"
    sleep "$delay"
    i=$((i+1))
  done
}

ensure_apt_updated() {
  retry 3 3 run_with_privilege apt-get update -y
}

ensure_package() {
  local pkg="$1"
  if dpkg -s "$pkg" >/dev/null 2>&1; then
    log_info "Package already installed: ${pkg}"
  else
    retry 3 3 run_with_privilege apt-get install -y "$pkg"
  fi
}

append_line_if_missing() {
  local line="$1"
  local target_file="$2"
  touch "$target_file"
  if ! grep -Fq "$line" "$target_file"; then
    echo "$line" >> "$target_file"
  fi
}

safe_write_file_if_missing() {
  local path="$1"
  local content="$2"
  if [ ! -f "$path" ]; then
    printf "%s" "$content" > "$path"
  fi
}
