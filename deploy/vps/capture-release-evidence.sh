#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

if [[ $# -lt 3 || $# -gt 5 ]]; then
  echo "Usage: $0 <production-env-file> <public-https-base-url> <evidence-directory> [compose-profile] [--require-rollback-target]" >&2
  exit 64
fi

environment_file="$1"
public_base_url="${2%/}"
evidence_directory="${3%/}"
compose_profile="${4:-}"
rollback_requirement="${5:-}"
require_rollback_target=false
script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
compose_file="${script_directory}/compose.production.yml"
staging_compose_file="${script_directory}/compose.staging.yml"
state_file="${script_directory}/.deployed-image-tag"
previous_state_file="${script_directory}/.previous-image-tag"

if [[ ! -f "$environment_file" ]]; then
  echo "Production environment file was not found: ${environment_file}" >&2
  exit 66
fi

if [[ ! "$public_base_url" =~ ^https:// ]]; then
  echo "The public release-validation URL must start with https://." >&2
  exit 64
fi

if [[ -z "$evidence_directory" ]]; then
  echo "Evidence directory must not be empty." >&2
  exit 64
fi

if [[ -n "$rollback_requirement" && "$rollback_requirement" != "--require-rollback-target" ]]; then
  echo "The optional fifth argument must be --require-rollback-target." >&2
  exit 64
fi

if [[ "$rollback_requirement" == "--require-rollback-target" ]]; then
  require_rollback_target=true
fi

if [[ -e "$evidence_directory" && ! -d "$evidence_directory" ]]; then
  echo "Evidence path exists but is not a directory: ${evidence_directory}" >&2
  exit 66
fi

require_command() {
  local command_name="$1"

  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Required command was not found: ${command_name}" >&2
    exit 69
  fi
}

require_command docker
require_command curl
require_command sha256sum
require_command tr

mkdir -p "$evidence_directory"
evidence_directory="$(cd -- "$evidence_directory" && pwd)"
chmod 700 "$evidence_directory"

compose() {
  local compose_arguments=(--env-file "$environment_file" -f "$compose_file")

  if [[ -n "$compose_profile" ]]; then
    compose_arguments+=(--profile "$compose_profile")
  fi

  if [[ "$compose_profile" == "staging" ]]; then
    compose_arguments+=(-f "$staging_compose_file")
  fi

  docker compose "${compose_arguments[@]}" "$@"
}

read_validated_tag() {
  local tag_file="$1"
  local description="$2"
  local tag

  if [[ ! -s "$tag_file" ]]; then
    echo "Required ${description} tag file was not found: ${tag_file}" >&2
    exit 66
  fi

  tag="$(tr -d '[:space:]' < "$tag_file")"
  if [[ ! "$tag" =~ ^([0-9a-f]{40}|v[0-9]+\.[0-9]+\.[0-9]+([.-][0-9A-Za-z.-]+)?)$ ]]; then
    echo "The ${description} tag is not an immutable SHA or release tag: ${tag}" >&2
    exit 74
  fi

  printf '%s\n' "$tag"
}

current_tag="$(read_validated_tag "$state_file" 'deployed')"
previous_tag=""
rollback_readiness="pending"

if [[ -s "$previous_state_file" ]]; then
  previous_tag="$(read_validated_tag "$previous_state_file" 'previous')"

  if [[ "$current_tag" == "$previous_tag" ]]; then
    echo "The deployed and previous tags are identical; a meaningful rollback target is missing." >&2
    exit 74
  fi

  rollback_readiness="ready"
elif [[ "$require_rollback_target" == true ]]; then
  echo "A previous immutable image tag is required for this release validation." >&2
  exit 74
fi

export IMAGE_TAG="$current_tag"
compose config --quiet

migration_container="$(compose ps -aq migration | tr -d '[:space:]')"
if [[ -z "$migration_container" || ! "$migration_container" =~ ^[a-f0-9]+$ ]]; then
  echo "The migration container could not be identified." >&2
  exit 74
fi

migration_state="$(docker inspect --format '{{.State.Status}} {{.State.ExitCode}}' "$migration_container")"
if [[ "$migration_state" != "exited 0" ]]; then
  echo "The migration container did not complete successfully: ${migration_state}" >&2
  exit 74
fi

temporary_evidence_file="$(mktemp "${evidence_directory}/.automated-checks.XXXXXX")"
temporary_metadata_file="$(mktemp "${evidence_directory}/.metadata.XXXXXX")"
temporary_manifest_file="$(mktemp "${evidence_directory}/.manifest.XXXXXX")"

cleanup() {
  rm -f -- "$temporary_evidence_file" "$temporary_metadata_file" "$temporary_manifest_file"
}

trap cleanup EXIT

{
  echo "V5 automated release evidence"
  echo "created_at_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "public_base_url=${public_base_url}"
  echo "deployed_tag=${current_tag}"
  echo "previous_tag=${previous_tag:-none}"
  echo "rollback_readiness=${rollback_readiness}"
  echo "migration_container=${migration_container}"
  echo "migration_state=${migration_state}"
  echo
  echo "== staging and observability verification =="
  "${script_directory}/verify-staging.sh" "$public_base_url"
  echo
  echo "== compose services =="
  compose ps --all
  echo
  echo "== compose images =="
  compose images
} >"$temporary_evidence_file" 2>&1

{
  echo "created_at_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "public_base_url=${public_base_url}"
  echo "deployed_tag=${current_tag}"
  echo "previous_tag=${previous_tag:-none}"
  echo "rollback_readiness=${rollback_readiness}"
  echo "migration_state=${migration_state}"
  echo "compose_profile=${compose_profile:-none}"
} >"$temporary_metadata_file"

mv -- "$temporary_evidence_file" "${evidence_directory}/automated-checks.txt"
mv -- "$temporary_metadata_file" "${evidence_directory}/metadata.txt"
if [[ "$rollback_readiness" == "ready" ]]; then
  printf '%s\n' "AUTOMATED_CHECKS_PASSED" >"${evidence_directory}/status"
else
  printf '%s\n' "AUTOMATED_CHECKS_PASSED_ROLLBACK_PENDING" >"${evidence_directory}/status"
fi

(
  cd "$evidence_directory"
  sha256sum automated-checks.txt metadata.txt status >"$temporary_manifest_file"
)
mv -- "$temporary_manifest_file" "${evidence_directory}/manifest.sha256"

(
  cd "$evidence_directory"
  sha256sum --check manifest.sha256
)

echo "Automated V5 release evidence captured at ${evidence_directory}."
echo "Manual backup, restore, alert-delivery, and rollback evidence is still required."
