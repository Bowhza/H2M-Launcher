#!/bin/bash

PARENT_DIR="$(cd "$(dirname "$PWD")" && pwd)"

# Load environment variables from .env
if [ -f "$PARENT_DIR/.env" ]; then
  # Remove windows line endings
  export $(tr -d '\r' < "$PARENT_DIR/.env"  | grep -v '^#' | xargs)
else
  echo ".env file not found!"
  exit 1
fi

# Check required environment variables
if [[ -z "$DB_USER" || -z "$DB_PASSWORD" ]]; then
  echo "DB_USER and/or DB_PASSWORD not set in .env"
  exit 1
fi

# Use provided backup directory or default to current directory
DEFAULT_BACKUP_DIR="$PARENT_DIR/pgbackup"
BACKUP_DIR="${1:-$DEFAULT_BACKUP_DIR}"

# Use provided docker network name or default compose name
DOCKER_NETWORK="${2:-"h2m-launcher_default"}"

# Create the backup directory if it doesn't exist
mkdir -p "$BACKUP_DIR"

echo "Loaded DB_USER: $DB_USER"
echo "Loaded DB_PASSWORD: '${#DB_PASSWORD}' characters"

# Run the Docker backup container
docker run --rm \
  -v "${BACKUP_DIR}:/backups" \
  -e POSTGRES_HOST=postgres \
  -e POSTGRES_DB=database \
  -e POSTGRES_USER=${DB_USER} \
  -e POSTGRES_PASSWORD=${DB_PASSWORD} \
  --network ${DOCKER_NETWORK} \
  prodrigestivill/postgres-backup-local /backup.sh