#!/bin/bash

echo "Attempting to remove the last EF Core migration..."
echo ""

# Run the dotnet ef migrations remove command
dotnet ef migrations remove

if [ $? -eq 0 ]; then
  echo ""
  echo "Last migration removed successfully."
  echo "Remember to also revert any database changes if you ran 'dotnet ef database update'."
else
  echo ""
  echo "Error: Failed to remove the last migration."
  echo "This usually happens if the last migration has already been applied to the database."
  echo "Please check the output above for details."
fi