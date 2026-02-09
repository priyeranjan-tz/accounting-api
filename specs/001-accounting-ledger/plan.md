# Implementation Plan: Dual-Entry Accounting & Invoicing Service

**Branch**: `001-accounting-ledger` | **Date**: 2026-02-07 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-accounting-ledger/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Build a production-ready double-entry accounting ledger system for NEMT ride billing with PostgreSQL persistence, enforcing immutability, idempotency, and 100% tenant isolation. System serves as the **financial system of record** for all billable ride services, recording ride charges and payments using double-entry accounting (debits = credits guaranteed), generating invoices with full traceability to ledger entries, and providing account statements.

**Business Value**: Establishes accounting correctness foundation, enables consolidated billing at multiple frequencies (per-ride, daily, weekly, monthly), provides audit-ready financial records with complete immutability, and prepares platform for future fleet-wise revenue reporting.

**Technical Approach**: .NET 9.0 + ASP.NET Core minimal APIs, Entity Framework Core 9.0 with PostgreSQL 17, Clean Architecture (Domain → Application → Infrastructure → API), Result pattern for errors, structured logging with Serilog, OpenTelemetry for observability, Polly for resilience. Strong consistency via ACID transactions (justified for financial accuracy), append-only ledger enforced by database triggers, idempotency via unique constraints, tenant isolation via EF Core global query filters.

**Key Success Criteria**: 100% ledger accuracy (debits=credits), <100ms ledger append (p95), <2s invoice generation, zero duplicate charges, zero tenant leakage, cent-level precision ($0.01), complete traceability (ride→ledger→invoice→balance).

**Specification Alignment**: This plan implements the complete feature specification from [spec.md](./spec.md) which defines:
- 5 user stories with 33 acceptance scenarios (ledger operations, account management, invoicing, billing frequencies, statements)
- 31 functional requirements (FR-001 to FR-031) organized across 7 capability areas
- 12 non-functional requirements (NFR-001 to NFR-012) for performance, scalability, observability, resilience
- 10 success criteria (SC-001 to SC-010) measuring accuracy, performance, traceability, security
- Assumptions (7 technical, 6 business, 5 data) defining scope boundaries
- Future extensions deferred to Phase 2-4 (tax handling, adjustments, multi-currency, ERP integration, reporting)

## Technical Context

**Language/Version**: .NET 9.0 (C#) - Latest stable release  
**Primary Dependencies**: 
- ASP.NET Core 9.0 (Minimal APIs)
- Entity Framework Core 9.0
- Npgsql.EntityFrameworkCore.PostgreSQL 9.x
- FluentValidation 11.12.0
- Polly 8.x + Microsoft.Extensions.Resilience 9.x (retry, circuit breaker, timeout)
- Serilog.AspNetCore 8.x (structured logging)
- OpenTelemetry 1.x (distributed tracing + metrics)
- xUnit 2.9 + FluentAssertions 6.x + Testcontainers.PostgreSql 3.x

**Storage**: PostgreSQL 17 (Alpine Docker image), snake_case naming, NUMERIC(19,4) for money, xid for concurrency tokens, PostgreSQL triggers for immutability enforcement

**Testing**: TDD with xUnit, contract tests (WebApplicationFactory), integration tests (Testcontainers for real PostgreSQL), unit tests for domain logic

**Target Platform**: Linux containers, horizontally scalable (stateless API), Docker Compose for local development

**Project Type**: Web API (microservice) - Clean Architecture with 4 layers (Domain, Application, Infrastructure, API) + separate test projects

**Performance Goals**: 
- Ledger append operations: <100ms p95 latency
- Invoice generation: <2 seconds for 100 line items
- Balance calculation: <50ms (spec implies real-time requirement)

**Constraints**: 
- Strong consistency for writes (PostgreSQL ACID transactions)
- Append-only ledger (immutable via PostgreSQL triggers)
- Idempotency enforcement (unique constraint on account_id + source_reference_id)
- Zero tenant data leakage (multi-tenant filtering at DbContext level)
- 100% accuracy (debits = credits validated before SaveChanges)

**Scale/Scope**: 
- 5 user stories (ledger, accounts, invoices, frequencies, statements)
- ~10 entities (Account, LedgerEntry, Invoice, InvoiceLine, etc.)
- 3 API endpoint groups (ledger, accounts, invoices)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Reference**: See `.specify/memory/constitution.md` for governing principles.

**I. Production-Ready Code** ✅ PASS
- Parallel processing: Not required for ledger (single-transaction ACID guarantees)
- Resilience patterns: Polly configured for HTTP clients, database retry logic via EF Core
- Cancellation support: All async methods accept CancellationToken
- Thread-safe operations: Repository uses DbContext scoped per request
- Resource disposal: using statements, IAsyncDisposable implemented
- Performance-optimized queries: AsNoTracking() for reads, projections for DTOs, indexed columns (account_id, tenant_id, created_at)
- Structured logging: Serilog with correlation IDs (TenantId, TransactionId, RideId)
- Error handling: Result pattern for business rules, exceptions only for infrastructure failures

**II. Domain-Driven Design & Clean Architecture** ✅ PASS
- Domain Layer: LedgerEntry, Account entities with private setters, factory methods, domain invariants (debits = credits)
- Application Layer: Commands (RecordRideChargeCommand, RecordPaymentCommand), Queries (GetAccountBalanceQuery), Handlers with FluentValidation, NO MediatR
- Infrastructure Layer: Repository pattern, domain entities SEPARATE from persistence entities (LedgerEntryEntity vs LedgerEntry), EF Core configurations
- API Layer: Minimal APIs, GlobalExceptionHandler middleware FIRST, RFC 9457 Problem Details
- Aggregates: Account is root, LedgerEntry belongs to Account, references by ID only
- Bounded Context: Accounting context = single microservice

**III. Test-First Development** ✅ PASS
- Tests written FIRST before implementation (constitutional requirement)
- 33 test methods across 4 test files (contract + integration tests)
- Acceptance scenarios from spec converted to xUnit tests
- Tests define contract: RED phase (tests fail) → GREEN phase (implementation) → REFACTOR
- Contract testing: WebApplicationFactory validates API endpoints match ledger-api.yaml specification

**IV. Resilience & Observability** ✅ PASS
- Structured logging: Serilog with JSON formatting, correlation IDs (TenantId, TransactionId, RideId, PaymentReferenceId)
- Distributed tracing: OpenTelemetry instrumentation for ASP.NET Core, HTTP, EF Core, Runtime
- Metrics: OpenTelemetry histogram for ledger_append_duration_ms with tags (tenant_id, transaction_type)
- Health checks: Liveness (/health/live), Readiness (/health/ready with DB check), Startup (/health/startup)
- Retry logic: Polly exponential backoff for HTTP clients, EF Core EnableRetryOnFailure
- Circuit breakers: Polly configured for external service calls
- Timeouts: Request timeouts configured globally

**V. Eventual Consistency** ✅ PASS (with justification)
- Ledger operations use STRONG CONSISTENCY (ACID transactions) - justified for financial data integrity
- Domain events: Not yet implemented (future: publish LedgerEntryCreated event for downstream consumers)
- Outbox pattern: Deferred to Phase 4 when invoice generation becomes event-driven
- Rationale: Accounting ledger requires immediate consistency for debits=credits guarantee; eventual consistency would violate accuracy requirement (SC-001: 100% accuracy)

**VI. Result Pattern for Error Handling** ✅ PASS
- Result<T> used for ALL business logic (handlers return Result<TResponse>)
- Error types: Validation (400), NotFound (404), Conflict (409 for idempotency violations), Failure (500)
- NO try-catch in controllers/endpoints
- Exceptions only for infrastructure: DbUpdateException (database errors), PostgresException (connection failures)

**VII. PostgreSQL & EF Core Standards** ✅ PASS
- snake_case naming: Global convention in DbContext
- NUMERIC(19,4): Money values with fixed precision
- Check constraints: Single-sided entries (debit > 0 AND credit = 0) OR (debit = 0 AND credit > 0)
- Unique indexes: Idempotency (account_id, source_reference_id)
- Row versioning: xid type for optimistic concurrency
- Migrations: Code-first with Up/Down methods, triggers in raw SQL
- Tenant isolation: HttpContext tenant filter applied to all queries
- UTC timestamps: timestamp with time zone, default NOW()

**GATE DECISION**: ✅ **APPROVED** - All NON-NEGOTIABLE principles satisfied. Strong consistency justified for financial accuracy. No constitutional violations.

## Project Structure

### Documentation (this feature)

```text
specs/001-accounting-ledger/
├── spec.md              # Feature specification (input)
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command) - TO BE CREATED
├── data-model.md        # Phase 1 output (/speckit.plan command) - TO BE CREATED
├── quickstart.md        # Phase 1 output (/speckit.plan command) - TO BE CREATED
├── contracts/           # Phase 1 output (/speckit.plan command) - TO BE CREATED
│   ├── ledger-api.yaml  # OpenAPI spec for ledger endpoints
│   ├── account-api.yaml # OpenAPI spec for account endpoints
│   └── invoice-api.yaml # OpenAPI spec for invoice endpoints
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan) - ALREADY EXISTS
```

### Source Code (repository root)

```text
src/
├── Accounting.Domain/              # Domain Layer (zero dependencies)
│   ├── Entities/
│   │   ├── Account.cs
│   │   ├── LedgerEntry.cs
│   │   ├── Invoice.cs
│   │   └── InvoiceLine.cs
│   ├── ValueObjects/
│   │   ├── Money.cs
│   │   ├── AccountId.cs
│   │   ├── RideId.cs
│   │   └── InvoiceNumber.cs
│   ├── Enums/
│   │   ├── AccountType.cs
│   │   ├── AccountStatus.cs
│   │   ├── LedgerAccount.cs      # AR, Revenue, Cash
│   │   └── TransactionType.cs    # RideCharge, Payment
│   ├── Interfaces/
│   │   ├── IAccountRepository.cs
│   │   ├── ILedgerRepository.cs
│   │   └── IInvoiceRepository.cs
│   └── Common/
│       ├── Result.cs              # Result pattern
│       └── Error.cs               # Error types
│
├── Accounting.Application/         # Application Layer
│   ├── Commands/
│   │   ├── RecordRideChargeCommand.cs
│   │   ├── RecordRideChargeCommandHandler.cs
│   │   ├── RecordPaymentCommand.cs
│   │   ├── RecordPaymentCommandHandler.cs
│   │   ├── CreateAccountCommand.cs
│   │   └── CreateAccountCommandHandler.cs
│   ├── Queries/
│   │   ├── GetAccountBalanceQuery.cs
│   │   ├── GetAccountBalanceQueryHandler.cs
│   │   ├── GenerateInvoiceQuery.cs
│   │   └── GenerateInvoiceQueryHandler.cs
│   ├── Validators/
│   │   ├── RecordRideChargeCommandValidator.cs
│   │   ├── RecordPaymentCommandValidator.cs
│   │   └── CreateAccountCommandValidator.cs
│   └── Common/
│       ├── ICommand.cs
│       └── IQuery.cs
│
├── Accounting.Infrastructure/       # Infrastructure Layer
│   ├── Persistence/
│   │   ├── DbContext/
│   │   │   └── AccountingDbContext.cs
│   │   ├── Entities/               # Persistence entities (separate from domain)
│   │   │   ├── AccountEntity.cs
│   │   │   ├── LedgerEntryEntity.cs
│   │   │   └── InvoiceEntity.cs
│   │   ├── Configurations/         # EF Core fluent API
│   │   │   ├── AccountConfiguration.cs
│   │   │   ├── LedgerEntryConfiguration.cs
│   │   │   └── InvoiceConfiguration.cs
│   │   └── Migrations/             # EF Core migrations
│   ├── Repositories/
│   │   ├── AccountRepository.cs
│   │   ├── LedgerRepository.cs
│   │   └── InvoiceRepository.cs
│   ├── Mappers/                    # Domain ↔ Persistence
│   │   ├── AccountMapper.cs
│   │   └── LedgerMapper.cs
│   └── BackgroundJobs/
│       └── InvoiceGenerator.cs     # Quartz.NET scheduled job
│
└── Accounting.API/                  # API/Presentation Layer
    ├── Endpoints/
    │   ├── LedgerEndpoints.cs       # Minimal API endpoints
    │   ├── AccountEndpoints.cs
    │   └── InvoiceEndpoints.cs
    ├── Middleware/
    │   ├── GlobalExceptionHandler.cs
    │   ├── AuthenticationMiddleware.cs
    │   └── TenantIsolationMiddleware.cs
    ├── Program.cs                    # Application entry point
    └── appsettings.json              # Configuration

tests/
├── Accounting.Domain.Tests/         # Domain unit tests
│   └── Entities/
│       ├── LedgerEntryTests.cs
│       └── AccountTests.cs
├── Accounting.Application.Tests/    # Application unit tests
│   └── Handlers/
│       ├── RecordRideChargeHandlerTests.cs
│       └── RecordPaymentHandlerTests.cs
├── Accounting.IntegrationTests/     # Integration tests (Testcontainers)
│   └── Ledger/
│       ├── LedgerOperationsTests.cs
│       ├── IdempotencyTests.cs
│       └── BalanceCalculationTests.cs
└── Accounting.ContractTests/        # API contract tests (WebApplicationFactory)
    └── LedgerApiTests.cs

docker/
└── docker-compose.yml               # PostgreSQL 17 container
```

**Structure Decision**: Clean Architecture with 4-layer separation (Domain → Application → Infrastructure → API). Domain layer has ZERO dependencies. Infrastructure layer maps between domain entities and persistence entities to maintain domain purity. API layer uses minimal APIs (not controllers) for better performance. Test projects organized by layer and test type (unit, integration, contract).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitutional violations. All NON-NEGOTIABLE principles satisfied without exceptions.

---

## Phase 0: Research & Technical Decisions

**Status**: ✅ COMPLETE - research.md generated (420 lines)

**Research Completed**:
1. ✅ Double-entry accounting implementation patterns in .NET/EF Core - Repository-level validation before SaveChanges
2. ✅ PostgreSQL trigger-based immutability enforcement - BEFORE triggers with RAISE EXCEPTION chosen for defense-in-depth
3. ✅ Idempotency strategies - Database unique constraint on (account_id, source_reference_id) chosen for atomic enforcement
4. ✅ Balance calculation - Real-time SUM() aggregation chosen for single source of truth (no denormalized column)
5. ✅ Multi-tenant isolation - EF Core global query filters chosen over PostgreSQL RLS for better error messages
6. ✅ Invoice generation - Synchronous for on-demand (User Story 3), background job for scheduled (User Story 4)
7. ✅ Fixed-point decimal handling - NUMERIC(19,4) in PostgreSQL + C# decimal type for no rounding errors
8. ✅ Entity Framework Core - Separate domain/persistence entities with explicit mapping for domain purity

**Output**: [research.md](./research.md) - 8 research areas with decisions, rationale, alternatives, and implementation patterns

---

## Phase 1: Design & Contracts

**Status**: ✅ COMPLETE - All design artifacts generated

**Design Completed**:
1. ✅ Data model - ERD with Account (aggregate root), LedgerEntry, Invoice, InvoiceLine entities with properties, relationships, validation rules (462 lines)
2. ✅ API contracts - 3 OpenAPI 3.0 specifications for ledger, accounts, and invoices endpoints with request/response schemas
3. ✅ Domain events - Deferred to post-MVP (Phase 4 - Integrations); not required for strong consistency model
4. ✅ Quickstart guide - Complete developer setup with Docker Compose, migrations, test execution, API access (519 lines)

**Output**: 
- [data-model.md](./data-model.md) - Complete entity definitions with aggregates, relationships, invariants, state transitions
- [contracts/ledger-api.yaml](./contracts/ledger-api.yaml) - POST /ledger/charges, POST /ledger/payments, GET /accounts/{id}/balance
- [contracts/accounts-api.yaml](./contracts/accounts-api.yaml) - POST /accounts, GET /accounts/{id}, PATCH /accounts/{id}/status
- [contracts/invoices-api.yaml](./contracts/invoices-api.yaml) - POST /invoices/generate, GET /invoices/{id}, GET /accounts/{id}/statement
- [quickstart.md](./quickstart.md) - Prerequisites, clone/setup, Docker commands, migration workflow, test execution, troubleshooting

---

## Next Steps

1. ✅ **Setup Complete**: plan.md filled with technical context, constitution check passed, structure defined
2. ✅ **Phase 0 Complete**: research.md generated with 8 technical decisions (double-entry patterns, immutability, idempotency, balance calculation, tenant isolation, invoice generation, decimal precision, domain/persistence separation)
3. ✅ **Phase 1 Complete**: data-model.md, 3 OpenAPI contracts (ledger, accounts, invoices), quickstart.md all generated
4. ✅ **Agent Context Updated**: Copilot context synchronized with .NET 9.0, PostgreSQL 17, Clean Architecture via update-agent-context.ps1
5. ✅ **Phase 2 Complete**: tasks.md already exists with detailed task breakdown for all 5 user stories
6. ⏳ **Implementation In Progress**: User Story 1 MVP complete (28/28 tasks), 4/12 contract tests passing, 8/12 tests failing (database write operations returning 500 errors)

**Current Blocker**: Test failures - TestWebApplicationFactory configuration issue causing 500 errors on ledger write operations. Need to debug database connection/transaction handling in test context before proceeding to User Stories 2-5.

**Post-Planning Actions**:
- Fix test failures (investigate PostgreSQL connection in TestWebApplicationFactory)
- Complete User Story 1 testing (all 12 contract tests passing)
- Proceed with User Stories 2-5 implementation per tasks.md
- Verify all 10 Success Criteria (SC-001 to SC-010) meet targets
- Validate Definition of Done checklist before production deployment
