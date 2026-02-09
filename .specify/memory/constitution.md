<!--
============================================================================
SYNC IMPACT REPORT - Constitution Update
============================================================================
Version Change: Initial → 1.0.0
Change Type: MAJOR (Initial constitution creation)
Date: 2026-02-06

Principles Added:
  - I. Production-Ready Code (NON-NEGOTIABLE)
  - II. Domain-Driven Design & Clean Architecture (NON-NEGOTIABLE)
  - III. Test-First Development (NON-NEGOTIABLE)
  - IV. Resilience & Observability (NON-NEGOTIABLE)
  - V. Eventual Consistency (NON-NEGOTIABLE)
  - VI. Result Pattern for Error Handling (NON-NEGOTIABLE)
  - VII. PostgreSQL & EF Core Standards (NON-NEGOTIABLE)

Sections Added:
  - Technology Stack Constraints
  - Development Workflow & Quality Gates
  - Governance

Template Consistency Check:
  ✅ .specify/templates/plan-template.md - Constitution Check gate present, aligns with principles
  ✅ .specify/templates/spec-template.md - User scenarios and requirements align with TDD principle
  ✅ .specify/templates/tasks-template.md - Task phases align with test-first and independent delivery
  ⚠ No command templates found - constitution can be referenced when created

Source Material:
  - prerequists/core-principles-dotnet.md (v2.3.0, 2026-02-02)
  - prerequists/tech-specifications.md
  - prerequists/20260205.requirements.backend.md

Follow-up TODOs:
  - None - all placeholders filled with concrete values
============================================================================
-->

# NEMT Accounting System Constitution

## Core Principles

### I. Production-Ready Code (NON-NEGOTIABLE)

**ALL code written MUST be production-ready from the first implementation.**

- Parallel processing for bulk operations (`Task.WhenAll`, controlled concurrency)
- Resilience patterns configured (retry, circuit breaker, timeout, bulkhead)
- Proper cancellation support (`CancellationToken` propagation)
- Thread-safe concurrent operations
- Resource disposal (`using`, `IDisposable`, `ConfigureAwait(false)`)
- Performance-optimized queries (projections, `AsNoTracking()`, indexed columns)
- Structured logging with correlation IDs
- Comprehensive error handling via Result pattern

**Rationale**: Systems are built for production from day one. There is no "development mode" or "prototype phase." Every line of code must be deployment-ready, observable, resilient, and performant. Iterative improvements add NEW features, not fix fundamental architectural or performance issues that should have been addressed initially.

**Zero tolerance for**: Placeholder implementations (`// TODO: Add retry logic later`), sequential processing where parallel is required, unhandled edge cases, or "this works for now" code requiring rework.

### II. Domain-Driven Design & Clean Architecture (NON-NEGOTIABLE)

**Architecture Layers**:

- **Domain Layer**: Single source of truth for ALL business rules; ZERO infrastructure dependencies (no EF Core, ASP.NET, HTTP, messaging, cloud SDKs); private setters only; encapsulate state; domain exceptions only for infrastructure failures
- **Application Layer**: Commands, Queries, Handlers, Validators, DTOs; NO MediatR—use explicit handler classes with constructor injection; validation via FluentValidation
- **Infrastructure Layer**: DbContext, EF Core Configurations, Repositories, Messaging, Outbox, External Services; domain entities SEPARATE from persistence entities (mandatory); Outbox pattern for integration events
- **Presentation/API Layer**: Controllers, Middleware, Request/Response models; global exception handling MUST be first in middleware pipeline; NO try-catch in controllers; all errors return RFC 9457 Problem Details

**Aggregate Rules**:
1. Only Aggregate Root can modify internal state
2. One aggregate = one transaction
3. Reference other aggregates by ID only (not object references)
4. Keep aggregates small for better concurrency
5. Cross-aggregate changes use domain events (eventual consistency)

**Bounded Context = Microservice**:
- Each bounded context maps to one deployable microservice
- No shared domain entities across contexts
- Cross-context interaction via contracts/events only
- No shared databases between services
- Each context has its own ubiquitous language

**Rationale**: Clean Architecture ensures maintainability, testability, and independent deployability. DDD aligns technical implementation with business domain. This separation of concerns prevents coupling that would impede scaling and evolution.

### III. Test-First Development (NON-NEGOTIABLE)

**Mandatory TDD Cycle**:
1. Write tests based on acceptance criteria
2. User/stakeholder approval of test scenarios
3. Tests MUST fail (red phase)
4. Implement code to pass tests (green phase)
5. Refactor while maintaining green tests

**CQRS Testing**:
- **Commands**: Test that aggregates enforce invariants and raise correct domain events
- **Queries**: Test projections return correct DTOs with optimized queries

**Contract Testing**: Required for all API endpoints, message schemas, and cross-service communication

**Rationale**: Tests define the contract before implementation, ensuring clarity of requirements and preventing regression. Test-first development catches design issues early and builds confidence in refactoring.

### IV. Resilience & Observability (NON-NEGOTIABLE)

**Seven Pillars of Distributed System Resilience**:

1. **Service Isolation**: Each service fails independently; database-per-service; no sync chains >2 hops
2. **Resilience Patterns**: Retry with exponential backoff, Circuit breaker, Timeout, Bulkhead, Fallback (configured via Polly)
3. **Data Consistency**: Eventual consistency via Saga pattern and events; NO distributed transactions (2PC)
4. **Idempotent Operations**: Every write operation must be safely repeatable
5. **Reliable Messaging**: At-least-once delivery via Outbox pattern; dead-letter queues
6. **Observability**: Structured logs (Serilog), Metrics (Prometheus), Traces (OpenTelemetry, Jaeger)
7. **Graceful Degradation**: Partial functionality over complete outage

**Standard Resilience Configuration**:
- **HTTP APIs**: 3 retries, exponential backoff + jitter, circuit breaker (5 failures/30s), 10s timeout
- **Database**: 5 retries, exponential backoff, 30s timeout
- **Message Broker**: 5 retries, exponential backoff, circuit breaker (10 failures/60s), 30s timeout
- **Cache**: 2 retries, fixed 100ms delay, circuit breaker (3 failures/10s), 2s timeout

**Observability Requirements**:
- Correlation IDs on all operations
- OpenTelemetry traces for distributed transactions
- High-performance logging via `[LoggerMessage]` source generators
- Event ID ranges: 1-999 (Info), 1000-1999 (Warning), 2000-2999 (Error), 3000+ (Critical)

**Rationale**: Distributed systems WILL fail. Resilience patterns and observability are not optional—they are fundamental to operating reliable services at scale. Without them, debugging production issues becomes impossible and cascading failures inevitable.

### V. Eventual Consistency (NON-NEGOTIABLE)

**Distributed Transactions (2PC) are FORBIDDEN**.

**Use Instead**:
- **Saga (Choreography)**: Simple workflows, few steps
- **Saga (Orchestration)**: Complex workflows, many compensations
- **Outbox Pattern**: Reliable event publishing (MANDATORY for integration events)
- **Event Sourcing**: Audit requirements, temporal queries

**Cross-Aggregate Consistency**:
- Changes spanning multiple aggregates use domain events
- Events processed in same transaction (internal) or via Outbox (external)
- All consumers MUST be idempotent (assume at-least-once delivery)

**Rationale**: Distributed transactions do not scale and violate service autonomy. Eventual consistency via events enables independent service evolution while maintaining system-wide correctness through compensation and reconciliation.

### VI. Result Pattern for Error Handling (NON-NEGOTIABLE)

**Use Result<T> for**:
- Business rule violations
- Validation errors
- Resource not found
- Conflict states
- Expected failures

**Use Exceptions for**:
- Database connection failures
- Network timeouts
- Out of memory
- Unexpected infrastructure failures

**Error Type → HTTP Status Mapping**:
- Validation → 400 Bad Request
- NotFound → 404 Not Found
- Conflict → 409 Conflict
- Failure → 500 Internal Server Error

**Implementation**:
- Domain methods return `Result` or `Result<T>`
- Application layer handlers return `Result<T>`
- Controllers map Result errors to HTTP status codes
- Global exception middleware handles only infrastructure exceptions

**Rationale**: The Result pattern makes error handling explicit in the type system, preventing unhandled exceptions and improving API contracts. Exceptions are reserved for truly exceptional circumstances, not business logic flow control.

### VII. PostgreSQL & EF Core Standards (NON-NEGOTIABLE)

**Entity Separation (MANDATORY)**:
- **Domain Entities**: Enforce business rules, zero infrastructure dependencies, private setters, rich behavior, value objects (Domain layer)
- **Persistence Entities**: Store/retrieve data, EF Core dependencies, public setters for EF Core, simple POCOs with primitives (Infrastructure layer)
- Mappers translate between domain and persistence models

**PostgreSQL Naming Conventions**:
- Table names: `lowercase_snake_case`, plural (e.g., `orders`, `order_lines`)
- Column names: `lowercase_snake_case` (e.g., `customer_id`, `created_at`)
- Primary keys: Always `id`
- Foreign keys: `<table_singular>_id` (e.g., `customer_id`)
- Indexes: `ix_<table>_<columns>` (e.g., `ix_orders_customer_id`)
- Unique constraints: `uq_<table>_<columns>` (e.g., `uq_users_email`)
- Check constraints: `ck_<table>_<condition>` (e.g., `ck_orders_total_positive`)

**Query Optimization (MANDATORY)**:
- Use `AsNoTracking()` for all read queries
- Use projections (`Select`) instead of loading full entities
- Implement pagination for list queries
- Index all columns in WHERE, ORDER BY, and JOIN clauses

**Migration Rules**:
- Idempotent scripts mandatory for production
- Large data migrations use background jobs, NOT EF migrations
- All migrations must be reversible (implement both `Up()` and `Down()`)
- Each service has its own schema and migration history
- NO cross-service foreign key constraints
- Deploy migrations FIRST, then application

**Rationale**: Separation of domain and persistence entities preserves domain purity while accommodating ORM requirements. Consistent PostgreSQL conventions improve maintainability across services. Query optimization and proper migrations prevent performance degradation and deployment failures.

## Technology Stack Constraints

**Mandatory Stack** (NON-NEGOTIABLE):

| Component      | Technology       | Version    | Justification                                    |
|----------------|------------------|------------|--------------------------------------------------|
| Runtime        | .NET             | 10+        | Native AOT, performance, modern language features|
| Database       | PostgreSQL       | 17+        | JSONB support, reliability, ACID compliance      |
| ORM            | EF Core          | 10+        | Mature .NET ORM, migration tooling               |
| Validation     | FluentValidation | Latest     | Expressive validation, separates from domain     |
| Resilience     | Polly            | Latest     | Industry-standard resilience policies            |
| Logging        | Serilog          | Latest     | Structured logging, rich sinks                   |
| Tracing        | OpenTelemetry    | Latest     | Distributed tracing standard                     |
| Messaging      | Apache Kafka     | Latest     | High-throughput event streaming                  |
| Caching        | Redis            | Latest     | Fast in-memory cache, pub/sub                    |
| API Gateway    | YARP             | Latest     | .NET-native reverse proxy                        |
| Authentication | Keycloak         | Latest     | OAuth2/OIDC, centralized identity                |
| Scheduling     | Quartz           | Latest     | Robust job scheduling                            |

**Performance Standards** (NON-NEGOTIABLE):
- API p50 latency: <50ms (target), <100ms (critical threshold)
- API p95 latency: <200ms (target), <500ms (critical threshold)

**Security Standards** (NON-NEGOTIABLE):
- Authentication via JWT/Keycloak
- Authorization via policy-based claims
- Input validation via FluentValidation at API boundary
- Never store secrets in code/config—use Key Vault/Secrets Manager
- Mask sensitive data in logs

**Rationale**: Standardizing the technology stack reduces cognitive load, enables code reuse, and ensures consistent operational characteristics across services. Deviations fragment expertise and increase maintenance burden.

## Development Workflow & Quality Gates

**Code Quality Standards**:
- `.editorconfig` at repository root only (`root = true`)
- `stylecop.json` for analyzer configuration
- `Directory.Build.props` for common build properties
- `Directory.Packages.props` for centralized package versions
- XML documentation REQUIRED on ALL members (public, private, protected, internal)
- NO `#pragma warning disable` inline—ALL suppressions in `GlobalSuppressions.cs` with justification

**Architecture Guardrails** (Hard Rules):
- [ ] **Domain Layer**: No reference to Infrastructure or Presentation; No EF Core, HTTP, or cloud SDK dependencies; No public setters
- [ ] **Infrastructure Layer**: No direct DbContext usage outside Infrastructure; No IQueryable exposure from repositories; Outbox pattern mandatory for integration events
- [ ] **Application Layer**: No MediatR; Handlers return Result types; NO exceptions for business rules
- [ ] **Presentation Layer**: No try-catch in controllers; Controllers map Result to HTTP status; Exception middleware first in pipeline
- [ ] **Cross-Service Rules**: No shared database; No direct assembly references; No distributed transactions; Cross-context changes via events only

**Health Checks** (MANDATORY):
- `/health/live` — Liveness probe
- `/health/ready` — Readiness probe (checks dependencies)
- `/health/startup` — Startup probe

**Dockerfile Requirements**:
- Multi-stage builds
- Non-root user
- Specific base image tags (not `latest`)
- `.dockerignore` file
- Health check configured

**Rationale**: Quality gates enforce constitutional principles automatically, preventing drift. Health checks enable Kubernetes to manage service lifecycle correctly. Guardrails protect architectural integrity across team growth.

## Governance

**Constitutional Authority**: This constitution supersedes all other practices, coding guidelines, and team conventions. When conflicts arise, the constitution takes precedence.

**Amendment Process**:
1. Propose amendment with rationale and impact analysis
2. Document affects on existing code and templates
3. Requires approval from technical leadership and affected stakeholders
4. Increment version according to semantic versioning:
   - **MAJOR**: Backward incompatible governance/principle removals or redefinitions
   - **MINOR**: New principle/section added or materially expanded guidance
   - **PATCH**: Clarifications, wording, typo fixes, non-semantic refinements
5. Update all dependent artifacts (`.specify/templates/*`)
6. Migration plan for existing code if breaking changes introduced

**Compliance Verification**:
- All Pull Requests must verify constitutional compliance before merge
- `.specify/templates/plan-template.md` includes "Constitution Check" gate
- Complexity that violates principles must be explicitly justified and approved
- Periodic constitution reviews (quarterly recommended) to ensure alignment with evolving needs

**Runtime Development Guidance**:
- Use `prerequists/core-principles-dotnet.md` for detailed implementation patterns
- Use `.specify/templates/*` for standardized workflow artifacts
- Constitution defines "what" and "why"; guidance documents define "how"

**Rationale**: The constitution provides stable, high-level governance that evolves slowly and deliberately. Rapid changes to foundational principles create chaos. Amendments must be justified, documented, and propagated systematically to maintain system-wide coherence.

**Version**: 1.0.0 | **Ratified**: 2026-02-06 | **Last Amended**: 2026-02-06
