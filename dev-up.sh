#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

load_env_file() {
  local env_file="$1"
  while IFS= read -r line || [[ -n $line ]]; do
    line="${line%$'\r'}"
    [[ -z "$line" ]] && continue
    [[ "$line" =~ ^[[:space:]]*# ]] && continue
    [[ "$line" != *"="* ]] && continue

    if [[ "$line" =~ ^[[:space:]]*([A-Za-z_][A-Za-z0-9_]*)[[:space:]]*=(.*)$ ]]; then
      local key="${BASH_REMATCH[1]}"
      local value="${BASH_REMATCH[2]}"

      value="${value#${value%%[![:space:]]*}}"
      value="${value%${value##*[![:space:]]}}"

      if [[ ${#value} -ge 2 ]]; then
        if [[ ${value:0:1} == '"' && ${value: -1} == '"' ]]; then
          value="${value:1:-1}"
        elif [[ ${value:0:1} == "'" && ${value: -1} == "'" ]]; then
          value="${value:1:-1}"
        fi
      fi

      export "$key=$value"
    fi
  done < "$env_file"
}

if [[ -f "$ROOT_DIR/.env" ]]; then
  echo "Loading environment variables from .env"
  load_env_file "$ROOT_DIR/.env"
fi

for tool in dotnet npm; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "Required tool '$tool' is not available in PATH" >&2
    exit 1
  fi
done

ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://localhost:8080}"
BACKEND_BASE_URL="${BACKEND_BASE_URL:-http://localhost:8080}"
FRONTEND_BASE_URL="${FRONTEND_BASE_URL:-http://localhost:5173}"
DATABASE_PATH="${DATABASE_PATH:-./data/app.db}"
LOG_PATH="${LOG_PATH:-./logs/log-.ndjson}"
VITE_BACKEND_BASE_URL="${VITE_BACKEND_BASE_URL:-$BACKEND_BASE_URL}"
FRONTEND_HOST="${FRONTEND_HOST:-localhost}"
FRONTEND_PORT="${FRONTEND_PORT:-5173}"

backend_pid=""
frontend_pid=""

cleanup() {
  trap - INT TERM EXIT
  if [[ -n "$frontend_pid" ]] && kill -0 "$frontend_pid" 2>/dev/null; then
    echo "Stopping frontend (PID $frontend_pid)"
    kill "$frontend_pid" 2>/dev/null || true
    wait "$frontend_pid" 2>/dev/null || true
  fi
  if [[ -n "$backend_pid" ]] && kill -0 "$backend_pid" 2>/dev/null; then
    echo "Stopping backend (PID $backend_pid)"
    kill "$backend_pid" 2>/dev/null || true
    wait "$backend_pid" 2>/dev/null || true
  fi
}
trap cleanup INT TERM EXIT

pushd "$ROOT_DIR/backend/WebApi" >/dev/null
ASPNETCORE_ENVIRONMENT="$ASPNETCORE_ENVIRONMENT" \
ASPNETCORE_URLS="$ASPNETCORE_URLS" \
BACKEND_BASE_URL="$BACKEND_BASE_URL" \
FRONTEND_BASE_URL="$FRONTEND_BASE_URL" \
DATABASE_PATH="$DATABASE_PATH" \
LOG_PATH="$LOG_PATH" \
dotnet run --no-launch-profile &
backend_pid=$!
popd >/dev/null

sleep 2
if ! kill -0 "$backend_pid" 2>/dev/null; then
  echo "Backend failed to start" >&2
  wait "$backend_pid"
  exit $?
fi

echo "Backend is running at ${ASPNETCORE_URLS}"

pushd "$ROOT_DIR/frontend" >/dev/null
if [[ ! -d node_modules ]]; then
  echo "Installing frontend dependencies"
  npm install
fi

VITE_BACKEND_BASE_URL="$VITE_BACKEND_BASE_URL" \
HOST="$FRONTEND_HOST" \
PORT="$FRONTEND_PORT" \
npm run dev -- --host "$FRONTEND_HOST" --port "$FRONTEND_PORT" &
frontend_pid=$!
popd >/dev/null

sleep 2
if ! kill -0 "$frontend_pid" 2>/dev/null; then
  echo "Frontend failed to start" >&2
  wait "$frontend_pid"
  exit $?
fi

echo "Frontend is running at http://${FRONTEND_HOST}:${FRONTEND_PORT}"

echo "Press Ctrl+C to stop both services"

wait -n "$backend_pid" "$frontend_pid"
status=$?
if (( status != 0 )); then
  echo "One of the services exited with status $status" >&2
fi
exit "$status"
