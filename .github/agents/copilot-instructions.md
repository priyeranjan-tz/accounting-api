# Accounting Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-02-06

## Active Technologies
- .NET 9.0 (C#) - Latest stable release (001-accounting-ledger)
- PostgreSQL 17 (Alpine Docker image), snake_case naming, NUMERIC(19,4) for money, xid for concurrency tokens, PostgreSQL triggers for immutability enforcement (001-accounting-ledger)

- .NET 10 (C#) with Native AOT for optimized cold starts and memory efficiency + ASP.NET Core 10 (Minimal APIs), EF Core 10, FluentValidation, Polly (resilience), Serilog (structured logging), OpenTelemetry (observability), Quartz (scheduled invoicing) (001-accounting-ledger)

## Project Structure

```text
backend/
frontend/
tests/
```

## Commands

# Add commands for .NET 10 (C#) with Native AOT for optimized cold starts and memory efficiency

## Code Style

.NET 10 (C#) with Native AOT for optimized cold starts and memory efficiency: Follow standard conventions

## Recent Changes
- 001-accounting-ledger: Added .NET 9.0 (C#) - Latest stable release

- 001-accounting-ledger: Added .NET 10 (C#) with Native AOT for optimized cold starts and memory efficiency + ASP.NET Core 10 (Minimal APIs), EF Core 10, FluentValidation, Polly (resilience), Serilog (structured logging), OpenTelemetry (observability), Quartz (scheduled invoicing)

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
