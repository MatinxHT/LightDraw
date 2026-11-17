#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
export DOTNET_ROOT="$PWD/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

mkdir -p "$DOTNET_ROOT"
curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$DOTNET_ROOT/dotnet-install.sh"
bash "$DOTNET_ROOT/dotnet-install.sh" --jsonfile global.json --install-dir "$DOTNET_ROOT" --no-path

"$DOTNET_ROOT/dotnet" workload install wasm-tools
"$DOTNET_ROOT/dotnet" publish src/LightDraw.Browser/LightDraw.Browser.csproj \
  --configuration Release --output "$PWD/artifacts/cloudflare-pages"
