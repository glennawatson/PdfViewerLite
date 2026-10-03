#!/usr/bin/env bash
# Starts the app on a private display, D-Bus session and accessibility bus, then checks a screen reader can see the
# window and a name for every control. Needs Xvfb, dbus, at-spi2-core and python3-gi.
# Usage: scripts/check-screen-reader.sh <path to pdfviewerlite> [minimum controls]
# PYTHON picks the interpreter with python3-gi (default /usr/bin/python3).
set -euo pipefail

if [[ -z "${DBUS_SESSION_BUS_ADDRESS:-}" || "${CHECK_SCREEN_READER_INNER:-}" != 1 ]]; then
  exec env CHECK_SCREEN_READER_INNER=1 dbus-run-session -- "$0" "$@"
fi

app=$(realpath "$1")
minimum=${2:-25}
python=${PYTHON:-/usr/bin/python3}
here=$(cd "$(dirname "$0")" && pwd)
work=$(mktemp -d)
pids=()
cleanup() {
  for pid in "${pids[@]}"; do kill "$pid" 2>/dev/null || true; done
  rm -rf "$work"
}
trap cleanup EXIT

if [[ -z "${DISPLAY:-}" ]]; then
  export DISPLAY=:97
  Xvfb "$DISPLAY" -screen 0 1400x900x24 &
  pids+=($!)
fi

# Fresh settings and session, so the check sees the default window.
export XDG_CONFIG_HOME="$work/config" XDG_DATA_HOME="$work/data" XDG_STATE_HOME="$work/state"

# A one page document, written with exact cross-reference offsets.
/usr/bin/python3 - "$work/check.pdf" <<'PY'
import sys
objects = [b"<< /Type /Catalog /Pages 2 0 R >>",
           b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
           b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
           None,
           b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"]
text = b"BT /F1 18 Tf 72 700 Td (Screen reader check) Tj ET"
objects[3] = b"<< /Length %d >>\nstream\n%s\nendstream" % (len(text), text)
out, offsets = bytearray(b"%PDF-1.4\n"), []
for number, body in enumerate(objects, 1):
    offsets.append(len(out))
    out += b"%d 0 obj\n%s\nendobj\n" % (number, body)
xref = len(out)
out += b"xref\n0 %d\n0000000000 65535 f \n" % (len(objects) + 1)
out += b"".join(b"%010d 00000 n \n" % offset for offset in offsets)
out += b"trailer\n<< /Size %d /Root 1 0 R >>\nstartxref\n%d\n%%%%EOF\n" % (len(objects) + 1, xref)
open(sys.argv[1], "wb").write(out)
PY

launcher=$(command -v at-spi-bus-launcher || echo /usr/libexec/at-spi-bus-launcher)
"$launcher" --launch-immediately &
pids+=($!)
for _ in $(seq 50); do
  gdbus call --session --dest org.a11y.Bus --object-path /org/a11y/bus --method org.a11y.Bus.GetAddress >/dev/null 2>&1 && break
  sleep 0.1
done
gdbus call --session --dest org.a11y.Bus --object-path /org/a11y/bus \
  --method org.freedesktop.DBus.Properties.Set org.a11y.Status IsEnabled "<true>" >/dev/null
address=$(gdbus call --session --dest org.a11y.Bus --object-path /org/a11y/bus --method org.a11y.Bus.GetAddress \
  | sed "s/^('\(.*\)',)$/\1/")

"$app" --new-instance "$work/check.pdf" >"$work/app.log" 2>&1 &
pids+=($!)

# Give the window time to appear, then let the walk decide.
for _ in $(seq 30); do
  sleep 1
  name=$(busctl --address="$address" list --no-pager 2>/dev/null | awk '$3 == "pdfviewerlite" { print $1; exit }')
  if [[ -n "$name" ]] && "$python" "$here/atspi-walk.py" "$address" "$name" "$minimum" >"$work/walk.txt" 2>&1; then
    cat "$work/walk.txt"
    exit 0
  fi
done

echo "A screen reader cannot see every control:" >&2
cat "$work/walk.txt" >&2 || true
cat "$work/app.log" >&2
exit 1
