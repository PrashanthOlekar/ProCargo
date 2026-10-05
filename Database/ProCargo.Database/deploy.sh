#!/usr/bin/env bash
# ---------------------------------------------------------------------------------------------
# ProCargo database deployment (Linux/macOS/CI). Runs every script in a predictable order:
#   01-Database (against master) -> 02 .. 09 -> 11-Security -> [10-TestData when --with-test-data]
#
# Usage:
#   SQL_SERVER=localhost,1433 SQL_USER=sa SQL_PASSWORD='...' ./deploy.sh [--with-test-data]
#   SQL_SERVER=myserver.database.windows.net SQL_AUTH=ActiveDirectoryDefault ./deploy.sh   (Azure SQL)
#
# Notes
#   * sqlcmd runs with -I (QUOTED_IDENTIFIER ON): required for filtered indexes and the procedures
#     that write to tables that have them.
#   * -b stops on the first error so a half-applied deployment is visible immediately.
#   * Every script is idempotent (IF NOT EXISTS / CREATE OR ALTER / MERGE), so re-running is safe.
# ---------------------------------------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SQLCMD="${SQLCMD:-sqlcmd}"
SQL_SERVER="${SQL_SERVER:-localhost,1433}"
SQL_DATABASE="${SQL_DATABASE:-ProCargo}"
WITH_TEST_DATA=0
[[ "${1:-}" == "--with-test-data" ]] && WITH_TEST_DATA=1

AUTH_ARGS=()
if [[ -n "${SQL_AUTH:-}" ]]; then
  AUTH_ARGS+=(--authentication-method "${SQL_AUTH}")
elif [[ -n "${SQL_USER:-}" ]]; then
  AUTH_ARGS+=(-U "${SQL_USER}" -P "${SQL_PASSWORD:?SQL_PASSWORD is required with SQL_USER}")
else
  AUTH_ARGS+=(-E)
fi
TRUST_ARGS=()
[[ "${SQL_TRUST_CERT:-1}" == "1" ]] && TRUST_ARGS+=(-C)

run() {
  local database="$1" file="$2"
  echo ">> [${database}] ${file#"$SCRIPT_DIR"/}"
  "$SQLCMD" -S "$SQL_SERVER" -d "$database" "${AUTH_ARGS[@]}" "${TRUST_ARGS[@]}" -b -I -i "$file"
}

run_folder() {
  local folder="$1"
  shopt -s nullglob
  for file in "$SCRIPT_DIR/$folder"/*.sql; do
    run "$SQL_DATABASE" "$file"
  done
}

for file in "$SCRIPT_DIR"/01-Database/*.sql; do run master "$file"; done

for folder in 02-Schemas 03-Tables 04-Constraints 05-Indexes 06-Views 07-Functions 08-StoredProcedures 09-SeedData 11-Security; do
  run_folder "$folder"
done

if [[ $WITH_TEST_DATA -eq 1 ]]; then
  run_folder 10-TestData
fi

echo "ProCargo database deployed to ${SQL_SERVER}/${SQL_DATABASE}"
