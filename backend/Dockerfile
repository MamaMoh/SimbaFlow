# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for restore
COPY SimbaFlow.slnx .
COPY src/SimbaFlow.API/SimbaFlow.API.csproj src/SimbaFlow.API/
COPY src/SimbaFlow.Application/SimbaFlow.Application.csproj src/SimbaFlow.Application/
COPY src/SimbaFlow.Domain/SimbaFlow.Domain.csproj src/SimbaFlow.Domain/
COPY src/SimbaFlow.Infrastructure/SimbaFlow.Infrastructure.csproj src/SimbaFlow.Infrastructure/
COPY src/SimbaFlow.Shared/SimbaFlow.Shared.csproj src/SimbaFlow.Shared/
RUN dotnet restore

# Copy all source and publish
COPY . .
RUN dotnet publish src/SimbaFlow.API -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# Install curl for health checks
RUN apk add --no-cache curl

# Create non-root user
RUN adduser -D -h /app appuser
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Create directories for file storage and logs
RUN mkdir -p /data /app/logs && chown -R appuser:appuser /data /app/logs /app

# Switch to non-root user
USER appuser

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "SimbaFlow.API.dll"]
