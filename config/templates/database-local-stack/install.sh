#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../common/lib.sh
source "${SCRIPT_DIR}/../common/lib.sh"

DB_COMPONENTS="${DB_COMPONENTS:-postgresql,redis,sqlite}"

ensure_apt_updated
IFS=',' read -r -a components <<< "$DB_COMPONENTS"

for component in "${components[@]}"; do
  case "${component}" in
    postgresql)
      ensure_package postgresql
      ;;
    mysql)
      ensure_package mysql-server
      ;;
    redis)
      ensure_package redis-server
      ;;
    mongodb)
      log_warn "mongodb package is distro-specific; skipping automatic install"
      ;;
    sqlite)
      ensure_package sqlite3
      ;;
    *)
      log_warn "Unknown DB component: ${component}"
      ;;
  esac
done

for svc in postgresql mysql redis-server; do
  if command_exists systemctl; then
    sudo systemctl enable --now "$svc" >/dev/null 2>&1 || true
    sudo systemctl is-active "$svc" >/dev/null 2>&1 && log_info "Service active: $svc" || true
  fi
done

log_info "database-local-stack completed"
