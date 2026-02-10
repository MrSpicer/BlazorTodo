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

This is a .NET 9 Blazor Server application. There are no tests configured.

## Architecture Overview

**TaskFlow** is a todo management app with projects, using Blazor Server with browser local storage persistence via Blazored.LocalStorage.

### Layer Structure

- **Pages/** - Razor pages with routes. Main page is `Todo.razor` at `/`
- **Components/** - Reusable Blazor components organized by domain:
  - `Todo/` - TodoForm, TodoFilters, TodoListView, TodoItemRow, TodoFormModal
  - `Project/` - ProjectTabs, ProjectModal
  - `Shared/` - ImportExportModal, PriorityBadge, StatusBadge
- **Services/** - Business logic layer with interfaces (ITodoService, IProjectService, IDialogService, IImportExportService)
- **Data/** - Repository layer for local storage persistence (TodoRepository, ProjectRepository)
- **Models/** - Domain entities (TodoItem, Project) and enums (Priority, TodoItemStatus, FilterOption, SortOption)

### Key Patterns

1. **Service/Repository separation**: Services handle business logic; repositories handle Blazored.LocalStorage persistence
2. **Event-driven UI updates**: Services expose `OnTodosChanged`/`OnProjectsChanged` events that components subscribe to
3. **DI registration**: All services registered in `Extensions/ServiceCollectionExtensions.cs` as scoped
4. **Component communication**: Parent components pass callbacks (`OnEdit`, `OnDelete`, `OnStatusChange`) to child components

### Domain Model

- **Project**: Container for todos with name, description, color. One project is marked `IsDefault`
- **TodoItem**: Has Title, Description, Priority (Low/Medium/High/Emergency), Status (None/New/InProgress/Done/Abandoned/Archived), belongs to a Project via `ProjectId`

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
