#!/bin/sh
set -eu

for pid in $(lsof -nP -tiTCP:8765 -sTCP:LISTEN 2>/dev/null || true); do
  case "$(ps -p "$pid" -o command= 2>/dev/null || true)" in
    *WasmAppHost.dll*LightDraw.Browser*) kill "$pid" 2>/dev/null || true ;;
  esac
done

# Only stop Chrome launched with this checkout's dedicated debug profile.
# Do not touch normal Chrome windows or another project's debug session.
project_dir=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd -P)
profile_dir="$project_dir/.vscode/.browser-profile"
ps -axo pid=,command= | while read -r pid command; do
  case "$command" in
    *"/Google Chrome.app/Contents/MacOS/Google Chrome "*)
      case "$command " in
        *" --user-data-dir=$profile_dir "*) kill -TERM "$pid" 2>/dev/null || true ;;
      esac
      ;;
  esac
done
