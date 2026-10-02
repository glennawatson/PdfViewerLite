#!/usr/bin/env bash
# Publishes the Native AOT build for a Linux runtime identifier into artifacts/<rid>.
# Usage: scripts/publish-linux.sh [linux-x64|linux-arm64]
set -euo pipefail

RID="${1:-linux-x64}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/artifacts/$RID"

rm -rf "$OUT"
dotnet publish "$ROOT/src/PdfViewerLite.App/PdfViewerLite.App.csproj" -c Release -r "$RID" -o "$OUT" "${@:2}"

# Debug symbols are kept next to the artifacts, not shipped.
mkdir -p "$ROOT/artifacts/symbols/$RID"
find "$OUT" -name '*.dbg' -exec mv {} "$ROOT/artifacts/symbols/$RID/" \;
echo "Published $RID to $OUT"
ls -la "$OUT"
