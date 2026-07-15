#!/usr/bin/env bash
set -euo pipefail

# Production deploy to a single-node Swarm. Run from the repo root on the host.
# Assumes secrets are already created via `docker secret create` (see scripts/seed-prod-secrets.sh).
cd "$(dirname "$0")/.."

: "${TAG:?must set TAG (e.g. TAG=$(git rev-parse --short HEAD))}"
: "${POSTGRES_PASSWORD:?must export POSTGRES_PASSWORD — must match the blazortodo_db_password secret}"
# Required in prod: the public hostname(s) the app accepts in the Host header, e.g.
# APP_ALLOWED_HOSTS="todo.example.com" (comma-separate multiples). Fail fast rather than
# silently falling back to "*", which would leave the app open to Host-header injection.
: "${APP_ALLOWED_HOSTS:?must set APP_ALLOWED_HOSTS to your public hostname(s), e.g. todo.example.com}"
# Email of the bootstrap admin account seeded at startup. The password comes from the
# blazortodo_admin_password Docker secret (see scripts/seed-prod-secrets.sh).
: "${ADMIN_EMAIL:?must set ADMIN_EMAIL for the bootstrap admin account, e.g. you@example.com}"

# Ingress is via the host-run cloudflared: the app publishes APP_PORT on the host
# and the tunnel's public-hostname route points at http://localhost:${APP_PORT}.
# The VPS firewall stays closed to inbound traffic, so the port is not public.
export ASPNETCORE_ENVIRONMENT=Production
export APP_ALLOWED_HOSTS
export ADMIN_EMAIL
export ADMIN_DISPLAY_NAME="${ADMIN_DISPLAY_NAME:-Administrator}"
export EMAIL_PROVIDER="${EMAIL_PROVIDER:-resend}"
# Resend's SMTP relay: username is literally "resend"; the password is the API key (re_...).
# Port 587 + EnableSsl=true = STARTTLS (the only TLS mode System.Net.Mail.SmtpClient supports).
: "${EMAIL_SMTP_PASSWORD:?must export EMAIL_SMTP_PASSWORD (the Resend API key, re_...)}"
export EMAIL_SMTP_HOST="${EMAIL_SMTP_HOST:-smtp.resend.com}"
export EMAIL_SMTP_PORT="${EMAIL_SMTP_PORT:-587}"
export EMAIL_SMTP_USERNAME="${EMAIL_SMTP_USERNAME:-resend}"
export EMAIL_SMTP_PASSWORD
export EMAIL_SMTP_ENABLE_SSL="${EMAIL_SMTP_ENABLE_SSL:-true}"
export EMAIL_FROM_ADDRESS="${EMAIL_FROM_ADDRESS:-noreply@example.com}"
export EMAIL_FROM_NAME="${EMAIL_FROM_NAME:-BlazorTodo}"
export APP_PORT="${APP_PORT:-8080}"
export LOCAL_SMTP4DEV_PORT=0
export SMTP4DEV_REPLICAS=0

docker stack deploy --detach=false --prune -c docker-stack.yml todolist

echo
echo "Prod stack deployed (TAG=$TAG)."
echo "Watch services with: docker service logs -f todolist_app"
echo
echo "Ingress: the host-run cloudflared must route the tunnel's public hostname"
echo "to http://localhost:${APP_PORT}."
