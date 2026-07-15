# Deployment Runbook

BlazorTodo deploys to a single-node Docker Swarm with Cloudflare Tunnel for ingress. `cloudflared` runs on the host (systemd or standalone container), **not** in the stack — the stack publishes the app on `localhost:8080` and the tunnel routes to it.

Local dev uses the same stack file as production with different environment knobs — there is no second config to drift out of sync.

## Architecture in one diagram

```
                 ┌───────────────────────────────────────────────┐
                 │                VPS (one node)                 │
                 │                                               │
  user browser   │  ┌──────────────┐   ┌ Swarm stack ─────────┐  │
        │        │  │ cloudflared  │   │ ┌─────────────────┐  │  │
        │        │  │ (host-run,   │◄──┼►│ app             │  │  │
        ▼        │  │  outbound)   │   │ │ localhost:8080  │  │  │
 https://todo... │  └──────┬───────┘   │ └────────┬────────┘  │  │
        │        │         │           │          ▼           │  │
        ▼        │         │           │ ┌────────────────┐   │  │
 ┌────────────┐  │         │           │ │  postgres:17   │   │  │
 │ Cloudflare │──┼─────────┘           │ │  (volume:      │   │  │
 │   edge     │  │                     │ │  postgres_data)│   │  │
 └────────────┘  │                     │ └────────────────┘   │  │
                 │                     └──────────────────────┘  │
                 └───────────────────────────────────────────────┘
```

- The VPS opens **no** inbound ports. `cloudflared` runs on the host, connects outbound to Cloudflare's edge, and proxies requests to `http://localhost:8080` — the host port the stack publishes for the `app` service (`APP_PORT`). The firewall stays closed, so the port is only reachable from the host itself.
- TLS is terminated by Cloudflare; the app speaks plain HTTP internally.

## Production bring-up (one-time)

### 1. Provision the VPS

Any provider works (Hetzner, DO, Vultr, Linode). 2 vCPU / 4 GB RAM is plenty for v1. **Do not open ports 80/443** — Cloudflare Tunnel uses outbound HTTPS only.

### 2. Install Docker + initialize Swarm

```bash
curl -fsSL https://get.docker.com | sh
docker swarm init
```

### 3. Set up the Cloudflare Tunnel (host-level, outside this stack)

`cloudflared` is a server prerequisite — install and run it on the host however you prefer (systemd service via `cloudflared service install <token>`, or a standalone container with host networking). This stack does not manage it.

In the Cloudflare Zero Trust dashboard → **Networks → Tunnels → Create tunnel**:

1. Name the tunnel (e.g. `blazortodo`) and install the connector on the host with the token Cloudflare gives you.
2. Under **Public Hostnames**, add a route:
   - Subdomain: `todo`
   - Domain: your zone (e.g. `example.com`)
   - Service type: **HTTP**
   - URL: `localhost:8080`
   (The stack publishes the app on host port 8080 — `APP_PORT` in `deploy-prod.sh`.)

### 4. Seed secrets on the swarm

```bash
export POSTGRES_PASSWORD="$(openssl rand -base64 32)"
export RESEND_API_KEY="re_..."             # or "unused" if you'll configure SMTP instead
# Bootstrap admin password. Must satisfy Identity rules: >=10 chars with upper, lower, digit,
# and a symbol — the startup seeder rejects anything weaker.
export ADMIN_PASSWORD='<strong-password>'

./scripts/seed-prod-secrets.sh
```

Secrets created: `blazortodo_db_password`, `blazortodo_resend_api_key`, `blazortodo_admin_password`.

### 5. Build the image and deploy

```bash
docker build -t blazortodo:$(git rev-parse --short HEAD) -f Dockerfile .

TAG=$(git rev-parse --short HEAD) \
POSTGRES_PASSWORD="$POSTGRES_PASSWORD" \
APP_ALLOWED_HOSTS="todo.example.com" \
ADMIN_EMAIL="you@example.com" \
ADMIN_DISPLAY_NAME="Administrator" \
EMAIL_PROVIDER=resend \
EMAIL_FROM_ADDRESS="noreply@example.com" \
EMAIL_FROM_NAME="BlazorTodo" \
./scripts/deploy-prod.sh
```

On startup the app applies EF Core migrations and seeds this admin account (pre-confirmed, so it
can sign in immediately without an email-confirmation step) with the `Admin` role. Seeding is
idempotent and never resets the password of an existing account.

For a registry-based workflow (CI publishes images): change the `image:` in `docker-stack.yml` to reference your registry path, and use `docker pull` on the host before `deploy-prod.sh`.

### 6. Watch it come up

```bash
docker stack ps todolist
docker service logs -f todolist_app
```

Once the app is running and the host's `cloudflared` reports "Registered tunnel connection" (`journalctl -u cloudflared -f` for the systemd install), browse to your public hostname. Cloudflare serves the cert.

### 7. Database migrations (automatic)

EF Core migrations are applied automatically at startup by the app (see `DatabaseInitializer`,
invoked from `Program.cs`) — no manual `dotnet ef database update` step is needed. The bootstrap
admin (step 5) is seeded in the same startup pass, right after migrations.

> **Caveat:** `update_config: { order: start-first }` briefly overlaps two `app` containers, so
> two `MigrateAsync()` calls can race on a schema change. This is fine for the single-replica
> hobby deploy here; if you scale out or ship risky migrations, gate migrations behind a Postgres
> advisory lock or a dedicated one-off migration job.

## Local dev (Swarm-on-laptop)

```bash
./scripts/deploy-local.sh
```

This builds the image, initializes a single-node swarm if needed, creates dummy secrets, and deploys with:

- App on `http://localhost:8080`
- smtp4dev on `http://localhost:8025` (catches all outbound mail)

Tear down: `docker stack rm todolist`. Volumes persist — remove them with `docker volume rm blazortodo_postgres_data` if you want a clean DB.

Note: the simpler `dotnet run` + `docker-compose.dev.yml` (Postgres + smtp4dev only) is still the fastest iteration loop for code changes. Use the Swarm deploy when you want to verify the production stack end-to-end.

## Updates

```bash
docker build -t blazortodo:$(git rev-parse --short HEAD) -f Dockerfile .
TAG=$(git rev-parse --short HEAD) POSTGRES_PASSWORD="$POSTGRES_PASSWORD" ./scripts/deploy-prod.sh
```

`update_config: { order: start-first }` in the stack file means the new replica starts before the old one stops — minimum downtime for a single-replica deploy.

**Migrating from the old stack-managed cloudflared:** `docker stack deploy --prune` removes the retired `cloudflared` service automatically on the next deploy. Set up the host-level `cloudflared` (step 3) *before* deploying, then delete the orphaned secret: `docker secret rm cloudflared_token`.

## Rolling back

```bash
TAG=<previous-git-sha> POSTGRES_PASSWORD="$POSTGRES_PASSWORD" ./scripts/deploy-prod.sh
```

Make sure the image for the previous tag still exists on the host (or is pullable from your registry).

## Useful commands

| What | Command |
|---|---|
| List services | `docker stack ps todolist` |
| Tail app logs | `docker service logs -f todolist_app` |
| Shell into the app | `docker exec -it $(docker ps -qf name=todolist_app) sh` |
| psql into Postgres | `docker exec -it $(docker ps -qf name=todolist_postgres) psql -U todolist -d todolist` |
| Manual `pg_dump` | `docker exec -t $(docker ps -qf name=todolist_postgres) pg_dump -U todolist todolist \| gzip > backup-$(date +%F).sql.gz` |
| Tear down stack | `docker stack rm todolist` |

## Troubleshooting

**Swarm stack shadows compose ports.** If you previously ran `./scripts/deploy-local.sh`
and later switch back to the plain `docker-compose.dev.yml` workflow, the Swarm-managed
`todolist_smtp4dev` service can hold onto container names and networks the compose file
expects. Symptom: `docker ps` shows containers up but with no host port bindings, and the
.NET app fails to connect to `localhost:1025` (smtp4dev SMTP).

```bash
docker stack rm todolist
docker compose -f docker-compose.dev.yml up -d
```

## What's not in this runbook (intentional, see Phase 13 of the plan)

- Automated database backups — currently manual `pg_dump`.
- Off-site backup replication.
- Redis SignalR backplane (only needed when scaling to multiple app replicas).
- Multi-region failover.
- OAuth providers (Google/Microsoft) — Identity supports them; UI to be added.
