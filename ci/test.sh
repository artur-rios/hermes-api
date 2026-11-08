#!/usr/bin/env bash
# The `test` check: restore, build, the unit and functional suites, and the coverage floor. yggdrasil.yml runs it in
# the pinned .NET SDK image from the repository's root, and it runs the same on your machine.
#
# Both suites run in one test run of the one test project, so its single report is the merged line coverage.
set -euo pipefail

SOLUTION=ArturRios.Hermes.sln
COVERAGE_DIRECTORY=artifacts/coverage
COVERAGE_FLOOR=${COVERAGE_FLOOR:-90}

# The repository's root, wherever this is run from.
cd "$(dirname "${BASH_SOURCE[0]}")/.."

export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

rm -rf "$COVERAGE_DIRECTORY"

dotnet restore "$SOLUTION"
dotnet build "$SOLUTION" --configuration Release --no-restore
dotnet test "$SOLUTION" --configuration Release --no-build \
  --collect "XPlat Code Coverage" --results-directory "$COVERAGE_DIRECTORY"

report=$(find "$COVERAGE_DIRECTORY" -name coverage.cobertura.xml | head -n 1)

if [ -z "$report" ]; then
  echo "ci/test.sh: the test run produced no coverage report" >&2
  exit 1
fi

line_rate=$(sed -n 's/.*<coverage [^>]*line-rate="\([0-9.]*\)".*/\1/p' "$report" | head -n 1)

if [ -z "$line_rate" ]; then
  echo "ci/test.sh: no line rate in $report" >&2
  exit 1
fi

if ! awk -v rate="$line_rate" -v floor="$COVERAGE_FLOOR" 'BEGIN {
  coverage = rate * 100
  printf "Line coverage: %.2f%% (floor: %s%%)\n", coverage, floor
  exit !(coverage >= floor)
}'; then
  echo "ci/test.sh: line coverage is below the floor of ${COVERAGE_FLOOR}%" >&2
  exit 1
fi
