#!/usr/bin/env bash
# Smoke test for the running compose stack (`docker compose up --build --detach --wait` first).
# Checks that the gateway routes /api/ to the API and doesn't expose anything else.
set -euo pipefail

base="${GATEWAY_URL:-http://localhost:8080}"
failures=0

check() {
  local description="$1" expected="$2" path="$3"
  local status
  status=$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 10 "$base$path" || echo "000")
  if [[ "$status" == "$expected" ]]; then
    echo "✔ $description ($path → $status)"
  else
    echo "✖ $description ($path → $status, expected $expected)"
    failures=$((failures + 1))
  fi
}

check "gateway is alive" 200 /healthz
# 404 comes from the API itself; an unreachable upstream would return 502/504.
check "/api/ is routed to the API" 404 /api/v1/smoke-test
check "health checks are not exposed through the gateway" 404 /health
check "health checks are not exposed under /api/" 404 /api/health

body=$(curl --silent --max-time 10 "$base/api/v1/smoke-test" || true)
if [[ "$body" == *"Frontend not configured"* ]]; then
  echo "✖ /api/ response came from the gateway's fallback, not the API"
  failures=$((failures + 1))
fi

for service in api gateway; do
  container=$(docker compose ps --all --quiet "$service")
  health=$( [[ -n "$container" ]] && docker inspect --format '{{.State.Status}}/{{.State.Health.Status}}' "$container" || echo "missing")
  if [[ "$health" == "running/healthy" ]]; then
    echo "✔ $service container is healthy"
  else
    echo "✖ $service container is $health"
    failures=$((failures + 1))
  fi
done

if ((failures > 0)); then
  echo "$failures smoke check(s) failed."
  exit 1
fi
