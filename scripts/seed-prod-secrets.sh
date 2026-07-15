#!/usr/bin/env bash
set -euo pipefail

# One-time secret creation for a fresh production Swarm. Run on the manager node.
# Re-running is a no-op for existing secrets (idempotent).

: "${POSTGRES_PASSWORD:?must export a Postgres password to seed}"
: "${RESEND_API_KEY:?must export a Resend API key (or set to 'unused' if not using Resend)}"
# Bootstrap admin password. Must satisfy Identity rules: >=10 chars with upper, lower, digit,
# and a non-alphanumeric symbol — otherwise the startup seeder logs an error and creates no user.
: "${ADMIN_PASSWORD:?must export an admin password (>=10 chars, upper+lower+digit+symbol)}"

create_secret() {
	local name=$1 value=$2
	if docker secret inspect "$name" >/dev/null 2>&1; then
		echo "secret $name already exists, skipping"
	else
		printf '%s' "$value" | docker secret create "$name" -
		echo "created secret: $name"
	fi
}

create_secret blazortodo_db_password "$POSTGRES_PASSWORD"
create_secret blazortodo_resend_api_key "$RESEND_API_KEY"
create_secret blazortodo_admin_password "$ADMIN_PASSWORD"

echo
echo "Secrets ready. Next: TAG=<git-sha> POSTGRES_PASSWORD=$POSTGRES_PASSWORD ADMIN_EMAIL=you@example.com ./scripts/deploy-prod.sh"
