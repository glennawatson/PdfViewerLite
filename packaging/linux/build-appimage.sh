#!/usr/bin/env bash
# Builds an AppImage from a published build.
# Usage: packaging/linux/build-appimage.sh <published dir> <arch: x86_64|aarch64> <version>
# Requires appimagetool on PATH (https://github.com/AppImage/appimagetool).
set -euo pipefail

FROM="$1"
ARCH="$2"
VERSION="$3"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
APP_ID="net.glennwatson.PdfViewerLite"
APPDIR="$ROOT/artifacts/AppDir-$ARCH"

rm -rf "$APPDIR"
install -d "$APPDIR/usr/bin" "$APPDIR/usr/share/applications" "$APPDIR/usr/share/metainfo" "$APPDIR/usr/share/icons/hicolor/scalable/apps"
install -m 755 "$FROM/pdfviewerlite" "$APPDIR/usr/bin/"
for lib in "$FROM"/*.so; do install -m 644 "$lib" "$APPDIR/usr/bin/"; done
install -m 644 "$ROOT/packaging/linux/$APP_ID.desktop" "$APPDIR/usr/share/applications/"
install -m 644 "$ROOT/packaging/linux/$APP_ID.desktop" "$APPDIR/"
install -m 644 "$ROOT/packaging/linux/$APP_ID.metainfo.xml" "$APPDIR/usr/share/metainfo/$APP_ID.appdata.xml"
install -m 644 "$ROOT/packaging/linux/icons/$APP_ID.svg" "$APPDIR/usr/share/icons/hicolor/scalable/apps/"
install -m 644 "$ROOT/packaging/linux/icons/$APP_ID.svg" "$APPDIR/"
ln -sf "$APP_ID.svg" "$APPDIR/.DirIcon"
cat > "$APPDIR/AppRun" <<'APPRUN'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/pdfviewerlite" "$@"
APPRUN
chmod 755 "$APPDIR/AppRun"

ARCH="$ARCH" appimagetool --no-appstream "$APPDIR" "$ROOT/artifacts/PdfViewerLite-$VERSION-$ARCH.AppImage"
