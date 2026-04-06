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
