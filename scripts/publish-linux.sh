#!/usr/bin/env bash
# Publishes the Native AOT build for a Linux runtime identifier into artifacts/<rid>.
# Usage: scripts/publish-linux.sh [linux-x64|linux-arm64] [extra dotnet publish arguments]
set -euo pipefail
"$(dirname "${BASH_SOURCE[0]}")/publish.sh" "${1:-linux-x64}" "${@:2}"
