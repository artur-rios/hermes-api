#!/bin/sh
# The `docker` check: the container image can still be built. Build only, no push and no run. yggdrasil.yml runs it in
# the pinned docker CLI image (an Alpine one: no bash, hence sh) from the repository's root.
set -eu

# The repository's root, wherever this is run from.
cd "$(dirname "$0")/.."

docker build --file Dockerfile --tag hermes-api-check:ci .
