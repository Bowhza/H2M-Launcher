#!/bin/bash

# Check if a migration name was provided
if [ -z "$1" ]; then
  echo "Usage: $0 <migration_name>"
  echo "Example: $0 AddInitialSchema"
  exit 1
fi

MIGRATION_NAME=$1
MIGRATIONS_OUTPUT_DIR="Database/Migrations"

echo "Adding EF Core migration: $MIGRATION_NAME"
echo "Output directory: $MIGRATIONS_OUTPUT_DIR"
echo ""

# Run the dotnet ef migrations add command
# -o: Specifies the output directory for the migration files, relative to the migration project
dotnet ef migrations add "$MIGRATION_NAME" --output-dir "$MIGRATIONS_OUTPUT_DIR"

if [ $? -eq 0 ]; then
  echo ""
  echo "Migration '$MIGRATION_NAME' added successfully to '$MIGRATIONS_OUTPUT_DIR'."
else
  echo ""
  echo "Error: Failed to add migration '$MIGRATION_NAME'."
  echo "Please check the output above for details."
fi