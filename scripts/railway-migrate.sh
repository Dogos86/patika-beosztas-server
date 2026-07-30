#!/bin/sh
set -eu

if [ -z "${ConnectionStrings__DefaultConnection:-}" ]; then
  echo "Hiba: a ConnectionStrings__DefaultConnection nincs beállítva." >&2
  exit 1
fi

exec dotnet /app/PatikaBeosztas.Api.dll migrate
