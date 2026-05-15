# syntax=docker/dockerfile:1.7

# ---- Build stage ------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (layer cache hits when only source — not the csproj — changes).
COPY src/TodoList.csproj .
RUN dotnet restore TodoList.csproj

# Build + publish.
COPY src/ .
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish TodoList.csproj -c ${BUILD_CONFIGURATION} -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----------------------------------------------------------
# Alpine variant — smaller image, faster pull. TLS is terminated at Cloudflare's
# edge (see deploy/docker-stack.yml), so we serve plain HTTP internally.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=false

USER $APP_UID

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "TodoList.dll"]
