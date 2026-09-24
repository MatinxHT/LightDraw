#!/bin/sh
set -eu

local_dotnet="$HOME/.local/share/lightdraw-dotnet/dotnet"
if [ -x "$local_dotnet" ]; then
  exec "$local_dotnet" "$@"
fi

exec dotnet "$@"
