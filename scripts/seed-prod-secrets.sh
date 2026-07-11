#!/usr/bin/env bash
set -euo pipefail

# One-time secret creation for a fresh production Swarm. Run on the manager node.
# Re-running is a no-op for existing secrets (idempotent).

: "${POSTGRES_PASSWORD:?must export a Postgres password to seed}"
: "${RESEND_API_KEY:?must export a Resend API key (or set to 'unused' if not using Resend)}"
: "${CF_TUNNEL_TOKEN:?must export the Cloudflare Tunnel token from the Zero Trust dashboard}"

create_secret() {
	local name=$1 value=$2
	if docker secret inspect "$name" >/dev/null 2>&1; then
		echo "secret $name already exists, skipping"
	else
		printf '%s' "$value" | docker secret create "$name" -
		echo "created secret: $name"
	fi
}

create_secret db_password "$POSTGRES_PASSWORD"
create_secret resend_api_key "$RESEND_API_KEY"
create_secret identity_signing_key "$(openssl rand -base64 64)"
create_secret cloudflared_token "$CF_TUNNEL_TOKEN"

echo
echo "Secrets ready. Next: TAG=<git-sha> POSTGRES_PASSWORD=$POSTGRES_PASSWORD ./scripts/deploy-prod.sh"
