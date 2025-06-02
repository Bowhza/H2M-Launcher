#!/bin/sh

CERT_DIR="."
DOMAIN=${DOMAIN:-localhost}
CERT_PATH="$CERT_DIR/selfsigned.crt"
KEY_PATH="$CERT_DIR/selfsigned.key"

mkdir -p "$CERT_DIR"

if [ ! -f "$CERT_PATH" ] || [ ! -f "$KEY_PATH" ]; then
  echo "Generating self-signed certificate for $DOMAIN..."
  openssl req -x509 -nodes -days 365 \
    -subj "/CN='$DOMAIN'" \
    -newkey rsa:2048 \
    -keyout "$KEY_PATH" \
    -out "$CERT_PATH"
else
  echo "Self-signed certificate already exists. Skipping generation."
fi