#!/usr/bin/env bash
set -euo pipefail

# Worker entrypoint — validates mounts and invokes Claude Code CLI
# Called inside an ephemeral Docker container launched by the Nexus spoke.

PROMPT_FILE="/workspace/prompt.md"

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

# Configure git identity (set via env vars by spoke)
if [ -n "${GIT_AUTHOR_NAME:-}" ]; then
    git config --global user.name "$GIT_AUTHOR_NAME"
    git config --global user.email "${GIT_AUTHOR_EMAIL:-}"
fi

# Mark workspace as safe directory
git config --global --add safe.directory /workspace/repo

# SSH auth setup — spoke mounts key at /tmp/.ssh/id_key (read-only)
if [ -f "/tmp/.ssh/id_key" ]; then
    mkdir -p /tmp/.ssh_work
    cp /tmp/.ssh/id_key /tmp/.ssh_work/id_key
    chmod 600 /tmp/.ssh_work/id_key
    if [ -f "/tmp/.ssh/known_hosts" ]; then
        cp /tmp/.ssh/known_hosts /tmp/.ssh_work/known_hosts
    fi
    export GIT_SSH_COMMAND="ssh -i /tmp/.ssh_work/id_key -o StrictHostKeyChecking=accept-new -o UserKnownHostsFile=/tmp/.ssh_work/known_hosts"
fi

# Token auth setup — spoke passes GIT_TOKEN env var for HTTPS credential helper
if [ -n "${GIT_TOKEN:-}" ]; then
    git config --global credential.helper '!f() { echo "password=$GIT_TOKEN"; }; f'
fi

# Build claude command arguments
CLAUDE_ARGS=(
    "--output-format" "stream-json"
    "--permission-mode" "bypassPermissions"
    "--verbose"
    "-p" "$(cat "$PROMPT_FILE")"
)

# Change to repo directory if mounted
if [ -d "/workspace/repo" ]; then
    cd /workspace/repo
fi

echo "Starting Claude Code worker..."
echo "Job ID: ${JOB_ID:-unknown}"
echo "Job Type: ${JOB_TYPE:-unknown}"
echo "Project Key: ${PROJECT_KEY:-unknown}"

exec claude "${CLAUDE_ARGS[@]}" "${SKILLS_ARGS[@]}"
