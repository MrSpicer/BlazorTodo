#!/usr/bin/env bash
set -euo pipefail

# Production deploy to a single-node Swarm. Run from the repo root on the host.
# Assumes secrets are already created via `docker secret create` (see scripts/seed-prod-secrets.sh).
cd "$(dirname "$0")/.."

: "${TAG:?must set TAG (e.g. TAG=$(git rev-parse --short HEAD))}"
: "${POSTGRES_PASSWORD:?must export POSTGRES_PASSWORD — must match the db_password secret}"
# Required in prod: the public hostname(s) the app accepts in the Host header, e.g.
# APP_ALLOWED_HOSTS="todo.example.com" (comma-separate multiples). Fail fast rather than
# silently falling back to "*", which would leave the app open to Host-header injection.
: "${APP_ALLOWED_HOSTS:?must set APP_ALLOWED_HOSTS to your public hostname(s), e.g. todo.example.com}"

# Ingress is via cloudflared, no host ports published.
export ASPNETCORE_ENVIRONMENT=Production
export APP_ALLOWED_HOSTS
export EMAIL_PROVIDER="${EMAIL_PROVIDER:-resend}"
export EMAIL_SMTP_HOST="${EMAIL_SMTP_HOST:-}"
export EMAIL_SMTP_PORT="${EMAIL_SMTP_PORT:-}"
export EMAIL_FROM_ADDRESS="${EMAIL_FROM_ADDRESS:-noreply@example.com}"
export EMAIL_FROM_NAME="${EMAIL_FROM_NAME:-BlazorTodo}"
export LOCAL_APP_PORT=0
export LOCAL_MAILHOG_PORT=0
export MAILHOG_REPLICAS=0
export CLOUDFLARED_REPLICAS=1

docker stack deploy --detach=false -c docker-stack.yml todolist

echo
echo "Prod stack deployed (TAG=$TAG)."
echo "Watch services with: docker service logs -f todolist_app"
echo "                     docker service logs -f todolist_cloudflared"
