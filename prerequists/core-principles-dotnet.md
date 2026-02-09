# Core Principles: AI Agent Context Guide

> **Purpose**: Governance document for building production-grade distributed systems using .NET, DDD, Microservices, and EF Core with PostgreSQL.

## Production-Grade Code Mandate (NON-NEGOTIABLE)

**ALL code written MUST be production-ready from the first implementation.**

### ✅ Production-Grade Requirements

- Parallel processing for bulk operations (`Task.WhenAll`, controlled concurrency)
- Resilience patterns configured (retry, circuit breaker, timeout, bulkhead)
- Proper cancellation support (`CancellationToken` propagation)
- Thread-safe concurrent operations
- Resource disposal (`using`, `IDisposable`, `ConfigureAwait(false)`)
- Performance-optimized queries (projections, `AsNoTracking()`, indexed columns)
- Structured logging with correlation IDs
- Comprehensive error handling (Result pattern, not exceptions for business rules)

### ❌ Forbidden Anti-Patterns

| ❌ NEVER DO THIS                                             | ✅ ALWAYS DO THIS                                                                                        | Reason                             |
| ----------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- | ---------------------------------- |
| `foreach (var item in items) { await ProcessAsync(item); }` | `var tasks = items.Select(ProcessAsync);`<br>`await Task.WhenAll(tasks);`                               | Sequential = 10x slower            |
| `// In production, this would use Task.WhenAll`             | Implement Task.WhenAll immediately                                                                      | No placeholders allowed            |
| `// TODO: Add retry logic later`                            | Configure Polly retry policy now                                                                        | Resilience is mandatory            |
| `await httpClient.GetAsync(url);`                           | `var policy = Policy.TimeoutAsync(10);`<br>`await policy.ExecuteAsync(() => httpClient.GetAsync(url));` | All external calls need resilience |
| `public DateTime CreatedAt { get; set; }` (domain)          | `public DateTime CreatedAt { get; private init; }`                                                      | Encapsulation mandatory            |
| `_dbContext.Orders.ToList()`                                | `_dbContext.Orders.AsNoTracking().Select(...).ToList()`                                                 | Avoid loading full entities        |
| `catch (Exception) { }`                                     | `catch (SpecificException ex) { _logger.LogError(...); throw; }`                                        | Never swallow exceptions           |

**Rationale**: We build systems for production. There is no "development mode" or "prototype phase." Every line of code should be deployment-ready, observable, resilient, and performant. Iterative improvements are for adding NEW features, not for fixing fundamental architectural or performance issues that should have been addressed initially.

**Zero tolerance for**: "This works for now" implementations that require rework later.


## Follow Seven Pillars of Distributed System Resilience

| Pillar                | Core Concept                                                                        |
| --------------------- | ----------------------------------------------------------------------------------- |
| Service Isolation     | Each service fails independently; database-per-service; no sync chains >2 hops      |
| Resilience Patterns   | Retry with backoff, Circuit breaker, Timeout, Bulkhead, Fallback                    |
| Data Consistency      | Eventual consistency via Saga pattern and events; NO distributed transactions (2PC) |
| Idempotent Operations | Every write operation must be safely repeatable                                     |
| Reliable Messaging    | At-least-once delivery via Outbox pattern; dead-letter queues                       |
| Observability         | Logs (Serilog), Metrics (Prometheus), Traces (Jaeger/OpenTelemetry)                 |
| Graceful Degradation  | Partial functionality over complete outage                                          |

## Architecture Layers

### Domain Layer (NON-NEGOTIABLE)

**Contains**: Aggregates, Entities, Value Objects, Domain Events, Domain Services, Repository Abstractions, Errors

**Rules**:
- Single source of truth for ALL business rules
- ZERO infrastructure dependencies (no EF Core, ASP.NET Core, HTTP, messaging, cloud SDKs)
- Private setters only; encapsulate state
- Domain Exceptions only for unexpected infrastructure failures

### Application Layer

**Contains**: Commands, Queries, Handlers, Validators, DTOs

**Rules**:
- NO MediatR — use explicit handler classes with constructor injection
- Validation via FluentValidation
- Explicit dependencies visible in constructors

### Infrastructure Layer

**Contains**: DbContext, EF Core Configurations, Repositories, Messaging, Outbox, External Services

**Rules**:
- Domain entities SEPARATE from Persistence entities (mandatory)
- Mappers translate between domain and persistence models
- Outbox pattern mandatory for integration events
- No IQueryable exposure from repositories

### Presentation/API Layer

**Contains**: Controllers, Middleware, Request/Response models

**Rules**:
- `app.UseGlobalExceptionHandling()` MUST be first in middleware pipeline
- NO try-catch blocks in controllers
- All errors return RFC 9457 Problem Details format

## Result Pattern (NON-NEGOTIABLE)

**Use Result for**: Business rule violations, Validation errors, Resource not found, Conflict states, Expected failures

**Use Exception for**: Database connection failures, Network timeouts, Out of memory, Unexpected infrastructure failures

**Error Types → HTTP Mapping**:
| ErrorType  | HTTP Status               |
| ---------- | ------------------------- |
| Validation | 400 Bad Request           |
| NotFound   | 404 Not Found             |
| Conflict   | 409 Conflict              |
| Failure    | 500 Internal Server Error |

## CQRS Pattern (NON-NEGOTIABLE)

- **Commands**: Modify state through aggregates; return `Result` or `Result<T>`
- **Queries**: Return read-optimized DTOs/projections; use `AsNoTracking()`

## Aggregate Rules (NON-NEGOTIABLE)

1. Only Aggregate Root can modify internal state
2. One aggregate = one transaction
3. Reference other aggregates by ID only (not object references)
4. Keep aggregates small for better concurrency
5. Cross-aggregate changes use domain events (eventual consistency)

## Bounded Context = Microservice (NON-NEGOTIABLE)

- Each bounded context maps to one deployable microservice
- No shared domain entities across contexts
- Cross-context interaction via contracts/events only
- No shared databases between services
- Each context has its own ubiquitous language

## EF Core with PostgreSQL Standards (NON-NEGOTIABLE)

### Entity Separation (NON-NEGOTIABLE)

| Concern       | Domain Entity                | Persistence Entity         |
| ------------- | ---------------------------- | -------------------------- |
| Purpose       | Enforce business rules       | Store/retrieve data        |
| Location      | Domain layer                 | Infrastructure layer       |
| Dependencies  | Zero infrastructure deps     | EF Core, database concerns |
| Encapsulation | Private setters, invariants  | Public setters for EF Core |
| Complexity    | Rich behavior, value objects | Simple POCOs, primitives   |

### PostgreSQL Naming Conventions (NON-NEGOTIABLE)

| Element            | Format                       | Example                     |
| ------------------ | ---------------------------- | --------------------------- |
| Table names        | lowercase_snake_case, plural | `orders`, `order_lines`     |
| Column names       | lowercase_snake_case         | `customer_id`, `created_at` |
| Primary keys       | Always `id`                  | `id`                        |
| Foreign keys       | `<table_singular>_id`        | `customer_id`               |
| Indexes            | `ix_<table>_<columns>`       | `ix_orders_customer_id`     |
| Unique constraints | `uq_<table>_<columns>`       | `uq_users_email`            |
| Check constraints  | `ck_<table>_<condition>`     | `ck_orders_total_positive`  |

### Index Strategy Questions (NON-NEGOTIABLE)

1. What columns appear in WHERE clauses most frequently?
2. What columns are used for sorting (ORDER BY)?
3. What columns are used in JOINs?
4. What is the read/write ratio?
5. How large will the table grow?

### Migration Rules (NON-NEGOTIABLE)

- Idempotent scripts mandatory for production
- Large data migrations use background jobs, NOT EF migrations
- All migrations must be reversible (implement both `Up()` and `Down()`)
- Each service has its own schema and migration history
- NO cross-service foreign key constraints
- Deploy migrations FIRST, then application

## Data Consistency Patterns (NON-NEGOTIABLE)

| Pattern              | Use Case                              |
| -------------------- | ------------------------------------- |
| Saga (Choreography)  | Simple workflows, few steps           |
| Saga (Orchestration) | Complex workflows, many steps         |
| Outbox Pattern       | Reliable event publishing (MANDATORY) |
| Event Sourcing       | Audit requirements, temporal queries  |

**Distributed Transactions (2PC) are FORBIDDEN** — use Saga + eventual consistency instead.

## Resilience Configuration Standards (NON-NEGOTIABLE)

| Scenario       | Retry Count | Retry Delay          | Circuit Breaker   | Timeout |
| -------------- | ----------- | -------------------- | ----------------- | ------- |
| HTTP APIs      | 3           | Exponential + jitter | 5 failures / 30s  | 10s     |
| Database       | 5           | Exponential          | N/A               | 30s     |
| Message Broker | 5           | Exponential          | 10 failures / 60s | 30s     |
| Cache          | 2           | Fixed 100ms          | 3 failures / 10s  | 2s      |

## Event-Driven Architecture

### Domain Events (Internal) (NON-NEGOTIABLE)
- Raised within aggregate operations
- Processed in same transaction or via Outbox

### Integration Events (External) (NON-NEGOTIABLE)
- Published via Outbox pattern (mandatory)
- Must be versioned
- New fields should have defaults; never remove fields

### Messaging Guarantees (NON-NEGOTIABLE)
- Assume at-least-once delivery
- All consumers must be idempotent
- Implement dead-letter queues for failed messages

## Kubernetes & Container Standards

### Health Checks (MANDATORY) (NON-NEGOTIABLE)
- `/health/live` — Liveness probe
- `/health/ready` — Readiness probe (checks dependencies)
- `/health/startup` — Startup probe

### Dockerfile Requirements (NON-NEGOTIABLE)
- Multi-stage builds
- Non-root user
- Specific base image tags (not `latest`)
- .dockerignore file
- Health check configured

## Security Standards (NON-NEGOTIABLE)

- Authentication via JWT/Keycloak
- Authorization via policy-based claims
- Input validation via FluentValidation at API boundary
- Never store secrets in code/config — use Key Vault/Secrets Manager
- Mask sensitive data in logs

## Performance Standards (NON-NEGOTIABLE)

| Metric          | Target  | Critical |
| --------------- | ------- | -------- |
| API p50 latency | < 50ms  | < 100ms  |
| API p95 latency | < 200ms | < 500ms  |

### Query Optimization (NON-NEGOTIABLE)
- Use `AsNoTracking()` for read queries
- Use projections (Select) instead of loading full entities
- Implement pagination for list queries
- Cache frequently accessed data

### High-Performance Logging (NON-NEGOTIABLE)

**Approach**:
- Use `[LoggerMessage]` attribute with source generators (.NET 6+) — preferred
- Use `LoggerMessage.Define` for .NET 5 and earlier
- Use standard extensions (`LogInformation`, `LogDebug`) only for low-frequency logging

**Rules**:
- Use strongly-typed parameters to avoid boxing
- Use named placeholders with PascalCase (e.g., `{OrderId}`, `{CustomerId}`)
- Assign unique event IDs with semantic names
- Guard expensive operations with `ILogger.IsEnabled(LogLevel)`
- Pass exceptions as last parameter, never in message template
- Centralize logger extensions in dedicated classes

**Event ID Ranges**:
| Range     | Purpose                        |
| --------- | ------------------------------ |
| 1-999     | Information (normal operation) |
| 1000-1999 | Warning (degraded operation)   |
| 2000-2999 | Error (recoverable failures)   |
| 3000+     | Critical (system failures)     |

## Code Quality Standards (NON-NEGOTIABLE)

### Configuration Files (NON-NEGOTIABLE)
- `.editorconfig` at repository root only (root = true)
- `stylecop.json` for analyzer configuration
- `Directory.Build.props` for common build properties
- `Directory.Packages.props` for centralized package versions

### Warning Suppression (NON-NEGOTIABLE)
- NO `#pragma warning disable` inline
- ALL suppressions in `GlobalSuppressions.cs` with justification

### XML Documentation (NON-NEGOTIABLE)
- Required on ALL members (public, private, protected, internal)
- Include `<summary>`, `<param>`, `<returns>`, `<remarks>`, `<exception>`

### Class Member Ordering (NON-NEGOTIABLE)
1. Constants
2. Static fields
3. Instance fields (by access modifier: public → private)
4. Constructors
5. Finalizers
6. Properties
7. Events
8. Methods (public static → public instance → private)
9. Nested types

### Naming Conventions
| Element            | Convention    | Example            |
| ------------------ | ------------- | ------------------ |
| Interfaces         | Prefix with I | `IOrderRepository` |
| Classes            | PascalCase    | `OrderService`     |
| Methods/Properties | PascalCase    | `PlaceOrder()`     |
| Local variables    | camelCase     | `orderTotal`       |
| Private fields     | _camelCase    | `_orderRepository` |
| Constants          | PascalCase    | `MaxRetryCount`    |

## AI Agent Guidelines (NON-NEGOTIABLE)

### Questions for New Aggregates/Entities
1. What is the business purpose?
2. What invariants must be enforced?
3. What are the possible state transitions?
4. What domain events should be raised?
5. What value objects should be extracted?

### Questions for New Database Tables
1. What columns will be frequently queried?
2. What columns used for sorting?
3. What columns used in JOINs?
4. Index requirements (single/composite, column order, partial/filtered)?
5. Expected table size and growth rate?
6. Read/write ratio?

### Questions for New Features
1. Is this a command (modifies state) or query (reads data)?
2. What aggregates are involved?
3. What validation rules apply?
4. What error scenarios need handling?
5. What events should be published?
6. What idempotency requirements exist?

### Questions for External Integrations
1. What is the SLA of the external service?
2. What happens if the service is unavailable?
3. What retry/circuit breaker policies should apply?
4. Is caching appropriate?
5. What timeout values should be used?

### Code Generation Requirements
1. Follow all conventions in this document
2. Use Result pattern for operations that can fail
3. Include XML documentation on all public members
4. Separate domain from persistence entities
5. Use snake_case for PostgreSQL naming
6. Include appropriate indexes based on query patterns
7. Implement idempotency for event handlers
8. Include resilience policies for external calls
9. Add health checks for dependencies
10. Include structured logging with correlation IDs

## Architecture Guardrails (Hard Rules) (NON-NEGOTIABLE)

### Domain Layer (NON-NEGOTIABLE)
- [ ] No reference to Infrastructure or Presentation
- [ ] No EF Core, HTTP, or cloud SDK dependencies
- [ ] No public setters on domain entities
- [ ] No repositories for non-aggregate entities

### Infrastructure Layer (NON-NEGOTIABLE)
- [ ] No direct DbContext usage outside Infrastructure
- [ ] No IQueryable exposure from repositories
- [ ] Outbox pattern mandatory for integration events

### Application Layer (NON-NEGOTIABLE)
- [ ] No MediatR — explicit handlers with constructor injection
- [ ] Handlers return Result types
- [ ] Domain methods return Result (no exceptions for business rules)

### Presentation Layer (NON-NEGOTIABLE)
- [ ] No try-catch blocks in controllers
- [ ] Controllers map Result errors to HTTP status codes
- [ ] Exception handling middleware first in pipeline

### Cross-Service Rules (NON-NEGOTIABLE)
- [ ] No shared database across services
- [ ] No direct assembly references between services
- [ ] No distributed transactions across services
- [ ] Cross-context changes via events or ACL

## Technology Stack (NON-NEGOTIABLE)

| Component      | Technology       |
| -------------- | ---------------- |
| Runtime        | .NET 10          |
| Database       | PostgreSQL 17+   |
| ORM            | EF Core 10       |
| Validation     | FluentValidation |
| Resilience     | Polly            |
| Logging        | Serilog          |
| Tracing        | OpenTelemetry    |
| Messaging      | Apache Kafka     |
| Caching        | Redis            |
| API Gateway    | YARP             |
| Authentication | Keycloak         |
| Scheduling     | Quartz           |

**Version**: 2.3.0  
**Last Updated**: 2026-02-02
