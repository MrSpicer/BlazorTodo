# BlazorTodo

A fast todo app with projects, subtasks, and optional accounts — built with .NET 10 Blazor Server.

Use it without signing up (your data stays in your browser), or create an account to save your data
on the server and sync it across devices in real time.

## Tech Stack

- [.NET 10](https://dotnet.microsoft.com/) — Blazor Server (real-time UI over SignalR)
- [PostgreSQL](https://www.postgresql.org/) + [EF Core](https://learn.microsoft.com/ef/core/) (Npgsql) — server-side persistence for signed-in users
- [ASP.NET Core Identity](https://learn.microsoft.com/aspnet/core/security/authentication/identity) — accounts, email confirmation, password reset, roles
- [Blazored.LocalStorage](https://github.com/Blazored/LocalStorage) — browser persistence for anonymous users
- [Bootstrap 5](https://getbootstrap.com/) — UI

## Data & Accounts

Persistence is dual-mode:

- **Anonymous** — todos, projects, and notes are stored entirely in your browser's `localStorage`. No account, nothing leaves your machine.
- **Signed in** — data is saved on the server in PostgreSQL and synced across your devices in real time. Sign-in is optional; the same UI works either way.

The app ships a single `Admin` role. Admins get a dashboard at `/admin` (user management, active connections, login activity).

## Getting Started

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (for the local Postgres + mail server).

The app needs a PostgreSQL connection string (`ConnectionStrings:Default`) to start. The fastest local loop is
to bring up Postgres and a mail catcher with the dev compose file, then run the app — the dev connection string
and a dev admin are already configured in `src/appsettings.Development.json`.

```bash
git clone https://github.com/MrSpicer/BlazorTodo.git
cd BlazorTodo

# Start Postgres (5432) + smtp4dev (SMTP 1025 / UI 8025)
docker compose -f docker-compose.dev.yml up -d

# Run the app
dotnet run --project src/TodoList.csproj
```

Open [http://localhost:5217](http://localhost:5217).

Dev admin login: `admin@blazortodo.local` / `DevAdmin!2345`. Confirmation and password-reset emails are captured by
smtp4dev at [http://localhost:8025](http://localhost:8025).

## Deployment

Production runs on a single-node Docker Swarm behind a Cloudflare Tunnel, with PostgreSQL and automatic EF Core
migrations. See **[DEPLOY.md](DEPLOY.md)** for the full runbook. A pre-built image is published on
[Docker Hub](https://hub.docker.com/r/mylsotol/blazortodo) (`mylsotol/blazortodo`) — note it requires a Postgres
database and connection string, so it isn't runnable standalone with a bare `docker run`.

Give it a try at https://blazortodo.justinspicer.com/
