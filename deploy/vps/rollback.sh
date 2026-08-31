#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: $0 <production-env-file> <public-base-url>" >&2
  exit 64
fi

environment_file="$1"
public_base_url="${2%/}"
script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
previous_state_file="${script_directory}/.previous-image-tag"

if [[ ! -f "$previous_state_file" ]]; then
  echo "No previous deployment tag is recorded." >&2
  exit 66
fi

previous_tag="$(tr -d '[:space:]' < "$previous_state_file")"
exec "${script_directory}/deploy.sh" "$previous_tag" "$environment_file" "$public_base_url"
