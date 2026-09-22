#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

if [[ $# -ne 3 ]]; then
  echo "Usage: $0 <encrypted-backup-file> <remote-host> <remote-directory>" >&2
  exit 64
fi

backup_file="$1"
remote_host="$2"
remote_directory="${3%/}"

if [[ ! -f "$backup_file" || ! -s "$backup_file" ]]; then
  echo "Encrypted backup file was not found or is empty: ${backup_file}" >&2
  exit 66
fi

if [[ "$backup_file" != *.tar.gz.gpg ]]; then
  echo "Only GPG-encrypted .tar.gz.gpg backups may be copied off host." >&2
  exit 64
fi

if [[ ! "$remote_host" =~ ^[A-Za-z0-9._@:-]+$ ]]; then
  echo "Remote host must be a hostname, IP address, SSH alias, or user@host without shell characters." >&2
  exit 64
fi

if [[ "$remote_host" == -* ]]; then
  echo "Remote host must not start with a command-line option." >&2
  exit 64
fi

if [[ "$remote_directory" != /* ]]; then
  echo "Remote directory must be an absolute path." >&2
  exit 64
fi

if ! command -v sha256sum >/dev/null 2>&1; then
  echo "The sha256sum command is required." >&2
  exit 69
fi

if ! command -v ssh >/dev/null 2>&1; then
  echo "The ssh command is required." >&2
  exit 69
fi

if ! command -v scp >/dev/null 2>&1; then
  echo "The scp command is required." >&2
  exit 69
fi

if ! command -v awk >/dev/null 2>&1; then
  echo "The awk command is required." >&2
  exit 69
fi

backup_name="$(basename -- "$backup_file")"
if [[ ! "$backup_name" =~ ^[A-Za-z0-9][A-Za-z0-9._-]*\.tar\.gz\.gpg$ ]]; then
  echo "Backup filename contains unsupported characters: ${backup_name}" >&2
  exit 64
fi

remote_file="${remote_directory}/${backup_name}"
local_checksum="$(sha256sum -- "$backup_file" | awk '{print $1}')"

printf -v quoted_remote_directory '%q' "$remote_directory"
printf -v quoted_remote_file '%q' "$remote_file"
ssh_options=(-o BatchMode=yes -o StrictHostKeyChecking=yes)

ssh "${ssh_options[@]}" "$remote_host" "mkdir -p -- ${quoted_remote_directory}"
scp "${ssh_options[@]}" -- "$backup_file" "${remote_host}:${remote_file}"

remote_checksum="$(
  ssh "${ssh_options[@]}" "$remote_host" \
    "sha256sum -- ${quoted_remote_file}" |
    awk 'NR == 1 { print $1 }'
)"

if [[ -z "$remote_checksum" ]]; then
  echo "Remote checksum could not be read for ${remote_file}." >&2
  exit 74
fi

if [[ "$local_checksum" != "$remote_checksum" ]]; then
  echo "Checksum mismatch after off-host transfer." >&2
  echo "Local:  ${local_checksum}" >&2
  echo "Remote: ${remote_checksum}" >&2
  exit 74
fi

echo "Encrypted backup copied and verified:"
echo "  file:     ${remote_host}:${remote_file}"
echo "  sha256:   ${local_checksum}"
