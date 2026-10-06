#!/usr/bin/env bash
# Shows only Polyfin's log lines from the dev Jellyfin container, including the
# stack traces under them. Follows new lines by default.
#
# Usage: dev-logs.sh [docker logs options]
#   dev-logs.sh                  last 200 lines, then follow
#   dev-logs.sh --since 10m      everything from the last 10 minutes, no follow
set -euo pipefail

if [[ $# -eq 0 ]]; then
    set -- --tail 200 --follow
fi

# A Jellyfin log entry starts with "[HH:MM:SS.mmm]"; lines without that prefix
# (stack traces) belong to the entry above them.
docker logs "$@" polyfin-test-jellyfin 2>&1 | awk '
    /^\[[0-9:.]+\]/ { show = /Jellyfin\.Plugin\.Polyfin/ }
    show { print; fflush() }
'
