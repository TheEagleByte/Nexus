#!/usr/bin/env bash
set -euo pipefail

# Worker entrypoint — clones repos, creates branches, then invokes Claude Code CLI.
# Called inside an ephemeral Docker container launched by the Nexus spoke.

PROMPT_FILE="/workspace/prompt.md"
REPO_CONFIG="/workspace/repo-config.json"

if [ ! -f "$PROMPT_FILE" ]; then
    echo "ERROR: Prompt file not found at $PROMPT_FILE" >&2
    exit 1
fi

# Build skills arguments for Claude Code CLI
SKILLS_ARGS=()

# Use merged CLAUDE.md if available (pre-merged by spoke with correct precedence)
if [ -f "/workspace/skills/CLAUDE.md" ]; then
    SKILLS_ARGS+=("--append-system-prompt-file" "/workspace/skills/CLAUDE.md")
fi

# Add skill subdirectories as plugin dirs for CC discovery
if [ -d "/workspace/skills/spoke" ] && [ "$(ls -A /workspace/skills/spoke 2>/dev/null)" ]; then
    SKILLS_ARGS+=("--plugin-dir" "/workspace/skills/spoke")
fi
if [ -d "/workspace/skills/project" ] && [ "$(ls -A /workspace/skills/project 2>/dev/null)" ]; then
    SKILLS_ARGS+=("--plugin-dir" "/workspace/skills/project")
fi

# Use /tmp as HOME since root filesystem is read-only
export HOME=/tmp

# Configure git identity (set via env vars by spoke — both must be present)
if [ -n "${GIT_AUTHOR_NAME:-}" ] && [ -n "${GIT_AUTHOR_EMAIL:-}" ]; then
    git config --global user.name "$GIT_AUTHOR_NAME"
    git config --global user.email "$GIT_AUTHOR_EMAIL"
elif [ -n "${GIT_AUTHOR_NAME:-}" ] || [ -n "${GIT_AUTHOR_EMAIL:-}" ]; then
    echo "ERROR: GIT_AUTHOR_NAME and GIT_AUTHOR_EMAIL must both be set" >&2
    exit 1
fi

# SSH auth setup — spoke mounts key at /tmp/.ssh/id_key (read-only)
if [ -f "/tmp/.ssh/id_key" ]; then
    mkdir -p /tmp/.ssh_work
    cp /tmp/.ssh/id_key /tmp/.ssh_work/id_key
    chmod 600 /tmp/.ssh_work/id_key
    if [ -f "/tmp/.ssh/known_hosts" ]; then
        cp /tmp/.ssh/known_hosts /tmp/.ssh_work/known_hosts
        export GIT_SSH_COMMAND="ssh -i /tmp/.ssh_work/id_key -o StrictHostKeyChecking=yes -o UserKnownHostsFile=/tmp/.ssh_work/known_hosts"
    else
        export GIT_SSH_COMMAND="ssh -i /tmp/.ssh_work/id_key -o StrictHostKeyChecking=accept-new"
    fi
fi

# Token auth — GIT_TOKEN is used per-clone below (no global credential helper)

# ---- Repository initialization phase ----
WORK_DIR="/workspace"

if [ -f "$REPO_CONFIG" ]; then
    echo "Initializing repositories from config..."

    REPO_COUNT=$(jq -r '.repositories | length' "$REPO_CONFIG")
    BRANCH_TEMPLATE=$(jq -r '.branchTemplate // "nexus/{type}/{key}"' "$REPO_CONFIG")
    JOB_TYPE=$(jq -r '.jobType // ""' "$REPO_CONFIG")
    PROJECT_KEY=$(jq -r '.projectKey // ""' "$REPO_CONFIG")
    CONFIG_JOB_ID=$(jq -r '.jobId // ""' "$REPO_CONFIG")

    # Build the branch name from template
    BRANCH_NAME="$BRANCH_TEMPLATE"
    BRANCH_NAME="${BRANCH_NAME//\{type\}/$JOB_TYPE}"
    BRANCH_NAME="${BRANCH_NAME//\{key\}/$PROJECT_KEY}"
    BRANCH_NAME="${BRANCH_NAME//\{job-id\}/$CONFIG_JOB_ID}"

    if [ "$REPO_COUNT" -eq 0 ]; then
        echo "WARNING: No repositories configured in repo-config.json" >&2
    fi

    for i in $(seq 0 $((REPO_COUNT - 1))); do
        REPO_NAME=$(jq -r ".repositories[$i].name" "$REPO_CONFIG")
        CLONE_URL=$(jq -r ".repositories[$i].cloneUrl" "$REPO_CONFIG")
        DEFAULT_BRANCH=$(jq -r ".repositories[$i].defaultBranch // \"main\"" "$REPO_CONFIG")

        if [[ ! "$REPO_NAME" =~ ^[A-Za-z0-9._-]+$ ]]; then
            echo "ERROR: Invalid repository name '$REPO_NAME'" >&2
            exit 1
        fi

        REPO_DIR="/workspace/repos/$REPO_NAME"

        # Redact URL for logging to avoid leaking embedded tokens
        SAFE_URL=$(echo "$CLONE_URL" | sed -E 's|://[^@]+@|://***@|')
        echo "Cloning $REPO_NAME from $SAFE_URL (branch: $DEFAULT_BRANCH)..."

        if [[ -n "${GIT_TOKEN:-}" && "$CLONE_URL" == https://* ]]; then
            CLONE_HOST="${CLONE_URL#https://}"
            CLONE_HOST="${CLONE_HOST%%/*}"
            if ! git -c "credential.https://$CLONE_HOST.helper=!f() { echo \"username=x-access-token\"; echo \"password=$GIT_TOKEN\"; }; f" \
                clone --branch "$DEFAULT_BRANCH" "$CLONE_URL" "$REPO_DIR"; then
                echo "ERROR: Failed to clone repository $REPO_NAME" >&2
                exit 1
            fi
        elif ! git clone --branch "$DEFAULT_BRANCH" "$CLONE_URL" "$REPO_DIR"; then
            echo "ERROR: Failed to clone repository $REPO_NAME" >&2
            exit 1
        fi

        # Mark as safe directory
        git config --global --add safe.directory "$REPO_DIR"

        # Create feature branch
        cd "$REPO_DIR"
        echo "Creating branch $BRANCH_NAME from $DEFAULT_BRANCH..."
        git checkout -b "$BRANCH_NAME"
        cd /workspace
    done

    # Set working directory based on repo count
    if [ "$REPO_COUNT" -eq 1 ]; then
        SINGLE_REPO_NAME=$(jq -r '.repositories[0].name' "$REPO_CONFIG")
        WORK_DIR="/workspace/repos/$SINGLE_REPO_NAME"
    elif [ "$REPO_COUNT" -gt 1 ]; then
        WORK_DIR="/workspace/repos"
    fi

    echo "Repository initialization complete. $REPO_COUNT repo(s) ready."
else
    echo "WARNING: No repo-config.json found, running without repository" >&2
fi

# Build claude command arguments
CLAUDE_ARGS=(
    "--output-format" "stream-json"
    "--permission-mode" "bypassPermissions"
    "--verbose"
    "-p" "$(cat "$PROMPT_FILE")"
)

# Change to working directory
cd "$WORK_DIR"

echo "Starting Claude Code worker..."
echo "Job ID: ${JOB_ID:-unknown}"
echo "Job Type: ${JOB_TYPE:-unknown}"
echo "Project Key: ${PROJECT_KEY:-unknown}"
echo "Working directory: $(pwd)"

exec claude "${CLAUDE_ARGS[@]}" "${SKILLS_ARGS[@]}"
