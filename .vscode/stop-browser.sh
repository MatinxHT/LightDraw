#!/bin/sh
set -eu

for pid in $(lsof -nP -tiTCP:8765 -sTCP:LISTEN 2>/dev/null || true); do
  case "$(ps -p "$pid" -o command= 2>/dev/null || true)" in
    *WasmAppHost.dll*LightDraw.Browser*) kill "$pid" ;;
  esac
done
