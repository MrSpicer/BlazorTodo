#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

# Stop any existing container so the port is free
docker compose down --remove-orphans 2>/dev/null || true

docker compose up -d
echo "App running at http://localhost:5217"
