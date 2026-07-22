# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Run the application (from git root)
dotnet run --project src/TodoList.csproj

# Build (from git root)
dotnet build BlazorTodo.sln

# Watch mode
cd src && dotnet watch

# Publish
dotnet publish src/TodoList.csproj
```

This is a .NET 10 Blazor Server application. There are no tests configured.

The app requires a PostgreSQL connection string (`ConnectionStrings:Default`) to start — DI throws without it.
For local dev, bring up Postgres + smtp4dev first: `docker compose -f docker-compose.dev.yml up -d`. The dev
connection string and a dev admin (`admin@blazortodo.local` / `DevAdmin!2345`) are already in
`src/appsettings.Development.json`. EF Core migrations are applied automatically at startup by `DatabaseInitializer`.
See `DEPLOY.md` for production (Docker Swarm + Cloudflare Tunnel).

## Architecture Overview

**BlazorTodo** is a todo management app with projects, built on Blazor Server. Persistence is **dual-mode**:
anonymous visitors persist to browser local storage (Blazored.LocalStorage); authenticated users persist to
**PostgreSQL via EF Core**. The service layer is unaware of which backend is active — `Routing*Repository` classes
dispatch per request on `ICurrentUserContext.IsAuthenticated`. Authenticated users also get real-time
multi-device sync. Accounts, email confirmation, password reset, and an `Admin` role come from ASP.NET Core Identity.

### Layer Structure

All source lives under `src/`. Non-source files (sln, Dockerfile, docs, scripts) stay at the git root.

- **src/Components/Pages/** - Razor pages with routes:
  - `Todo.razor` (`/`) - Main todo list
  - `Statistics.razor` (`/statistics`) - Todo stats and charts
  - `Settings.razor` (`/settings`) - App settings
  - `About.razor` (`/about`) - App info
  - `Admin.razor` (`/admin`) - Admin dashboard, gated by `[Authorize(Roles = "Admin")]`
- **src/Components/** - Reusable Blazor components organized by domain:
  - `Todo/` - TodoForm, TodoFilters, TodoListView, TodoItemRow, TodoFormModal
  - `Project/` - ProjectTabs, ProjectModal
  - `Notes/` - NoteCard, NotesList, NoteFormModal
  - `Shared/` - Modal (shared modal shell), ImportExportModal, TagsModal, StatusManagementModal, PriorityManagementModal, CreateRoleModal, RoleManagementModal, LocalDataMigrationPrompt, TagSelector, PriorityBadge, StatusBadge, RedirectToLogin, MultiDeviceSyncListener
  - `Layout/` - MainLayout (auth header + sync listener), NavMenu (Admin link gated by `<AuthorizeView Roles="Admin">`)
- **src/Services/** - Business logic layer with interfaces (ITodoService, IProjectService, IDialogService, IImportExportService, INoteService, IFileService, ITagService, IStatusService, IPriorityService, IFilterPresetService, UserOnboardingService)
  - `Admin/` - Admin layer (IAdminService, IConnectionTracker, ILoginActivityTracker, AdminCircuitHandler)
- **src/Data/** - Repositories and `AppDbContext`. Three repository implementations per aggregate: `LocalStorage*` (anonymous), `Ef*` (authenticated, in `Repositories/`, per-user scoped via `EfRepositoryBase`/`RequireUserId()`), and `Routing*` (wrapper the interfaces resolve to, dispatching on auth state). Also `Configurations/` (EF `IEntityTypeConfiguration`s), `Migrations/`, and `DatabaseInitializer`.
- **src/Identity/** - ASP.NET Identity: `ApplicationUser : IdentityUser<Guid>`, `TrackingSignInManager`, `SmtpEmailSender`, `AdminSeedOptions`, `UserOnboardingService`, `ICurrentUserContext`
- **src/Realtime/** - `IUserChangeBus`/`UserChangeBus` (in-process pub/sub keyed by UserId) powering multi-device sync
- **src/Models/** - Domain entities (TodoItem, Project, ProjectNote, Tag, Status, Priority, FilterPreset) and enums (Priority, TodoItemStatus, FilterOption, SortOption). Server-persisted entities carry a `UserId`.

### Key Patterns

1. **Service/Repository separation**: Services handle business logic; repositories handle persistence
2. **Routing repositories**: The `ITodoRepository`/`IProjectRepository`/etc. interfaces resolve to `Routing*Repository` wrappers that delegate to the `Ef*` (server/Postgres) repo when `ICurrentUserContext.IsAuthenticated`, else the `LocalStorage*` (browser) repo. EF repos scope every query by `UserId` and re-validate DataAnnotations at the persistence boundary (`EfRepositoryBase`)
3. **Event-driven UI updates**: Services expose `OnTodosChanged`/`OnProjectsChanged` events that components subscribe to
4. **DI registration**: Services registered in `Extensions/ServiceCollectionExtensions.cs` (scoped). Identity is wired here too: `AddIdentity<ApplicationUser, IdentityRole<Guid>>().AddDefaultUI().AddSignInManager<TrackingSignInManager>()` (RequireConfirmedAccount/Email, password length 10, lockout after 5). `AppDbContext` uses `AddDbContextFactory` (circuits outlive requests, so repos build short-lived contexts) with Npgsql `EnableDynamicJson()` for `jsonb` columns
5. **Component communication**: Parent components pass callbacks (`OnEdit`, `OnDelete`, `OnStatusChange`) to child components
6. **Multi-device sync**: For authenticated users, `IUserChangeBus` (`src/Realtime/`, in-process pub/sub keyed by UserId) notifies the `MultiDeviceSyncListener` in `MainLayout` so changes made on one device refresh others in real time
7. **Admin & Identity**: `DatabaseInitializer` (invoked from `Program.cs`) applies migrations and seeds the `Admin` role + admin user from the `AdminUser` config section (or `/run/secrets/admin_password`). `TrackingSignInManager` records failed logins for the admin dashboard; `AdminCircuitHandler`/`IConnectionTracker` track live connections; `ILoginActivityTracker` holds recent failures. `src/Program.cs` adds production hardening (HSTS, ForwardedHeaders for Cloudflare, secure auth cookies, CSP, per-IP rate limiting)
8. **Modals**: Every modal renders through the single shared shell `Components/Shared/Modal.razor`. It owns the overlay, header (`Title` + optional `HeaderIcon`), and close button, and marks its container/overlay with `app-modal`/`app-modal-overlay`. Content goes in either the `BodyContent` (scrolling `.modal-body`) + `FooterContent` (pinned `.modal-footer`) slots for non-form modals, or — for **form** modals — via `ChildContent` holding a `<form class="modal-form">` that wraps a `.modal-body` and a sibling `.modal-footer` (so a `type=submit` button in the footer stays pinned yet still submits). `Size` = `compact` (440px) / `medium` (600px) / `wide` (760px) / `large` (90vw) / `default` (75vw); all are content-height capped at `min(85vh, 900px)` on desktop and full-screen (`100dvh`) at ≤640px. The shell + form primitives + shared management-list styles live in `wwwroot/css/site.css`, **not** in component `<style>` blocks (see Code Style)

### Domain Model

- **Project**: Container for todos with name, description, color. One project is marked `IsDefault`
- **TodoItem**: Has Title, Description, Priority (Low/Medium/High/Emergency), Status (None/New/InProgress/Done/Abandoned/Archived), belongs to a Project via `ProjectId`. Supports nested `SubTasks List<TodoItem>` (one level deep).
- **ProjectNote**: Per-project notes with Title and Content (up to 5000 chars), linked via `ProjectId`

### Namespace

Root namespace is `TodoList` (despite repo name BlazorTodo).

## Code Style

- **Indentation**: Tabs, not spaces
- **Namespaces**: File-scoped (`namespace TodoList.Services;`)
- **Private fields**: `_camelCase` with underscore prefix
- **Async methods**: Always suffix with `Async` (`SaveTodoAsync`, `InitializeAsync`)
- **Nullable reference types**: Enabled; use `?` for nullable types, initialize strings to `string.Empty`
- **Collections**: Expose as `IReadOnlyList<T>`, use `Task<bool>` for fallible operations
- **Error handling**: Try-catch at service/repository boundaries, log with `ILogger<T>`, return `false`/empty rather than throwing
- **Components**: `[Parameter, EditorRequired]` for required inputs, `EventCallback<T>` for callbacks, implement `IDisposable` to unsubscribe from service events
- **CSS / `<style>` blocks are global**: there are no scoped `.razor.css` files — a `<style>` block inside a `.razor` renders into the DOM and leaks app-wide. Keep only genuinely component-specific rules in component `<style>`; put anything shared (the modal shell, `.form-input/.form-group/.form-label/.form-row/.form-stack`, the `.status-*`/`.color-input*` management-list styles) in `wwwroot/css/site.css`. **Never redefine `.modal-*` or `.form-input` in a component `<style>`** — doing so silently overrides the shared shell for every other modal by render order
