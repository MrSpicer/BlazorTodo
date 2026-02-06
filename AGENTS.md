# AGENTS.md - BlazorTodo Development Guide

## Build & Run Commands

### Build
```bash
dotnet build TodoList.csproj
```

### Run Development Server
```bash
dotnet run
# or with hot reload:
dotnet watch run
```

### Publish
```bash
dotnet publish TodoList.csproj
```

### Testing
Currently no test projects exist. When adding tests:
- Create test project: `dotnet new xunit -n TodoList.Tests`
- Run all tests: `dotnet test`
- Run single test: `dotnet test --filter "FullyQualifiedName~TodoService.SaveTodoAsync_ShouldReturnTrue"`

## Project Structure

- `/Components` - Reusable Blazor components organized by feature
  - `/Todo` - Todo-specific components
  - `/Project` - Project management components  
  - `/Shared` - Shared components across features
- `/Data` - Repository implementations (LocalStorage)
- `/Extensions` - Extension methods (DI registration)
- `/Helpers` - Helper classes and utilities
- `/Interfaces` - Interface definitions (I-prefix)
- `/Models` - Data models and entities
  - `/Enums` - Enumeration types
- `/Pages` - Routable Blazor pages
- `/Services` - Business logic services
- `/Shared` - Shared layout components
- `/wwwroot` - Static assets (CSS, images, etc.)

## Technology Stack

- .NET 9.0
- Blazor Server
- Blazored.LocalStorage (data persistence)
- Bootstrap 5 (UI framework)

## Code Style Guidelines

### Namespaces & Imports
- Use file-scoped namespaces: `namespace TodoList.Services;`
- Implicit usings enabled (common namespaces auto-imported)
- Explicit using for project namespaces
- Razor imports in `_Imports.razor` for components

### Naming Conventions
- **Classes/Methods/Properties**: PascalCase (`TodoService`, `GetTodos()`)
- **Private fields**: _camelCase with underscore prefix (`_repository`, `_logger`)
- **Local variables**: camelCase (`var todoList`)
- **Interfaces**: I-prefix (`ITodoService`, `ITodoRepository`)
- **Enums**: PascalCase for type and values (`Priority.High`)
- **Parameters**: camelCase (`projectId`)
- **Async methods**: Suffix with `Async` (`SaveTodoAsync`, `InitializeAsync`)

### Types & Nullability
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Use `?` for nullable types: `Guid?`, `DateTime?`, `Project?`
- Initialize strings to `string.Empty` not null
- Use `Task<bool>` for operations that can fail
- Prefer `IReadOnlyList<T>` for exposed collections

### Formatting
- Tabs for indentation (project uses tabs)
- Braces on new lines for methods and classes
- Use expression-bodied members for simple properties: `=> value`
- Switch expressions with `=>` pattern matching

### Error Handling
- Use try-catch at repository/service boundaries
- Log errors using `ILogger<T>`
- Return `false` or empty collections rather than throwing on failure
- Validate inputs before processing (`IsValid()` methods)
- Log at appropriate levels: Debug, Error, etc.

### Documentation
- XML doc comments (`///`) on all public classes and methods
- Use `<summary>` tags to describe purpose
- Document parameters with `<param>` when non-obvious
- Example:
```csharp
/// <summary>
/// Implementation of ITodoService for managing todo items.
/// </summary>
public class TodoService : ITodoService
```

## Blazor Component Guidelines

### Component Structure
```razor
@* Imports/directives at top *@
@using TodoList.Models
@inject ITodoService TodoService

@* HTML/Markup *@
<div class="component-content">
    @* ... *@
</div>

@* Code block at bottom *@
@code {
    [Parameter, EditorRequired]
    public TodoItem Todo { get; set; } = new();
    
    [Parameter]
    public EventCallback<TodoItem> OnSubmit { get; set; }
}
```

### Component Patterns
- Use `[Parameter]` for component inputs
- Use `[EditorRequired]` for required parameters
- Use `EventCallback<T>` for parent-child communication
- Initialize parameters with sensible defaults
- Use `@inject` for dependency injection in components
- Organize by feature in `/Components/{Feature}` folders

### State Management
- Services use events: `event Action? OnTodosChanged`
- Components subscribe in `OnInitialized` lifecycle
- Call `StateHasChanged()` when needed
- Unsubscribe in `Dispose()` if implementing `IDisposable`

## Service & Dependency Injection

### Service Registration
- Register services via extension methods in `/Extensions`
- Use scoped lifetime for most services: `services.AddScoped<T, TImpl>()`
- Pattern: `services.AddScoped<IService, ServiceImpl>()`

### Service Implementation Pattern
```csharp
public class TodoService : ITodoService
{
    private readonly ITodoRepository _repository;
    private List<TodoItem> _todos = new();
    
    public event Action? OnTodosChanged;
    public IReadOnlyList<TodoItem> Todos => _todos.AsReadOnly();
    
    public TodoService(ITodoRepository repository)
    {
        _repository = repository;
    }
    
    private void NotifyStateChanged() => OnTodosChanged?.Invoke();
}
```

### Repository Pattern
- Repositories handle data persistence (LocalStorage)
- Async operations throughout: `Task<T>`, `Task<bool>`, `Task`
- Return success boolean for add/update operations
- Initialize with `InitializeAsync()` called from service

## Common Patterns

### Async/Await
- Always use `async`/`await` for async operations
- Return `Task` for void operations
- Return `Task<T>` for operations with results
- No `.Result` or `.Wait()` - always await

### LINQ & Filtering
- Use LINQ for data filtering and transformation
- Switch expressions for conditional logic
- Method chaining for readable queries
- Example: `filtered.Where(t => !t.IsDone).OrderBy(t => t.Priority)`

### Validation
- Use DataAnnotations: `[Required]`, `[StringLength]`
- Custom `IsValid()` methods on models
- `<DataAnnotationsValidator />` in forms
- `<ValidationMessage For="..." />` for errors

## Bootstrap & Styling
- Bootstrap 5 classes for UI
- Helper methods in `/Helpers/CssHelpers.cs` for dynamic classes
- Scoped CSS per component (`.razor.css` files)
- Icons via Open Iconic font or emoji

## Git Workflow
- Commit messages: Brief, imperative mood ("Add feature", "Fix bug")
- No generated files in git (bin/, obj/ ignored)
