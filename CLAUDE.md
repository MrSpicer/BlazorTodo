# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Run the application (hot reload enabled by default)
dotnet run

# Build
dotnet build

# Watch mode (auto-rebuild on changes)
dotnet watch

# Publish
dotnet publish TodoList.csproj
```

This is a .NET 10 Blazor Server application. There are no tests configured.

## Architecture Overview

**BlazorTodo** is a todo management app with projects, using Blazor Server with browser local storage persistence via Blazored.LocalStorage.

### Layer Structure

- **Pages/** - Razor pages with routes:
  - `Todo.razor` (`/`) - Main todo list
  - `Statistics.razor` (`/statistics`) - Todo stats and charts
  - `Settings.razor` (`/settings`) - App settings
  - `About.razor` (`/about`) - App info
  - `Restore.razor` (`/restore`) - Restore data from a sync session ID
- **Components/** - Reusable Blazor components organized by domain:
  - `Todo/` - TodoForm, TodoFilters, TodoListView, TodoItemRow, TodoFormModal
  - `Project/` - ProjectTabs, ProjectModal
  - `Notes/` - NoteCard, NotesList, NoteFormModal
  - `Shared/` - ImportExportModal, PriorityBadge, StatusBadge, SyncModal
- **Shared/** (top-level) - MainLayout, NavMenu
- **Services/** - Business logic layer with interfaces (ITodoService, IProjectService, IDialogService, IImportExportService, INoteService, ISyncService, IFileService)
- **Data/** - Repository implementations for local storage persistence (TodoRepository, ProjectRepository, NoteRepository)
- **Interfaces/** - Repository interfaces (ITodoRepository, IProjectRepository, INoteRepository)
- **Models/** - Domain entities (TodoItem, Project, ProjectNote) and enums (Priority, TodoItemStatus, FilterOption, SortOption)

### Key Patterns

1. **Service/Repository separation**: Services handle business logic; repositories handle Blazored.LocalStorage persistence
2. **Event-driven UI updates**: Services expose `OnTodosChanged`/`OnProjectsChanged` events that components subscribe to
3. **DI registration**: All services registered in `Extensions/ServiceCollectionExtensions.cs` as scoped
4. **Component communication**: Parent components pass callbacks (`OnEdit`, `OnDelete`, `OnStatusChange`) to child components
5. **Device sync**: `SyncService` stores exported JSON in server-side `IMemoryCache` (48hr TTL, 15min rate limit). Generates QR codes via `Net.Codecrete.QrCodeGenerator`. Restore page accepts a session ID to pull data from cache.

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
