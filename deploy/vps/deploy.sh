#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: $0 <image-tag> <production-env-file> <public-base-url>" >&2
  exit 64
fi

image_tag="$1"
environment_file="$2"
public_base_url="${3%/}"
script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
compose_file="${script_directory}/compose.production.yml"
state_file="${script_directory}/.deployed-image-tag"
previous_state_file="${script_directory}/.previous-image-tag"
lock_file="${script_directory}/.deployment.lock"

if [[ ! "$image_tag" =~ ^([0-9a-f]{40}|v[0-9]+\.[0-9]+\.[0-9]+([.-][0-9A-Za-z.-]+)?)$ ]]; then
  echo "Image tag must be a full commit SHA or a vMAJOR.MINOR.PATCH release tag." >&2
  exit 64
fi

if [[ ! -f "$environment_file" ]]; then
  echo "Production environment file was not found: $environment_file" >&2
  exit 66
fi

exec 9>"$lock_file"
if ! flock -n 9; then
  echo "Another deployment is already running." >&2
  exit 75
fi

compose() {
  docker compose --env-file "$environment_file" -f "$compose_file" "$@"
}

wait_for_readiness() {
  local attempt
  for ((attempt = 1; attempt <= 30; attempt++)); do
    if curl --fail --silent --show-error --max-time 10 \
      "${public_base_url}/health/ready" >/dev/null; then
      return 0
    fi

    sleep 5
  done

  return 1
}

rollback() {
  local previous_tag="$1"

  if [[ -z "$previous_tag" ]]; then
    echo "No previous image tag is available. Stopping the failed deployment." >&2
    compose down --remove-orphans
    return 1
  fi

  echo "Rolling back to image tag ${previous_tag}." >&2
  export IMAGE_TAG="$previous_tag"
  compose pull backend frontend migration
  compose up --detach --wait --wait-timeout 300
  wait_for_readiness
}

previous_tag=""
if [[ -f "$state_file" ]]; then
  previous_tag="$(tr -d '[:space:]' < "$state_file")"
fi

export IMAGE_TAG="$image_tag"
compose config --quiet
compose pull

if ! compose up --detach --wait --wait-timeout 300; then
  rollback "$previous_tag"
  exit 1
fi

if ! wait_for_readiness; then
  echo "Public readiness check failed after deployment." >&2
  compose logs --no-color --tail 200 backend caddy >&2 || true
  rollback "$previous_tag"
  exit 1
fi

if [[ -n "$previous_tag" && "$previous_tag" != "$image_tag" ]]; then
  printf '%s\n' "$previous_tag" > "$previous_state_file"
fi

printf '%s\n' "$image_tag" > "$state_file"
docker image prune --force --filter "until=168h" >/dev/null
echo "Deployment ${image_tag} is ready at ${public_base_url}."
