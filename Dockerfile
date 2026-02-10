# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies (layer caching optimization)
COPY TodoList.csproj .
RUN dotnet restore TodoList.csproj

# Copy source code and build
COPY . .
RUN dotnet build TodoList.csproj -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish TodoList.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Run as non-root user for security
USER $APP_UID

# Copy published application
COPY --from=publish /app/publish .

# Expose HTTP port (TLS should be handled at reverse proxy level)
EXPOSE 8080

# Set entry point
ENTRYPOINT ["dotnet", "TodoList.dll"]
