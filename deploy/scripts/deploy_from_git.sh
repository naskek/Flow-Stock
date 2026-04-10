#!/usr/bin/env bash
set -euo pipefail

source "$(cd -- "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

usage() {
    cat <<EOF
Usage:
  bash deploy/scripts/deploy_from_git.sh
  bash deploy/scripts/deploy_from_git.sh <git-ref>

Behavior:
  - without arguments deploys ${FLOWSTOCK_DEFAULT_DEPLOY_REF}
  - with <git-ref> deploys an explicit tag / branch / commit
  - refuses to run on a dirty tracked worktree
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
    usage
    exit 0
fi

requested_ref="${1:-${FLOWSTOCK_DEFAULT_DEPLOY_REF}}"
deploy_stamp="$(date -u +%Y%m%dT%H%M%SZ)"
latest_success_file="$(release_state_file latest_success)"
previous_success_file="$(release_state_file previous_success)"
last_attempt_file="$(release_state_file last_attempt)"
history_file="$(release_history_file "$deploy_stamp")"
backup_path=""
current_commit=""
current_branch=""
target_commit=""
deployed_ref=""
default_mode="false"

on_error() {
    local exit_code=$?
    log "git-aware deployment failed"
    [[ -n "$requested_ref" ]] && log "requested ref: $requested_ref"
    [[ -n "$target_commit" ]] && log "resolved target commit: $target_commit"
    [[ -n "$backup_path" ]] && log "prepared backup path: $backup_path"
    compose ps || true
    compose logs --tail=80 migrator flowstock nginx postgres || true
    exit "$exit_code"
}

trap on_error ERR

ensure_docker
ensure_git_repo
ensure_git_clean_worktree
ensure_compose_config

log "fetching git refs from $FLOWSTOCK_GIT_REMOTE"
git_in_repo fetch --prune --tags "$FLOWSTOCK_GIT_REMOTE"

current_commit="$(git_in_repo rev-parse HEAD)"
current_branch="$(git_in_repo symbolic-ref --short -q HEAD || printf 'detached')"

if ! target_commit="$(git_in_repo rev-parse --verify "${requested_ref}^{commit}" 2>/dev/null)"; then
    fail "git ref not found: $requested_ref"
fi

if [[ -f "$latest_success_file" ]]; then
    unset deployed_commit deployed_ref backup_path previous_commit previous_branch deploy_kind deployed_at restored_from_dump
    # shellcheck disable=SC1090
    source "$latest_success_file"
    if [[ -n "${deployed_commit:-}" && "$deployed_commit" != "$current_commit" ]]; then
        log "warning: latest recorded deployed commit is $deployed_commit but repository HEAD is $current_commit"
    fi
fi

log "current git revision: $current_commit ($current_branch)"
log "requested git ref: $requested_ref"
log "target git revision: $target_commit"

if [[ "$current_commit" == "$target_commit" ]]; then
    log "repository already matches the requested revision; nothing to deploy"
    exit 0
fi

if git_in_repo merge-base --is-ancestor "$current_commit" "$target_commit" >/dev/null 2>&1; then
    log "commits to deploy:"
    git_in_repo log --oneline --no-merges "${current_commit}..${target_commit}" || true
else
    log "target revision is not a fast-forward of the current revision"
fi

write_release_state "$last_attempt_file" \
    attempted_at="$deploy_stamp" \
    requested_ref="$requested_ref" \
    previous_commit="$current_commit" \
    previous_branch="$current_branch" \
    target_commit="$target_commit"

if [[ "${1:-}" == "" ]]; then
    default_mode="true"
    if git_in_repo show-ref --verify --quiet "refs/heads/${FLOWSTOCK_GIT_BRANCH}"; then
        log "checking out local branch ${FLOWSTOCK_GIT_BRANCH}"
        git_in_repo checkout "$FLOWSTOCK_GIT_BRANCH"
        log "fast-forwarding ${FLOWSTOCK_GIT_BRANCH} to ${FLOWSTOCK_DEFAULT_DEPLOY_REF}"
        git_in_repo merge --ff-only "$FLOWSTOCK_DEFAULT_DEPLOY_REF"
    else
        log "creating local branch ${FLOWSTOCK_GIT_BRANCH} from ${FLOWSTOCK_DEFAULT_DEPLOY_REF}"
        git_in_repo checkout -b "$FLOWSTOCK_GIT_BRANCH" "$FLOWSTOCK_DEFAULT_DEPLOY_REF"
    fi
    deployed_ref="$FLOWSTOCK_DEFAULT_DEPLOY_REF"
else
    log "checking out detached revision $target_commit"
    git_in_repo checkout --detach "$target_commit"
    deployed_ref="$requested_ref"
fi

backup_path="$(resolve_backup_path "${FLOWSTOCK_BACKUP_OUTPUT_DIR}/git_deploy/FlowStock_${deploy_stamp}_${target_commit:0:12}.dump")"
log "starting deploy_update.sh"
export FLOWSTOCK_BACKUP_PATH_OVERRIDE="$backup_path"
bash "${SCRIPT_DIR}/deploy_update.sh"
unset FLOWSTOCK_BACKUP_PATH_OVERRIDE

if [[ -f "$latest_success_file" ]]; then
    cp "$latest_success_file" "$previous_success_file"
fi

write_release_state "$history_file" \
    deployed_at="$deploy_stamp" \
    deploy_kind="git_update" \
    requested_ref="$requested_ref" \
    deployed_ref="$deployed_ref" \
    deployed_commit="$target_commit" \
    previous_commit="$current_commit" \
    previous_branch="$current_branch" \
    backup_path="$backup_path" \
    default_mode="$default_mode" \
    git_remote="$FLOWSTOCK_GIT_REMOTE" \
    git_branch="$FLOWSTOCK_GIT_BRANCH"

cp "$history_file" "$latest_success_file"

log "git-aware deployment completed successfully"
log "deployed ref: $deployed_ref"
log "deployed commit: $target_commit"
log "pre-deploy backup: $backup_path"
log "release metadata: $history_file"
