#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -lt 1 || $# -gt 2 ]]; then
  echo "Usage: $0 <public-https-base-url> [--trigger-alert-test]" >&2
  exit 64
fi

public_base_url="${1%/}"
alert_test="${2:-}"

if [[ ! "$public_base_url" =~ ^https:// ]]; then
  echo "The public staging URL must start with https://." >&2
  exit 64
fi

if [[ -n "$alert_test" && "$alert_test" != "--trigger-alert-test" ]]; then
  echo "The optional second argument must be --trigger-alert-test." >&2
  exit 64
fi

require_command() {
  local command_name="$1"

  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Required command was not found: ${command_name}" >&2
    exit 69
  fi
}

require_command curl
require_command grep

retry_check() {
  local description="$1"
  shift

  local attempt
  for ((attempt = 1; attempt <= 8; attempt++)); do
    if "$@"; then
      return 0
    fi

    if [[ "$attempt" -lt 8 ]]; then
      sleep 5
    fi
  done

  echo "Timed out while checking ${description}." >&2
  return 1
}

check_public_endpoint() {
  local endpoint="$1"

  curl \
    --fail \
    --silent \
    --show-error \
    --max-time 15 \
    --proto '=https' \
    --tlsv1.2 \
    "${public_base_url}${endpoint}" \
    >/dev/null

  echo "Public endpoint is healthy: ${endpoint}"
}

check_local_endpoint() {
  local url="$1"
  local description="$2"

  curl \
    --fail \
    --silent \
    --show-error \
    --max-time 10 \
    "$url" \
    >/dev/null

  echo "${description} is ready."
}

check_prometheus_query() {
  local query="$1"
  local description="$2"
  local response

  response="$(
    curl \
      --fail \
      --silent \
      --show-error \
      --max-time 10 \
      --get \
      --data-urlencode "query=${query}" \
      http://127.0.0.1:9090/api/v1/query
  )"

  if ! grep -Eq '"status"[[:space:]]*:[[:space:]]*"success"' <<<"$response"; then
    echo "Prometheus query failed: ${description}" >&2
    return 1
  fi

  if ! grep -Eq '"value"[[:space:]]*:' <<<"$response"; then
    echo "Prometheus query returned no sample: ${description}" >&2
    return 1
  fi

  echo "Prometheus has live data for ${description}."
}

check_prometheus_rules() {
  local response
  local expected_rule
  local expected_rules=(
    ApplicationUnavailable
    ApplicationNotReady
    BackgroundWorkersUnhealthy
    ObjectStorageUnavailable
    MalwareScannerUnavailable
    EmailDeliveryFailed
    HostDiskSpaceLow
    HostMemoryLow
  )

  response="$(
    curl \
      --fail \
      --silent \
      --show-error \
      --max-time 10 \
      http://127.0.0.1:9090/api/v1/rules
  )"

  if ! grep -Eq '"status"[[:space:]]*:[[:space:]]*"success"' <<<"$response"; then
    echo "Prometheus rules API did not report success." >&2
    return 1
  fi

  if ! grep -Eq '"groups"[[:space:]]*:' <<<"$response"; then
    echo "Prometheus returned no alert rule groups." >&2
    return 1
  fi

  for expected_rule in "${expected_rules[@]}"; do
    if ! grep -Eq "\"name\"[[:space:]]*:[[:space:]]*\"${expected_rule}\"" <<<"$response"; then
      echo "Prometheus alert rule is not loaded: ${expected_rule}" >&2
      return 1
    fi
  done

  echo "Prometheus alert rules are loaded."
}

trigger_alert_test() {
  local started_at
  local ends_at
  local alert_payload

  started_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  ends_at="$(date -u -d '+2 minutes' +%Y-%m-%dT%H:%M:%SZ)"
  printf -v alert_payload \
    '{"labels":{"alertname":"V5StagingNotificationTest","severity":"info","v5_test":"true"},"annotations":{"summary":"V5 staging Alertmanager notification test","description":"Synthetic notification used to verify the configured operator route."},"startsAt":"%s","endsAt":"%s"}' \
    "$started_at" \
    "$ends_at"

  curl \
    --fail \
    --silent \
    --show-error \
    --max-time 10 \
    --request POST \
    --header 'Content-Type: application/json' \
    --data "[$alert_payload]" \
    http://127.0.0.1:9093/api/v2/alerts \
    >/dev/null

  echo "Synthetic alert accepted by Alertmanager."
  echo "The synthetic alert is scheduled to resolve in two minutes. Confirm delivery at the configured operator receiver and record the result in the V5 release evidence."
}

for endpoint in /health/live /health/ready /health/workers; do
  check_public_endpoint "$endpoint"
done

check_local_endpoint 'http://127.0.0.1:9090/-/ready' 'Prometheus'
check_local_endpoint 'http://127.0.0.1:9093/-/ready' 'Alertmanager'
check_local_endpoint 'http://127.0.0.1:3001/api/health' 'Grafana'
retry_check 'Prometheus alert rules' check_prometheus_rules
retry_check 'node exporter metrics' check_prometheus_query 'up{job="node"}' 'node exporter'
retry_check 'application health metrics' check_prometheus_query 'probe_success{job="application-health"}' 'application health probes'

if [[ "$alert_test" == "--trigger-alert-test" ]]; then
  trigger_alert_test
fi

echo "Staging runtime and observability verification completed."
