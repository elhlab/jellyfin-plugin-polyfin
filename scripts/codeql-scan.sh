#!/usr/bin/env bash
# Runs the same CodeQL query suite as .github/workflows/scan-codeql.yaml, locally.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

if ! command -v codeql &> /dev/null; then
    echo "codeql CLI not found. Install it with: brew install --cask codeql" >&2
    exit 1
fi

DB_DIR=.codeql-db
SARIF_OUT=.codeql-results.sarif

rm -rf "$DB_DIR"
codeql database create "$DB_DIR" --language=csharp \
    --command='dotnet build Jellyfin.Plugin.Polyfin.slnx /t:rebuild' \
    --overwrite

codeql database analyze "$DB_DIR" \
    codeql/csharp-queries:codeql-suites/csharp-security-and-quality.qls \
    --format=sarif-latest --output="$SARIF_OUT" --download

python3 -c "
import json
data = json.load(open('$SARIF_OUT'))
results = data['runs'][0]['results']
if not results:
    print('No findings.')
else:
    for r in results:
        loc = r['locations'][0]['physicalLocation']
        print(f\"{loc['artifactLocation']['uri']}:{loc['region']['startLine']} [{r['ruleId']}] {r['message']['text'][:100]}\")
    print(f'\n{len(results)} finding(s). Full details: $SARIF_OUT')
"
