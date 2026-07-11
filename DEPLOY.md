# Deployment Runbook

BlazorTodo deploys to a single-node Docker Swarm with Cloudflare Tunnel for ingress.

Local dev uses the same stack file as production with different environment knobs — there is no second config to drift out of sync.

## Architecture in one diagram

```
                 ┌─────────────────────────────────────────────┐
                 │              VPS (one node)                 │
                 │                                             │
  user browser   │   ┌──────────────┐    ┌─────────────────┐   │
        │        │   │ cloudflared  │◄──►│  app:8080       │   │
        │        │   │  (outbound)  │    │  (Blazor Server)│   │
        ▼        │   └──────┬───────┘    └─────────┬───────┘   │
 https://todo... │          │ overlay net           │           │
        │        │          │                       ▼           │
        ▼        │          │              ┌────────────────┐   │
 ┌────────────┐  │          │              │  postgres:17   │   │
 │ Cloudflare │──┼──────────┘              │  (volume:      │   │
 │   edge     │  │                         │  postgres_data)│   │
 └────────────┘  │                         └────────────────┘   │
                 │                                             │
                 └─────────────────────────────────────────────┘
```

- The VPS opens **no** inbound ports. `cloudflared` connects outbound to Cloudflare's edge and proxies requests to `app:8080` over the Swarm overlay network.
- TLS is terminated by Cloudflare; the app speaks plain HTTP internally.

## Production bring-up (one-time)

### 1. Provision the VPS

Any provider works (Hetzner, DO, Vultr, Linode). 2 vCPU / 4 GB RAM is plenty for v1. **Do not open ports 80/443** — Cloudflare Tunnel uses outbound HTTPS only.

### 2. Install Docker + initialize Swarm

```bash
curl -fsSL https://get.docker.com | sh
docker swarm init
```

### 3. Create the Cloudflare Tunnel

In the Cloudflare Zero Trust dashboard → **Networks → Tunnels → Create tunnel**:

1. Connector type: **Docker**.
2. Name the tunnel (e.g. `blazortodo`).
3. Cloudflare gives you a token — copy it.
4. Under **Public Hostnames**, add a route:
   - Subdomain: `todo`
   - Domain: your zone (e.g. `example.com`)
   - Service type: **HTTP**
   - URL: `app:8080`
   (The hostname `app` resolves over the Swarm overlay network from inside the `cloudflared` container.)

### 4. Seed secrets on the swarm

```bash
export POSTGRES_PASSWORD="$(openssl rand -base64 32)"
export RESEND_API_KEY="re_..."             # or "unused" if you'll configure SMTP instead
export CF_TUNNEL_TOKEN="<token from step 3>"

./scripts/seed-prod-secrets.sh
```

Secrets created: `db_password`, `resend_api_key`, `cloudflared_token`.

### 5. Build the image and deploy

```bash
docker build -t blazortodo:$(git rev-parse --short HEAD) -f Dockerfile .

TAG=$(git rev-parse --short HEAD) \
POSTGRES_PASSWORD="$POSTGRES_PASSWORD" \
APP_ALLOWED_HOSTS="todo.example.com" \
EMAIL_PROVIDER=resend \
EMAIL_FROM_ADDRESS="noreply@example.com" \
EMAIL_FROM_NAME="BlazorTodo" \
./scripts/deploy-prod.sh
```

For a registry-based workflow (CI publishes images): change the `image:` in `docker-stack.yml` to reference your registry path, and use `docker pull` on the host before `deploy-prod.sh`.

### 6. Watch it come up

```bash
docker stack ps todolist
docker service logs -f todolist_app
docker service logs -f todolist_cloudflared
```

Once both services are running and `cloudflared` reports "Registered tunnel connection", browse to your public hostname. Cloudflare serves the cert.

### 7. Run database migrations

EF Core migrations are not applied automatically by the image; do it once after first deploy:

```bash
docker exec -it $(docker ps -qf name=todolist_app) dotnet ef database update --no-build
```

(Or build a one-off migration container and run `dotnet ef` against the prod DB from a developer machine.)

## Local dev (Swarm-on-laptop)

```bash
./scripts/deploy-local.sh
```

This builds the image, initializes a single-node swarm if needed, creates dummy secrets, and deploys with:

- App on `http://localhost:8080`
- MailHog on `http://localhost:8025` (catches all outbound mail)
- No `cloudflared` container

Tear down: `docker stack rm todolist`. Volumes persist — remove them with `docker volume rm blazortodo_postgres_data` if you want a clean DB.

Note: the simpler `dotnet run` + `docker-compose.dev.yml` (Postgres + MailHog only) is still the fastest iteration loop for code changes. Use the Swarm deploy when you want to verify the production stack end-to-end.

## Updates

```bash
docker build -t blazortodo:$(git rev-parse --short HEAD) -f Dockerfile .
TAG=$(git rev-parse --short HEAD) POSTGRES_PASSWORD="$POSTGRES_PASSWORD" ./scripts/deploy-prod.sh
```

`update_config: { order: start-first }` in the stack file means the new replica starts before the old one stops — minimum downtime for a single-replica deploy.

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
`todolist_mailhog` service can hold onto container names and networks the compose file
expects. Symptom: `docker ps` shows containers up but with no host port bindings, and the
.NET app fails to connect to `localhost:1025` (MailHog SMTP).

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
