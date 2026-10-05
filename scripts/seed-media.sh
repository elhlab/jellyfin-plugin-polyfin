#!/bin/sh
# Builds a fake Jellyfin library in the given folder.
# Every movie and episode is a hardlink to one small playable MKV (or a copy
# if hardlinking fails).
#
# The library is only rebuilt when this script changed since the last build,
# tracked by a checksum in <media folder>/.seed-hash. Pass --rebuild to
# rebuild anyway.
# A rebuild deletes the old library first, so it asks before doing that unless
# --yes is passed.
#
# Usage: seed-media.sh [--yes] [--rebuild] <media folder>
set -e

usage() {
  echo "Usage: seed-media.sh [--yes] [--rebuild] <media folder>"
  echo
  echo "Builds a fake Jellyfin library in <media folder>, e.g. docker/media."
  echo
  echo "  --yes      don't ask before deleting the old library"
  echo "  --rebuild  rebuild even if the library is up to date"
  echo "  --help     show this help"
}

yes=false
rebuild=false
while true; do
  case "$1" in
    --yes) yes=true ;;
    --rebuild) rebuild=true ;;
    --help) usage && exit 0 ;;
    -*) usage >&2 && exit 1 ;;
    *) break ;;
  esac
  shift
done

if [ -z "$1" ]; then
  usage >&2
  exit 1
fi

script_hash=$(cksum < "$0")
media="$1"
source_file="$media/source.mkv"
hash_file="$media/.seed-hash"

if [ "$yes" != true ]; then
  echo "You probably want to run this inside the dev Jellyfin container"
  echo "('docker compose up' already does that for you)."
  echo "It deletes and rebuilds $media/movies and $media/series."
  if [ ! -t 0 ]; then
    echo "Pass --yes to skip this check." >&2
    exit 1
  fi
  printf 'Continue anyway? [y/N] '
  read -r answer
  case "$answer" in
    y | Y | yes) ;;
    *) echo "Aborted." && exit 1 ;;
  esac
fi

if [ "$rebuild" != true ] && [ "$(cat "$hash_file" 2>/dev/null)" = "$script_hash" ]; then
  echo "Media library is up to date, skipping rebuild. (pass --rebuild to rebuild anyway)"
  exit 0
fi

mkdir -p "$media"
trap 'rm -f "$source_file"' EXIT
# Jellyfin's image ships its own ffmpeg and points JELLYFIN_FFMPEG at it
"${JELLYFIN_FFMPEG:-ffmpeg}" -y -loglevel error \
  -f lavfi -i "testsrc=duration=10:size=1280x720:rate=30" \
  -f lavfi -i "sine=frequency=1000:duration=10" \
  -c:v libx264 -pix_fmt yuv420p -c:a aac -shortest \
  -metadata:s:a:0 language=eng \
  "$source_file"

rm -f "$hash_file"
rm -rf "$media/movies" "$media/series"

link_source() {
  mkdir -p "$(dirname "$1")"
  ln "$source_file" "$1" 2>/dev/null || cp "$source_file" "$1"
}

# Movies: movies/<Title (Year)>/<Title (Year)>.mkv
while IFS= read -r title; do
  [ -z "$title" ] && continue
  link_source "$media/movies/$title/$title.mkv"
done <<'EOF'
Big Buck Bunny (2008)
Elephants Dream (2006)
Tears of Steel (2012)
Cosmos Laundromat (2015)
Night of the Living Dead (1968)
Nosferatu (1922)
Metropolis (1927)
The General (1926)
Charade (1963)
Spirited Away (2001)
Amélie (2001)
Parasite (2019)
Inception (2010)
EOF

# Series: series/<Title (Year)>/Season XX/<Title> SXXEYY.mkv
# Each line: <Title (Year)>|<seasons>|<episodes per season>
while IFS='|' read -r title seasons episodes; do
  [ -z "$title" ] && continue
  name="${title% (*}"
  s=1
  while [ "$s" -le "$seasons" ]; do
    season=$(printf '%02d' "$s")
    e=1
    while [ "$e" -le "$episodes" ]; do
      episode=$(printf '%02d' "$e")
      link_source "$media/series/$title/Season $season/$name S${season}E${episode}.mkv"
      e=$((e + 1))
    done
    s=$((s + 1))
  done
done <<'EOF'
The Twilight Zone (1959)|2|4
Breaking Bad (2008)|2|3
Sherlock (2010)|2|3
Dark (2017)|2|3
Money Heist (2017)|1|4
EOF

echo "$script_hash" > "$hash_file"
