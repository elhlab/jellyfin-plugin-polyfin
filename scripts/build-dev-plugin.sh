#!/usr/bin/env bash
# Builds the plugin into docker/plugin for the dev Jellyfin container, then offers to restart it.
#
# Usage: build-dev-plugin.sh [--head]
#   --head  Build the last commit, ignoring uncommitted changes. Builds in a temporary
#           git worktree, so the working tree and staged changes are never touched.
set -euo pipefail

cd "$(dirname "$0")/.."
repo=$(pwd)

head_only=false
for arg in "$@"; do
    case "$arg" in
        --head) head_only=true ;;
        *) echo "Unknown option: $arg" >&2; exit 1 ;;
    esac
done

out="$repo/docker/plugin"
revision=$(git rev-parse HEAD)
source="$repo"

if [[ "$head_only" == true ]]; then
    # Resolve symlinks (macOS /var -> /private/var), or the build won't find the repo's .editorconfig.
    source=$(cd "$(mktemp -d)" && pwd -P)
    trap 'git -C "$repo" worktree remove --force "$source"' EXIT
    git worktree add --quiet --detach "$source" HEAD
elif [[ -n "$(git status --short)" ]]; then
    revision="$revision-dirty"
fi

# Clear the folder's contents rather than the folder itself, so the bind mount stays valid.
mkdir -p "$out"
find "$out" -mindepth 1 -delete

dotnet build "$source/Jellyfin.Plugin.Polyfin" -c Debug -o "$out" -p:SourceRevisionId="$revision"

# Only ask when run from a terminal, so a non-interactive run just builds.
if [[ -t 0 ]]; then
    read -r -p "Restart dev Jellyfin to load it? [y/N] " answer
    if [[ "$answer" == [yY] ]]; then
        docker compose restart jellyfin
    fi
fi
