#!/usr/bin/env sh

set -eu

REMOTE_NAME="upstream"
UPSTREAM_URL="git@github.com:Monolith-Station/Monolith.git"

if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "error: run this script inside a git repository"
    exit 1
fi

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

if git remote get-url "$REMOTE_NAME" >/dev/null 2>&1; then
    CURRENT_URL="$(git remote get-url "$REMOTE_NAME")"
    if [ "$CURRENT_URL" != "$UPSTREAM_URL" ]; then
        echo "Updating $REMOTE_NAME URL"
        echo "  from: $CURRENT_URL"
        echo "    to: $UPSTREAM_URL"
        git remote set-url "$REMOTE_NAME" "$UPSTREAM_URL"
    else
        echo "Remote $REMOTE_NAME already configured"
    fi
else
    echo "Adding remote $REMOTE_NAME -> $UPSTREAM_URL"
    git remote add "$REMOTE_NAME" "$UPSTREAM_URL"
fi

echo "Done. Current remotes:"
git remote -v