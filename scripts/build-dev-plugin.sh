#!/usr/bin/env bash
# Builds the plugin into docker/plugin for the dev Jellyfin container, then offers to restart it.
set -euo pipefail

cd "$(dirname "$0")/.."

out=docker/plugin
revision=$(git rev-parse HEAD)
if [[ -n "$(git status --short)" ]]; then
    revision="$revision-dirty"
fi

# Clear the folder's contents rather than the folder itself, so the bind mount stays valid.
mkdir -p "$out"
find "$out" -mindepth 1 -delete

dotnet build Jellyfin.Plugin.Polyfin -c Debug -o "$out" -p:SourceRevisionId="$revision"

# Only ask when run from a terminal, so a non-interactive run just builds.
if [[ -t 0 ]]; then
    read -r -p "Restart dev Jellyfin to load it? [y/N] " answer
    if [[ "$answer" == [yY] ]]; then
        docker compose restart jellyfin
    fi
fi
