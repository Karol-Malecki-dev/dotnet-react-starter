#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 3 || "$3" != "--force" ]]; then
  echo "Usage: $0 <production-env-file> <snapshot-directory> --force" >&2
  exit 64
fi

environment_file="$1"
snapshot_directory="${2%/}"
script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
compose_file="${script_directory}/compose.production.yml"
write_path_stopped=false

if [[ ! -f "$environment_file" ]]; then
  echo "Production environment file was not found: $environment_file" >&2
  exit 66
fi

for required_file in database.dump data-protection-keys.tar.gz manifest.sha256; do
  if [[ ! -f "${snapshot_directory}/${required_file}" ]]; then
    echo "Backup is incomplete. Missing ${required_file}." >&2
    exit 66
  fi
done

compose() {
  docker compose --env-file "$environment_file" -f "$compose_file" "$@"
}

restart_write_path() {
  if [[ "$write_path_stopped" == true ]]; then
    compose up --detach --wait --wait-timeout 300 backend frontend caddy
  fi
}

trap restart_write_path EXIT

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
# Credentials are expanded inside the temporary MinIO client container.
# shellcheck disable=SC2016
compose run --rm --no-deps \
  -v "${snapshot_directory}:/backup:ro" \
  --entrypoint /bin/sh \
  minio-init -ec \
  'mc alias set restore-target http://minio:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null
   mc mirror --overwrite --remove /backup/attachments "restore-target/$MINIO_BUCKET"'

echo "Restoring Data Protection keys."
docker run --rm \
  -v dotnet-react-starter-production_data-protection-keys:/target \
  -v "${snapshot_directory}:/backup:ro" \
  alpine:3.22 \
  sh -ec 'find /target -mindepth 1 -maxdepth 1 -exec rm -rf -- {} +
          tar -xzf /backup/data-protection-keys.tar.gz -C /target'

restart_write_path
write_path_stopped=false
echo "Restore completed from ${snapshot_directory}."
