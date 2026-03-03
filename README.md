# BlazorTodo

A fast, local-first todo app built with .NET 10 Blazor Server.

## Tech Stack

- [.NET 10](https://dotnet.microsoft.com/) — Blazor Server
- [Blazored.LocalStorage](https://github.com/Blazored/LocalStorage) — browser-side persistence
- [QRCoder](https://github.com/codebude/QRCoder) — QR code generation

## Getting Started

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download)

```bash
git clone https://github.com/youruser/BlazorTodo.git
cd BlazorTodo
dotnet run
```

Open [http://localhost:5217](http://localhost:5217) in your browser.

## Docker

A pre-built image is available on Docker Hub:

```bash
docker run -p 8080:8080 mylsotol/blazortodo
```

Image: [hub.docker.com/r/mylsotol/blazortodo](https://hub.docker.com/r/mylsotol/blazortodo)

Data is stored entirely in the browser's `localStorage` — no database or server required.
