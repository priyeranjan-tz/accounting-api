# T150: Multi-stage Dockerfile with .NET 9 Native AOT compilation
# Produces a minimal, self-contained binary with fast startup and low memory footprint

# ============================================================
# Build Stage: Compile and publish with Native AOT
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy solution and project files for dependency restoration
COPY ["Accounting.sln", "./"]
COPY ["src/Accounting.API/Accounting.API.csproj", "src/Accounting.API/"]
COPY ["src/Accounting.Application/Accounting.Application.csproj", "src/Accounting.Application/"]
COPY ["src/Accounting.Domain/Accounting.Domain.csproj", "src/Accounting.Domain/"]
COPY ["src/Accounting.Infrastructure/Accounting.Infrastructure.csproj", "src/Accounting.Infrastructure/"]

# Restore dependencies (cached layer unless .csproj changes)
RUN dotnet restore "src/Accounting.API/Accounting.API.csproj" -r linux-musl-x64

# Copy source code
COPY ["src/", "src/"]

# Build and publish with Native AOT
# - PublishAot=true: Enable Native AOT compilation
# - Configuration=Release: Optimized build
# - Runtime=linux-musl-x64: Alpine Linux runtime
# - SelfContained=true: Include all dependencies
# - PublishTrimmed=true: Remove unused code
# - PublishSingleFile=false: AOT already produces single binary
RUN dotnet publish "src/Accounting.API/Accounting.API.csproj" \
    -c Release \
    -r linux-musl-x64 \
    -p:PublishAot=true \
    -p:SelfContained=true \
    -p:PublishTrimmed=true \
    --no-restore \
    -o /app/publish

# ============================================================
# Runtime Stage: Minimal container with AOT binary
# ============================================================
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine AS runtime
WORKDIR /app

# Create non-root user for security
RUN addgroup -g 1001 appgroup && \
    adduser -D -u 1001 -G appgroup appuser && \
    chown -R appuser:appgroup /app

# Copy published AOT binary from build stage
COPY --from=build --chown=appuser:appgroup /app/publish .

# Switch to non-root user
USER appuser

# Expose HTTP port (HTTPS handled by reverse proxy)
EXPOSE 8080

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Set environment variables for production
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    COMPlus_EnableDiagnostics=0

# Run the AOT-compiled binary
ENTRYPOINT ["./Accounting.API"]
