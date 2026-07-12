#!/usr/bin/env bash
set -euo pipefail

# Local single-node Swarm deploy. Run from the repo root.
cd "$(dirname "$0")/.."

# 1. Initialize swarm if not already (no-op when already initialized).
docker swarm init 2>/dev/null || true

# 2. Idempotent secret creation. Dev secrets are dummies; prod operator creates
#    real ones once via `docker secret create` (see scripts/deploy-prod.sh).
create_secret() {
	local name=$1 value=$2
	docker secret inspect "$name" >/dev/null 2>&1 || printf '%s' "$value" | docker secret create "$name" -
}
create_secret db_password "dev-password"
create_secret resend_api_key "dummy-not-used-in-dev"
create_secret cloudflared_token "unused-in-dev"
# admin_password is consumed by the app service (AdminUser__PasswordFile). Must satisfy
# Identity rules: >=10 chars with upper, lower, digit, and a symbol. Matches appsettings.Development.json.
create_secret admin_password "DevAdmin!2345"

# 3. Build the image so the stack has something to pull. Without a registry,
#    Swarm reads the image from the local Docker daemon for single-node deploys.
if [[ -z "${SKIP_BUILD:-}" ]]; then
	docker build -t blazortodo:latest -f Dockerfile .
fi

# 4. Configure environment for dev: app + mailhog exposed on localhost, no
#    cloudflared.
export TAG="${TAG:-latest}"
export ASPNETCORE_ENVIRONMENT=Development
export EMAIL_PROVIDER=smtp
export EMAIL_SMTP_HOST=mailhog
export EMAIL_SMTP_PORT=1025
export EMAIL_FROM_ADDRESS="noreply@blazortodo.local"
export EMAIL_FROM_NAME="BlazorTodo (Dev)"
export POSTGRES_PASSWORD="dev-password"
# Seed a dev admin at startup so you can log in. Email/display name are env; the
# password comes from the admin_password secret created above.
export ADMIN_EMAIL="admin@blazortodo.local"
export ADMIN_DISPLAY_NAME="Dev Admin"
export LOCAL_APP_PORT=8080
export LOCAL_MAILHOG_PORT=8025
export MAILHOG_REPLICAS=1
export CLOUDFLARED_REPLICAS=0

docker stack deploy --detach=false -c docker-stack.yml todolist

echo
echo "Stack deployed."
echo "  App:     http://localhost:8080"
echo "  MailHog: http://localhost:8025"
echo
echo "Watch services with: docker stack ps todolist"
echo "Tear down with:      docker stack rm todolist"
