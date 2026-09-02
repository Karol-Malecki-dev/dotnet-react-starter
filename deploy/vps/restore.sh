#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

if [[ $# -lt 4 || $# -gt 6 || "$3" != "--force" ]]; then
  echo "Usage: $0 <production-env-file> <snapshot-or-encrypted-archive> --force <public-base-url> [encryption-key-file] [compose-profile]" >&2
  exit 64
fi

environment_file="$1"
backup_source="${2%/}"
public_base_url="${4%/}"
encryption_key_file="${5:-${BACKUP_ENCRYPTION_KEY_FILE:-}}"
compose_profile="${6:-${COMPOSE_PROFILE:-}}"
script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
compose_file="${script_directory}/compose.production.yml"
staging_compose_file="${script_directory}/compose.staging.yml"
snapshot_directory="$backup_source"
temporary_restore_root=""
write_path_stopped=false

if [[ ! -f "$environment_file" ]]; then
  echo "Production environment file was not found: $environment_file" >&2
  exit 66
fi

if [[ ! "$public_base_url" =~ ^https?:// ]]; then
  echo "Public base URL must start with http:// or https://." >&2
  exit 64
fi

cleanup_temporary_restore_root() {
  if [[ -n "$temporary_restore_root" && -d "$temporary_restore_root" ]]; then
    rm -rf -- "$temporary_restore_root"
  fi
}

trap cleanup_temporary_restore_root EXIT

if [[ -f "$backup_source" && "$backup_source" == *.tar.gz.gpg ]]; then
  if [[ -z "$encryption_key_file" || ! -s "$encryption_key_file" ]]; then
    echo "A non-empty backup encryption key file is required for encrypted archives." >&2
    exit 64
  fi

  if ! command -v gpg >/dev/null 2>&1; then
    echo "The gpg command is required to decrypt backups." >&2
    exit 69
  fi

  temporary_restore_root="$(mktemp -d)"
  archive_name="$(basename "$backup_source")"
  archive_name="${archive_name%.tar.gz.gpg}"
  gpg --batch --yes --pinentry-mode loopback \
    --passphrase-file "$encryption_key_file" \
    --decrypt "$backup_source" |
    tar -xzf - -C "$temporary_restore_root"
  snapshot_directory="${temporary_restore_root}/${archive_name}"
fi

if [[ ! -d "$snapshot_directory" ]]; then
  echo "Backup snapshot directory was not found: $snapshot_directory" >&2
  exit 66
fi

for required_file in database.dump data-protection-keys.tar.gz manifest.sha256; do
  if [[ ! -f "${snapshot_directory}/${required_file}" ]]; then
    echo "Backup is incomplete. Missing ${required_file}." >&2
    exit 66
  fi
done

if [[ ! -d "${snapshot_directory}/attachments" ]]; then
  echo "Backup is incomplete. Missing attachments directory." >&2
  exit 66
fi

native_host_path() {
  if [[ "${OSTYPE:-}" == msys* ]]; then
    cygpath --mixed "$1"
  else
    printf '%s\n' "$1"
  fi
}

docker_cli() {
  if [[ "${OSTYPE:-}" == msys* ]]; then
    MSYS2_ARG_CONV_EXCL='*' docker "$@"
  else
    docker "$@"
  fi
}

compose() {
  local compose_arguments=(
    --env-file "$(native_host_path "$environment_file")"
    -f "$(native_host_path "$compose_file")"
  )
  if [[ -n "$compose_profile" ]]; then
    compose_arguments+=(--profile "$compose_profile")
  fi
  if [[ "$compose_profile" == "staging" ]]; then
    compose_arguments+=(-f "$(native_host_path "$staging_compose_file")")
  fi

  docker_cli compose "${compose_arguments[@]}" "$@"
}

restart_write_path() {
  if [[ "$write_path_stopped" == true ]]; then
    compose up --detach --wait --wait-timeout 300 backend frontend caddy
  fi
}

cleanup() {
  local exit_code=$?
  set +e
  restart_write_path
  cleanup_temporary_restore_root
  return "$exit_code"
}

trap cleanup EXIT

(
  cd "$snapshot_directory"
  sha256sum --check manifest.sha256
)

echo "Stopping the public write path before destructive restore."
compose stop caddy frontend backend
write_path_stopped=true

echo "Restoring PostgreSQL."
# Variables are expanded by the shell inside the database container.
# shellcheck disable=SC2016
compose exec -T db sh -ec \
  'dropdb --username "$POSTGRES_USER" --if-exists --force "$POSTGRES_DB"
   createdb --username "$POSTGRES_USER" "$POSTGRES_DB"'
# Variables are expanded by the shell inside the database container.
# shellcheck disable=SC2016
compose exec -T db sh -ec \
  'pg_restore --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --no-owner --no-privileges' \
  < "${snapshot_directory}/database.dump"

echo "Restoring attachment objects."
snapshot_mount_source="$(native_host_path "$snapshot_directory")"
# Credentials are expanded inside the temporary MinIO client container.
# shellcheck disable=SC2016
compose run --rm --no-deps \
  -v "${snapshot_mount_source}:/backup:ro" \
  --entrypoint sh \
  minio-init -ec \
  'mc alias set restore-target http://minio:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null
   mc mirror --overwrite --remove /backup/attachments "restore-target/$MINIO_BUCKET"'

echo "Restoring Data Protection keys."
docker_cli run --rm \
  -v dotnet-react-starter-production_data-protection-keys:/target \
  -v "${snapshot_mount_source}:/backup:ro" \
  alpine:3.22 \
  sh -ec 'find /target -mindepth 1 -maxdepth 1 -exec rm -rf -- {} +
          tar -xzf /backup/data-protection-keys.tar.gz -C /target'

echo "Applying controlled migrations after restore."
compose run --rm migration

restart_write_path
write_path_stopped=false

wait_for_public_endpoint() {
  local endpoint="$1"
  local attempt
  for ((attempt = 1; attempt <= 30; attempt++)); do
    if curl --fail --silent --show-error --max-time 10 \
      "${public_base_url}${endpoint}" >/dev/null; then
      return 0
    fi

    sleep 5
  done

  return 1
}

for endpoint in /health/live /health/ready /health/workers; do
  if ! wait_for_public_endpoint "$endpoint"; then
    echo "Post-restore health check failed: ${public_base_url}${endpoint}" >&2
    compose logs --no-color --tail 200 backend caddy >&2 || true
    exit 1
  fi
done

echo "Restore completed and public health checks passed from ${backup_source}."
