#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

if [[ $# -lt 3 || $# -gt 4 ]]; then
  echo "Usage: $0 <production-env-file> <backup-root> <retention-days> [encryption-key-file]" >&2
  exit 64
fi

environment_file="$1"
backup_root="$2"
retention_days="$3"
encryption_key_file="${4:-${BACKUP_ENCRYPTION_KEY_FILE:-}}"
script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
compose_file="${script_directory}/compose.production.yml"
timestamp="$(date -u +%Y-%m-%dT%H%M%SZ)"
snapshot_directory="${backup_root%/}/${timestamp}"
encrypted_backup_path="${backup_root%/}/${timestamp}.tar.gz.gpg"
write_path_stopped=false
backup_complete=false

if [[ ! -f "$environment_file" ]]; then
  echo "Production environment file was not found: $environment_file" >&2
  exit 66
fi

if [[ -z "$encryption_key_file" || ! -s "$encryption_key_file" ]]; then
  echo "A non-empty backup encryption key file is required." >&2
  exit 64
fi

if ! command -v gpg >/dev/null 2>&1; then
  echo "The gpg command is required to encrypt backups." >&2
  exit 69
fi

if [[ ! "$retention_days" =~ ^[1-9][0-9]*$ ]]; then
  echo "Retention days must be a positive integer." >&2
  exit 64
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
  docker_cli compose \
    --env-file "$(native_host_path "$environment_file")" \
    -f "$(native_host_path "$compose_file")" \
    "$@"
}

create_private_directory() {
  local directory_path="$1"
  mkdir -p "$directory_path"

  if [[ "${OSTYPE:-}" != msys* ]]; then
    chmod 700 "$directory_path"
  fi
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

  if [[ "$backup_complete" != true && -d "$snapshot_directory" ]]; then
    rm -rf -- "$snapshot_directory"
  fi

  if [[ "$backup_complete" != true && -f "$encrypted_backup_path" ]]; then
    rm -f -- "$encrypted_backup_path"
  fi

  return "$exit_code"
}

trap cleanup EXIT

create_private_directory "$backup_root"
create_private_directory "$snapshot_directory"
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
create_private_directory "${snapshot_directory}/attachments"
snapshot_mount_source="$(native_host_path "$snapshot_directory")"
# Credentials are expanded inside the temporary MinIO client container.
# shellcheck disable=SC2016
compose run --rm --no-deps \
  -v "${snapshot_mount_source}:/backup" \
  --entrypoint sh \
  minio-init -ec \
  'mc alias set backup-source http://minio:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null
   mc mirror --overwrite "backup-source/$MINIO_BUCKET" /backup/attachments'

echo "Exporting Data Protection keys."
docker_cli run --rm \
  -v dotnet-react-starter-production_data-protection-keys:/source:ro \
  -v "${snapshot_mount_source}:/backup" \
  --workdir /backup \
  alpine:3.22 \
  tar -czf data-protection-keys.tar.gz -C /source .

cat > "${snapshot_directory}/metadata.txt" <<EOF
created_at_utc=${timestamp}
image_tag=$(cat "${script_directory}/.deployed-image-tag" 2>/dev/null || printf 'unknown')
database=postgresql
attachment_storage=minio
encryption=gpg-aes256
EOF

if [[ ! -s "${snapshot_directory}/database.dump" ]]; then
  echo "PostgreSQL backup is empty." >&2
  exit 74
fi

if [[ ! -d "${snapshot_directory}/attachments" ]]; then
  echo "Attachment backup directory is missing." >&2
  exit 74
fi

if [[ ! -s "${snapshot_directory}/data-protection-keys.tar.gz" ]]; then
  echo "Data Protection key backup is empty or missing." >&2
  exit 74
fi

temporary_manifest="$(mktemp)"
(
  cd "$snapshot_directory"
  find . -type f ! -name manifest.sha256 -print0 |
    sort -z |
    xargs -0 sha256sum
) > "$temporary_manifest"
mv "$temporary_manifest" "${snapshot_directory}/manifest.sha256"

(
  cd "$snapshot_directory"
  sha256sum --check manifest.sha256
)

echo "Encrypting backup archive."
tar -czf - -C "$backup_root" "$(basename "$snapshot_directory")" |
  gpg --batch --yes --pinentry-mode loopback \
    --passphrase-file "$encryption_key_file" \
    --cipher-algo AES256 \
    --output "$encrypted_backup_path" \
    --symmetric

if [[ ! -s "$encrypted_backup_path" ]]; then
  echo "Encrypted backup archive is empty or missing." >&2
  exit 74
fi

rm -rf -- "$snapshot_directory"
backup_complete=true

restart_write_path
write_path_stopped=false

find "$backup_root" \
  -mindepth 1 \
  -maxdepth 1 \
  -type f \
  -name '20??-??-??T??????Z.tar.gz.gpg' \
  -mtime "+${retention_days}" \
  -print0 |
  xargs -0r rm -f --

echo "Backup completed: ${encrypted_backup_path}"
