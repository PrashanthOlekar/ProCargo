#!/usr/bin/env bash
# Runs a command, streams its output to the job log and, if it fails, publishes the tail of the
# output as a GitHub check annotation so the failure reason is visible on the run summary page
# (and through the checks API) without opening the full log.
#
# Usage: .github/scripts/run-and-annotate.sh "<title>" <command> [args...]
set -uo pipefail

title="$1"; shift
log_file="$(mktemp)"

"$@" 2>&1 | tee "$log_file"
status=${PIPESTATUS[0]}

if [[ $status -ne 0 ]]; then
  # Prefer lines that look like errors; fall back to the raw tail.
  summary="$(grep -E -i "error|failed|msg [0-9]+|level 1[1-9]|exception|assert" "$log_file" | tail -n 60)"
  [[ -z "$summary" ]] && summary="$(tail -n 60 "$log_file")"
  summary="${summary:0:12000}"
  summary="${summary//'%'/'%25'}"
  summary="${summary//$'\r'/'%0D'}"
  summary="${summary//$'\n'/'%0A'}"
  echo "::error title=${title}::${summary}"
fi

rm -f "$log_file"
exit $status
