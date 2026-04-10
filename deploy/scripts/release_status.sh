#!/usr/bin/env bash
set -euo pipefail

source "$(cd -- "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

usage() {
    cat <<EOF
Usage:
  bash deploy/scripts/release_status.sh

Shows:
  - current git checkout on the server
  - latest and previous recorded release metadata
  - docker compose service status when available
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
    usage
    exit 0
fi

print_release_state() {
    local label="$1"
    local path="$2"

    printf '\n%s\n' "$label"
    if [[ ! -f "$path" ]]; then
        printf '  not recorded\n'
        return 0
    fi

    unset deployed_at deploy_kind requested_ref deployed_ref deployed_commit previous_commit previous_branch backup_path restored_from_dump git_remote git_branch
    # shellcheck disable=SC1090
    source "$path"

    [[ -n "${deployed_at:-}" ]] && printf '  deployed_at: %s\n' "$deployed_at"
    [[ -n "${deploy_kind:-}" ]] && printf '  deploy_kind: %s\n' "$deploy_kind"
    [[ -n "${requested_ref:-}" ]] && printf '  requested_ref: %s\n' "$requested_ref"
    [[ -n "${deployed_ref:-}" ]] && printf '  deployed_ref: %s\n' "$deployed_ref"
    [[ -n "${deployed_commit:-}" ]] && printf '  deployed_commit: %s\n' "$deployed_commit"
    [[ -n "${previous_commit:-}" ]] && printf '  previous_commit: %s\n' "$previous_commit"
    [[ -n "${previous_branch:-}" ]] && printf '  previous_branch: %s\n' "$previous_branch"
    [[ -n "${backup_path:-}" ]] && printf '  backup_path: %s\n' "$backup_path"
    [[ -n "${restored_from_dump:-}" ]] && printf '  restored_from_dump: %s\n' "$restored_from_dump"
}

ensure_git_repo
ensure_runtime_dirs

printf 'Repo: %s\n' "$FLOWSTOCK_REPO_DIR"
printf 'Default deploy ref: %s\n' "$FLOWSTOCK_DEFAULT_DEPLOY_REF"
printf 'Current branch: %s\n' "$(git_in_repo symbolic-ref --short -q HEAD || printf 'detached')"
printf 'Current commit: %s\n' "$(git_in_repo rev-parse HEAD)"

print_release_state "Latest successful release" "$(release_state_file latest_success)"
print_release_state "Previous successful release" "$(release_state_file previous_success)"
print_release_state "Last attempted deploy" "$(release_state_file last_attempt)"

if command -v docker >/dev/null 2>&1 && [[ -f "$FLOWSTOCK_COMPOSE_FILE" && -f "$FLOWSTOCK_ENV_FILE" ]]; then
    printf '\nCompose services\n'
    compose ps || true
fi
