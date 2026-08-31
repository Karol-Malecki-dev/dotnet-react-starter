#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -lt 2 || $# -gt 3 ]]; then
  echo "Usage: $0 <production-env-file> <backup-root> [retention-days]" >&2
  exit 64
fi

environment_file="$1"
backup_root="$2"
retention_days="${3:-14}"
script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
compose_file="${script_directory}/compose.production.yml"
timestamp="$(date -u +%Y-%m-%dT%H%M%SZ)"
snapshot_directory="${backup_root%/}/${timestamp}"
write_path_stopped=false

if [[ ! -f "$environment_file" ]]; then
  echo "Production environment file was not found: $environment_file" >&2
  exit 66
fi

if [[ ! "$retention_days" =~ ^[1-9][0-9]*$ ]]; then
  echo "Retention days must be a positive integer." >&2
  exit 64
fi

compose() {
  docker compose --env-file "$environment_file" -f "$compose_file" "$@"
}

restart_write_path() {
  if [[ "$write_path_stopped" == true ]]; then
    compose up --detach --wait --wait-timeout 300 backend frontend caddy
  fi
}

trap restart_write_path EXIT

install -m 700 -d "$backup_root"
install -m 700 -d "$snapshot_directory"
compose config --quiet

echo "Stopping the public write path for a consistent snapshot."
compose stop caddy frontend backend
write_path_stopped=true

echo "Exporting PostgreSQL."
# Variables are expanded by the shell inside the database container.
# shellcheck disable=SC2016
compose exec -T db sh -ec \
  'pg_dump --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --format=custom --no-owner --no-privileges' \
  > "${snapshot_directory}/database.dump"

echo "Exporting attachment objects."
install -m 700 -d "${snapshot_directory}/attachments"
# Credentials are expanded inside the temporary MinIO client container.
# shellcheck disable=SC2016
compose run --rm --no-deps \
  -v "${snapshot_directory}:/backup" \
  --entrypoint /bin/sh \
  minio-init -ec \
  'mc alias set backup-source http://minio:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null
   mc mirror --overwrite "backup-source/$MINIO_BUCKET" /backup/attachments'

echo "Exporting Data Protection keys."
docker run --rm \
  -v dotnet-react-starter-production_data-protection-keys:/source:ro \
  -v "${snapshot_directory}:/backup" \
  alpine:3.22 \
  tar -czf /backup/data-protection-keys.tar.gz -C /source .

cat > "${snapshot_directory}/metadata.txt" <<EOF
created_at_utc=${timestamp}
image_tag=$(cat "${script_directory}/.deployed-image-tag" 2>/dev/null || printf 'unknown')
database=postgresql
attachment_storage=minio
EOF

temporary_manifest="$(mktemp)"
(
  cd "$snapshot_directory"
  find . -type f ! -name manifest.sha256 -print0 |
    sort -z |
    xargs -0 sha256sum
) > "$temporary_manifest"
mv "$temporary_manifest" "${snapshot_directory}/manifest.sha256"

restart_write_path
write_path_stopped=false

find "$backup_root" \
  -mindepth 1 \
  -maxdepth 1 \
  -type d \
  -name '20??-??-??T??????Z' \
  -mtime "+${retention_days}" \
  -print0 |
  xargs -0r rm -rf --

echo "Backup completed: ${snapshot_directory}"
