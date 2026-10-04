#!/usr/bin/env bash
# Wraps a published osx build into PdfViewerLite.app and a disk image with an Applications shortcut.
# Usage: packaging/macos/build-app.sh <publish-dir> <version> <rid>
# The app is signed ad hoc so it runs on Apple silicon; set MACOS_SIGNING_IDENTITY to sign with a Developer ID.
set -euo pipefail

PUBLISH="${1:?publish directory}"
VERSION="${2:?version}"
RID="${3:?runtime identifier}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STAGE="$ROOT/artifacts/macos-$RID"
APP="$STAGE/PdfViewerLite.app"

rm -rf "$STAGE"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBLISH"/. "$APP/Contents/MacOS/"
cp "$ROOT/packaging/macos/PdfViewerLite.icns" "$APP/Contents/Resources/"
sed "s/@VERSION@/$VERSION/g" "$ROOT/packaging/macos/Info.plist" > "$APP/Contents/Info.plist"
plutil -lint "$APP/Contents/Info.plist"

if [[ -n "${MACOS_SIGNING_IDENTITY:-}" ]]; then
  # A Developer ID signature uses the hardened runtime, ready for notarisation.
  codesign --force --deep --options runtime --timestamp --entitlements "$ROOT/packaging/macos/PdfViewerLite.entitlements" \
    --sign "$MACOS_SIGNING_IDENTITY" "$APP"
else
  codesign --force --deep --sign - "$APP"
fi
codesign --verify --deep --strict "$APP"

ln -s /Applications "$STAGE/Applications"
hdiutil create -volname PdfViewerLite -srcfolder "$STAGE" -ov -format UDZO "$ROOT/artifacts/pdfviewerlite-$VERSION-$RID.dmg"
(cd "$STAGE" && zip -qry "$ROOT/artifacts/pdfviewerlite-$VERSION-$RID.app.zip" PdfViewerLite.app)
echo "Built $ROOT/artifacts/pdfviewerlite-$VERSION-$RID.dmg"
