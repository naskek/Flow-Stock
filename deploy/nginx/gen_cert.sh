#!/bin/sh
set -eu

TARGET="${1:?Usage: ./gen_cert.sh <server-ip-or-hostname>}"
SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
OUT_DIR="${SCRIPT_DIR}/certs"

mkdir -p "$OUT_DIR"

SAN_TYPE="IP"
case "$TARGET" in
  *[!0-9.]*)
    SAN_TYPE="DNS"
    ;;
esac

openssl req -x509 -newkey rsa:2048 -nodes -sha256 -days 825 \
  -keyout "${OUT_DIR}/flowstock.key" \
  -out "${OUT_DIR}/flowstock.crt" \
  -subj "/CN=${TARGET}" \
  -addext "subjectAltName=${SAN_TYPE}:${TARGET}"

echo "Created ${OUT_DIR}/flowstock.crt and ${OUT_DIR}/flowstock.key for ${TARGET}"
