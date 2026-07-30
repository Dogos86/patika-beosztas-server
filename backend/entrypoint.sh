#!/bin/sh
set -eu

port="${PORT:-8080}"
unset ASPNETCORE_HTTP_PORTS ASPNETCORE_HTTPS_PORTS
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:${port}}"

mkdir -p /app/keys
chown -R patika:patika /app/keys

exec gosu patika "$@"
