# Feature Specification: Dual-Entry Accounting & Invoicing Service

**Feature Branch**: `001-accounting-ledger`  
**Version**: v1.0  
**Created**: 2026-02-06  
**Updated**: 2026-02-07  
**Status**: Complete  
**Input**: PRD from [20260205.requirements.backend.md](../../../prerequists/20260205.requirements.backend.md)

**Constitutional Alignment**: This specification must align with `.specify/memory/constitution.md` principles, particularly Test-First Development (Principle III) with acceptance scenarios serving as test specifications.

---

## Executive Summary

The Dual-Entry Accounting & Invoicing Service provides a **financial system of record** for all billable ride services delivered by transportation fleets. It records **ride-based charges and payments** using a **double-entry ledger model** and supports **on-demand invoice generation** at multiple frequencies.

This service is financially authoritative, while ride creation, fare calculation, and payment processing remain external to this service.

The system ensures:
- Accounting correctness through double-entry ledger validation
- Audit-ready financial records with complete immutability
- Flexible billing cycles (daily, weekly, monthly, or per ride)
- Clear traceability from ride → ledger → invoice → balance

---

## Business Context

### Problem Statement

As the platform scales across organizations and individuals, there is a growing need for:
- Accurate receivables tracking across multiple customer accounts
- Consolidated billing with professional invoice generation
- Clear outstanding balance visibility for finance teams
- Enterprise-grade financial reporting and auditing

Without a formal ledger system:
- Billing logic becomes fragmented across multiple services
- Manual reconciliation increases operational overhead
- Financial audits become difficult and error-prone
- Revenue reporting becomes unreliable and inconsistent

### Business Goals

1. **Establish a double-entry accounting foundation** that ensures mathematical correctness (debits = credits) for all financial transactions
2. **Support multiple billing frequencies** to accommodate diverse customer preferences (per-ride, daily, weekly, monthly)
3. **Enable self-service invoice generation** for on-demand billing without manual intervention
4. **Maintain immutable, traceable financial records** for audit compliance and dispute resolution
5. **Prepare for future fleet-wise revenue reporting** with clean separation between receivables and revenue

### Success Metrics

| Metric | Target | Rationale |
|--------|--------|-----------|
| Ledger accuracy | 100% | Zero tolerance for accounting errors; debits must always equal credits |
| Duplicate charge incidents | 0 | Idempotency enforcement prevents double-billing |
| Invoice generation latency | < 2 seconds | Enables real-time invoice generation for customer service scenarios |
| Ledger append latency | < 100ms (p95) | Supports high-throughput transaction recording |
| Ledger to invoice traceability | 100% | Every invoice line must reference source ledger entries |
| Tenant data leakage | 0 | Complete isolation required for multi-tenant SaaS deployment |
| Balance accuracy | $0.01 precision | Cent-level accuracy for all monetary calculations |

---

## Scope

### In Scope

✅ **Core Accounting Engine**
- Double-entry ledger implementation with debit/credit validation
- Append-only, immutable ledger entries with audit metadata
- Fixed-point decimal arithmetic for monetary precision

✅ **Account Management**
- Account creation for Organizations and Individuals
- Account status management (Active/Inactive)
- Multi-tenant account isolation

✅ **Transaction Recording**
- Ride service charge recording (Accounts Receivable DR, Service Revenue CR)
- Payment recording (Cash/Bank DR, Accounts Receivable CR)
- Idempotency enforcement for duplicate prevention
- Support for partial payments, full payments, and overpayments

✅ **Balance & Reporting**
- Real-time account balance calculation (Total DR - Total CR)
- Account statements for any date range
- Negative balance support (credit balances from overpayments)

✅ **Invoice Generation**
- On-demand invoice generation for date ranges or specific rides
- Multiple invoice frequencies (per-ride, daily, weekly, monthly)
- Immutable invoices with unique invoice numbers per tenant
- Complete traceability from invoice line items to ledger entries

✅ **Multi-Tenant Isolation**
- Tenant ID enforcement at all data access layers
- Zero cross-tenant data leakage through strict isolation

### Out of Scope

❌ **Ride Lifecycle Management** - Ride creation, dispatching, tracking handled by external Ride Service  
❌ **Fare Calculation Logic** - Fare rules, pricing models, surge pricing handled by external Fare Service  
❌ **Payment Gateway Integration** - Payment processing, card tokenization, settlements handled by external Payment Service  
❌ **Tax Handling** - GST, VAT, sales tax calculations deferred to future phase  
❌ **Manual Adjustments or Credits** - Credit notes, manual corrections deferred to future phase  
❌ **Fleet Payouts** - Driver/fleet settlements handled by separate Payout Service  
❌ **Multi-Currency Support** - Fixed to USD only for MVP  
❌ **Historic Data Migration** - Fresh start; no legacy ledger import

---

## Core Concepts

### Account

Represents the **financially responsible entity** that incurs charges and makes payments.

**Account Types:**
- **Organization** - Rehab Centers, Medical Facilities, Hospitals, Old Age Homes (business accounts)
- **Individual** - Passengers, Guardians, Relatives (personal accounts)

Each account owns:
- **A ledger** - All financial transactions (charges and payments) posted to this account
- **A balance** - Calculated as SUM(debits) - SUM(credits) from ledger entries
- **Invoice history** - All invoices generated for this account

**Account Properties:**
- Account ID (unique identifier, externally provided)
- Account Name (display name for billing)
- Account Type (Organization or Individual)
- Status (Active or Inactive)
- Tenant ID (multi-tenant isolation)
- Currency (fixed to USD)
- Created/Modified timestamps and user metadata

**Account Status Rules:**
- **Active**: Can receive new charges and payments
- **Inactive**: Cannot receive new transactions; historical invoices/statements still accessible

### Currency

- **Single currency per tenant**: USD only for MVP
- **No currency conversion required**: All amounts stored and displayed in USD
- **Fixed-point arithmetic**: Uses `NUMERIC(19,4)` to avoid floating-point errors
- **Precision**: Calculations accurate to cent level ($0.01)

### Ledger (Double-Entry Accounting)

The ledger follows **classical double-entry accounting** principles:

**Double-Entry Rule**: Every transaction produces **exactly two ledger entries** where:
```
Total Debits = Total Credits
```

**Ledger Accounts** (Chart of Accounts):
- **Accounts Receivable (AR)** - Asset account tracking amounts owed by customers
- **Service Revenue** - Revenue account tracking earned income from ride services
- **Cash/Bank** - Asset account tracking received payments

**Example Transaction - Ride Charge:**
| Ledger Account | Debit | Credit |
|----------------|-------|--------|
| Accounts Receivable | $25.00 | — |
| Service Revenue | — | $25.00 |

**Example Transaction - Payment Received:**
| Ledger Account | Debit | Credit |
|----------------|-------|--------|
| Cash/Bank | $25.00 | — |
| Accounts Receivable | — | $25.00 |

**Ledger Characteristics:**
- **Append-only**: New entries can be added; existing entries cannot be modified
- **Immutable**: Enforced by PostgreSQL triggers (prevent UPDATE/DELETE)
- **Fully auditable**: Every entry stores source type, source reference ID, timestamp, created by
- **Tenant-isolated**: All entries filtered by tenant ID at data access layer

**Balance Calculation:**
```
Account Balance = SUM(AR Debits) - SUM(AR Credits)
Positive Balance = Customer owes money (receivable)
Negative Balance = Customer has credit (prepayment/overpayment)
Zero Balance = Account is settled
```

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record Financial Transactions (Priority: P1)

A billing system records completed ride charges and received payments to a double-entry ledger, ensuring accounting correctness and establishing the foundation for all financial reporting.

**Why this priority**: This is the core accounting foundation. Without accurate ledger entries for charges and payments, no other financial operations (invoicing, statements, reporting) can function correctly. This delivers immediate value by establishing a system of financial record.

**Independent Test**: Can be fully tested by posting ride charges and payments, then verifying that ledger entries balance (debits = credits), balances calculate correctly, and duplicate transactions are prevented. Delivers a working double-entry ledger system.

**Acceptance Scenarios**:

1. **Given** a ride is completed with fare $25.00, **When** the system records the ride charge for Account A123, **Then** two ledger entries are created: Accounts Receivable debited $25.00 and Service Revenue credited $25.00
2. **Given** Account A123 has an outstanding balance of $25.00, **When** a payment of $25.00 is recorded, **Then** two ledger entries are created: Cash debited $25.00 and Accounts Receivable credited $25.00
3. **Given** a ride with ID R456 has already been posted to Account A123, **When** the system attempts to record the same Ride ID again for the same account, **Then** the transaction is rejected with an idempotency error
4. **Given** multiple transactions for an account, **When** calculating the account balance, **Then** Balance = Total Debits - Total Credits and the formula is accurate to the cent
5. **Given** Account A123 receives a partial payment of $10.00 against a $25.00 charge, **When** the payment is recorded, **Then** the ledger entries are correct and the balance shows $15.00 outstanding
6. **Given** Account A123 receives an overpayment of $30.00 against a $25.00 charge, **When** the payment is recorded, **Then** the ledger entries are correct and the balance shows -$5.00 (credit balance)

---

### User Story 2 - Manage Customer Accounts (Priority: P2)

Billing administrators create and manage customer accounts (organizations and individuals) that will be charged for ride services, enabling multi-tenant financial tracking.

**Why this priority**: Accounts are required containers for ledger entries, but the ledger logic (P1) is more foundational. Account management can be implemented after the ledger engine proves correct. Delivers ability to segment finances by customer.

**Independent Test**: Can be fully tested by creating accounts with required attributes, verifying tenant isolation, and ensuring account status changes work correctly. Delivers a working account management system.

**Acceptance Scenarios**:

1. **Given** a new organization customer "Metro Rehab Center", **When** creating an account with Account ID A123, Name "Metro Rehab Center", Type "Organization", Status "Active", **Then** the account is created and associated with the current tenant
2. **Given** a new individual rider "John Doe", **When** creating an account with Account ID A124, Name "John Doe", Type "Individual", Status "Active", **Then** the account is created successfully
3. **Given** Tenant T1 has Account A123 and Tenant T2 has Account A456, **When** Tenant T1 attempts to access Account A456, **Then** access is denied (tenant isolation enforced)
4. **Given** an existing active account A123, **When** the account status is changed to "Inactive", **Then** the account status is updated and future transactions to inactive accounts are prevented
5. **Given** an attempt to create an account without required fields (Account ID, Name, Type), **Then** validation fails with clear error messages

---

### User Story 3 - Generate Invoices (Priority: P3)

Finance teams generate invoices on-demand for any date range or specific rides, providing professional billing documents to customers with clear traceability to ledger entries.

**Why this priority**: Invoicing depends on existing ledger entries (P1) and accounts (P2). While important for customer communication, the underlying ledger is more critical. Delivers formal billing capability.

**Independent Test**: Can be fully tested by generating invoices for specific date ranges or ride IDs, verifying invoice structure, line items trace to ledger entries, and invoices are immutable once created. Delivers working invoice generation.

**Acceptance Scenarios**:

1. **Given** Account A123 has 3 completed rides ($25, $30, $20) posted between Jan 1-7, 2026, **When** generating an invoice for date range Jan 1-7, 2026, **Then** an invoice is created with 3 line items totaling $75, each line item references the corresponding ledger entry ID
2. **Given** an invoice request for specific Ride IDs [R1, R2, R5], **When** the invoice is generated, **Then** only those 3 rides appear as line items with correct fare amounts
3. **Given** Account A123 has charges totaling $100 and payments totaling $40, **When** generating an invoice, **Then** the invoice shows subtotal $100, payments applied $40, and outstanding balance $60
4. **Given** an invoice INV-001 has been generated, **When** attempting to modify the invoice, **Then** the operation is rejected (invoices are immutable)
5. **Given** a request to generate an invoice for Account A123, **When** the invoice is created, **Then** it contains a unique invoice number (unique per tenant), account details, billing period, and creation timestamp
6. **Given** Account A123 has a payment recorded before any charges, **When** generating an invoice for subsequent charges, **Then** the payment is correctly applied and the balance reflects the prepayment

---

### User Story 4 - Support Multiple Invoice Frequencies (Priority: P4)

Organizations configure their preferred billing frequency (per-ride, daily, weekly, monthly) and the system generates invoices accordingly, accommodating diverse customer billing preferences.

**Why this priority**: This is an enhancement to basic invoice generation (P3). The core value is delivered with on-demand invoicing; frequency automation adds convenience but isn't foundational. Delivers billing flexibility.

**Independent Test**: Can be fully tested by configuring different invoice frequencies for test accounts and verifying invoices are generated with correct date ranges per the frequency setting. Delivers automated billing cycles.

**Acceptance Scenarios**:

1. **Given** Account A123 is configured for "Per Ride" invoicing, **When** a ride charge is posted, **Then** an invoice is automatically generated containing only that single ride
2. **Given** Account A124 is configured for "Daily" invoicing, **When** the daily billing job runs at end of day, **Then** an invoice is generated containing all rides completed that day
3. **Given** Account A125 is configured for "Weekly" invoicing, **When** the weekly billing job runs on Sunday, **Then** an invoice is generated for all rides from Monday-Sunday
4. **Given** Account A126 is configured for "Monthly" invoicing, **When** the monthly billing job runs on the last day of the month, **Then** an invoice is generated for all rides in that calendar month
5. **Given** an account has no rides in a billing period, **When** the scheduled invoice generation runs, **Then** no invoice is generated for that period

---

### User Story 5 - Generate Account Statements (Priority: P5)

Customers and finance teams can request account statements for any date range showing all transactions (charges and payments) with opening balance, transaction details, and closing balance.

**Why this priority**: Statements provide visibility but aren't required for core operations (ledger, accounts, invoicing). They enhance transparency and customer service. Delivers transaction history reporting.

**Independent Test**: Can be fully tested by requesting statements for various date ranges and verifying they show correct opening balance, chronological transactions, and accurate closing balance. Delivers account history reporting.

**Acceptance Scenarios**:

1. **Given** Account A123 has charges and payments between Jan 1-31, 2026, **When** requesting a statement for this date range, **Then** the statement shows opening balance as of Jan 1, all transactions in chronological order, and closing balance as of Jan 31
2. **Given** a statement request with no transactions in the date range, **When** the statement is generated, **Then** it shows opening and closing balances as equal with no transaction lines
3. **Given** Account A123 has mixed transaction types, **When** generating a statement, **Then** each transaction line shows: date, type (Charge/Payment), reference ID (Ride ID/Payment ID), amount, and running balance
4. **Given** requesting a statement for a date range before the account was created, **When** the statement is generated, **Then** opening balance is $0.00 and only transactions within the valid date range appear

---

### Edge Cases

- **Duplicate ride posting**: What happens when the same Ride ID is posted to the same account twice? (Answer: System rejects with idempotency error)
- **Negative balance**: How does the system handle overpayments resulting in negative balance? (Answer: Negative balance indicates credit; account can continue to receive charges which reduce the credit)
- **Zero-amount transactions**: Can a ride with $0 fare be posted? (Answer: Yes, for data completeness, though it creates offsetting $0 ledger entries)
- **Concurrent transactions**: What happens if two payments are recorded simultaneously for the same account? (Answer: Both are accepted; ledger entries are append-only and balance calculation handles all entries)
- **Tenant isolation breach attempts**: How does system prevent cross-tenant data access? (Answer: All queries include tenant ID filter at data access layer; constitutional requirement for zero tenant leakage)
- **Invoice generation during active transactions**: What happens if an invoice is requested while transactions are being posted? (Answer: Invoice captures transactions up to the moment of generation with strong consistency)
- **Large date ranges**: How does system handle statement requests for multi-year date ranges? (Answer: Pagination required for performance; constitutional p95 latency must stay <500ms)
- **Account with no ledger**: What is the balance of a newly created account with no transactions? (Answer: $0.00)
- **Payment without prior charges**: Can a payment be recorded to an account with no outstanding charges? (Answer: Yes, resulting in a credit balance; valid for prepayments)
- **Invoice for inactive account**: Can invoices be generated for inactive accounts? (Answer: Yes, for historical billing; only new transaction posting is prevented for inactive accounts)

## Requirements *(mandatory)*

### Functional Requirements

#### Account Management
- **FR-001**: System MUST allow creation of accounts with Account ID, Account Name, Account Type (Organization or Individual), and Status (Active or Inactive)
- **FR-002**: System MUST enforce tenant isolation ensuring accounts from one tenant cannot be accessed by another tenant
- **FR-003**: System MUST support updating account status between Active and Inactive
- **FR-004**: System MUST prevent posting new transactions to Inactive accounts
- **FR-005**: System MUST fix currency to USD (no currency conversion)

#### Ledger Operations
- **FR-006**: System MUST record ride charges using double-entry accounting with Accounts Receivable (debit) and Service Revenue (credit)
- **FR-007**: System MUST record payments using double-entry accounting with Cash/Bank (debit) and Accounts Receivable (credit)
- **FR-008**: System MUST ensure every transaction creates exactly two ledger entries where total debits equal total credits
- **FR-009**: System MUST make ledger entries append-only and immutable (no updates or deletes permitted)
- **FR-010**: System MUST reject duplicate ride charges by preventing the same Ride ID from being posted to the same account more than once
- **FR-011**: System MUST support partial payments, full payments, and overpayments against outstanding charges
- **FR-012**: System MUST use fixed-point arithmetic for all monetary calculations to ensure precision
- **FR-013**: System MUST complete ledger append operations in under 100ms (p95 latency)

#### Balance Calculation
- **FR-014**: System MUST calculate account balance using formula: Balance = Total Debits - Total Credits
- **FR-015**: System MUST ensure balance calculations are accurate to the cent ($0.01)
- **FR-016**: System MUST support negative balances (credit balances) when overpayments occur

#### Invoice Generation
- **FR-017**: System MUST generate invoices on-demand for specified date ranges or explicit Ride IDs
- **FR-018**: System MUST include in each invoice: unique invoice number (per tenant), account details, billing period, line items, subtotal, payments applied, outstanding balance
- **FR-019**: System MUST ensure every invoice line item references its source ledger entry ID for full traceability
- **FR-020**: System MUST make invoices immutable once generated (read-only)
- **FR-021**: System MUST generate invoices in under 2 seconds
- **FR-022**: System MUST support invoice generation at multiple frequencies: Per Ride, Daily, Weekly, Monthly
- **FR-023**: System MUST skip invoice generation for billing periods with no transactions

#### Account Statements
- **FR-024**: System MUST generate account statements for any requested date range showing opening balance, transactions, and closing balance
- **FR-025**: System MUST display transactions in chronological order with date, type, reference ID, amount, and running balance

#### Audit and Metadata
- **FR-026**: System MUST store audit metadata for every ledger entry including: Source Type (Ride or Payment), Source Reference ID, Created Timestamp, Created By
- **FR-027**: System MUST ensure 100% traceability from ride → ledger entry → invoice line item → balance

#### Data Integrity
- **FR-028**: System MUST guarantee strong consistency for all ledger operations
- **FR-029**: System MUST ensure 100% ledger accuracy (zero incorrect entries)
- **FR-030**: System MUST ensure zero duplicate charge incidents through idempotency enforcement
- **FR-031**: System MUST ensure zero tenant data leakage through strict isolation

### Key Entities

- **Account**: Represents the financially responsible entity (organization or individual); contains Account ID, Name, Type, Status, Tenant ID, Currency (fixed USD); owns a ledger and balance
- **Ledger Entry**: Individual line in the double-entry ledger; contains Entry ID, Account ID, Ledger Account (Accounts Receivable, Service Revenue, Cash/Bank), Debit Amount, Credit Amount, Source Type (Ride/Payment), Source Reference ID, Created Timestamp, Created By; immutable
- **Invoice**: Formal billing document; contains Invoice Number (unique per tenant), Account ID, Billing Period (start/end dates), Line Items, Subtotal, Payments Applied, Outstanding Balance, Generated Timestamp; immutable after creation
- **Invoice Line Item**: Individual charge on an invoice; contains Ride ID, Service Date, Fare Amount, Ledger Entry ID Reference; provides traceability
- **Transaction**: Generic term for ride charges and payments that create ledger entries; includes metadata for audit trail

### Non-Functional Requirements

- **NFR-001: Fixed-Point Monetary Calculations** - System MUST use `NUMERIC(19,4)` data type in PostgreSQL and `decimal` type in C# for all monetary values to avoid floating-point precision errors
- **NFR-002: Strong Consistency** - System MUST use PostgreSQL ACID transactions to guarantee strong consistency for all ledger operations; eventual consistency is NOT acceptable for financial data
- **NFR-003: Horizontal Scalability (Reads)** - System SHOULD support read replicas for balance queries and statement generation to enable horizontal scaling of read operations
- **NFR-004: Ledger Append Performance** - System MUST complete ledger append operations (record charge or payment) in under 100ms at p95 latency
- **NFR-005: Invoice Generation Performance** - System MUST generate invoices with up to 100 line items in under 2 seconds
- **NFR-006: Database Optimization** - System MUST use database indexes on frequently queried columns (account_id, tenant_id, created_at, source_reference_id) and optimized queries with AsNoTracking() for read operations
- **NFR-007: Immutability Enforcement** - System MUST use PostgreSQL triggers (`prevent_ledger_update`, `prevent_ledger_delete`) to enforce ledger immutability at the database level as defense-in-depth
- **NFR-008: Audit Logging** - System MUST log all ledger operations with correlation IDs (TenantId, TransactionId, RideId, PaymentReferenceId) using structured logging (Serilog)
- **NFR-009: Observability** - System MUST implement OpenTelemetry distributed tracing and metrics (including `ledger_append_duration_ms` histogram) for production monitoring
- **NFR-010: Resilience** - System MUST implement retry policies (exponential backoff), circuit breakers, and timeout patterns using Polly for external service calls
- **NFR-011: Health Checks** - System MUST expose liveness, readiness, and startup health check endpoints for Kubernetes orchestration
- **NFR-012: API Documentation** - System MUST provide OpenAPI 3.0 specifications for all API endpoints with example requests/responses

---

## Assumptions

### Technical Assumptions

1. **Single Currency**: USD only; no multi-currency support required for MVP
2. **External Systems**: Ride Service, Fare Service, and Payment Service are upstream dependencies that provide ride completion events, calculated fares, and payment confirmations
3. **No Historic Migration**: Fresh ledger; no legacy data import from previous systems
4. **Tenant Context via JWT**: Tenant ID extracted from JWT claims in authentication middleware
5. **Fixed Chart of Accounts**: Three ledger accounts only (Accounts Receivable, Service Revenue, Cash/Bank); no user-defined accounts
6. **PostgreSQL Database**: PostgreSQL 17 required for trigger support and NUMERIC precision
7. **.NET 9.0 Runtime**: Latest stable .NET release (not .NET 10 from original tech specs)

### Business Assumptions

1. **No Tax Handling**: Sales tax, GST, VAT calculations deferred to future phase
2. **No Manual Adjustments**: Credit notes, manual corrections, refunds deferred to future phase
3. **Ride-Based Billing**: System assumes all charges originate from completed rides; other charge types (e.g., subscription fees) out of scope
4. **Payment Reconciliation**: Assumes external Payment Service provides accurate payment confirmations; this service records payments but doesn't reconcile bank deposits
5. **No Dispute Management**: Chargebacks, disputes, billing corrections handled manually outside the system
6. **Organization-Level Billing**: Individual accounts may be billed separately or consolidated under organization accounts (business decision pending)

### Data Assumptions

1. **Ride ID Uniqueness**: Ride IDs are globally unique per tenant (provided by Ride Service)
2. **Payment Reference ID Uniqueness**: Payment reference IDs are globally unique per tenant (provided by Payment Service)
3. **Account ID External**: Account IDs provided by external system during account creation (not auto-generated by this service)
4. **USD Precision**: All monetary amounts have maximum 4 decimal places ($0.0001 precision)
5. **Date/Time UTC**: All timestamps stored in UTC; timezone conversion responsibility of client applications

---

## Future Extensions

The following features are **explicitly deferred** to post-MVP phases:

### Phase 2 - Advanced Billing
- **Adjustments and Credit Notes**: Manual corrections, refunds, chargebacks
- **Tax Ledger Entries**: Automated GST/VAT/sales tax calculation and recording
- **Multi-Currency Support**: Currency conversion and forex rate tracking
- **Custom Invoice Templates**: Branded invoices with logos and custom layouts
- **Recurring Charges**: Subscription fees, membership charges beyond ride-based billing

### Phase 3 - Reporting & Analytics
- **Fleet-Wise Revenue Reports**: Revenue breakdown by fleet, driver, vehicle, route
- **Aging Reports**: Accounts receivable aging (30/60/90 days outstanding)
- **Payment Forecasting**: Predictive analytics for expected payment dates
- **Dunning Management**: Automated reminders for overdue invoices

### Phase 4 - Integrations
- **ERP Integration**: Export to QuickBooks, Xero, SAP, Oracle Financials
- **Payment Gateway Integration**: Direct payment processing within invoices
- **Email/SMS Delivery**: Automated invoice and statement delivery to customers
- **Webhook Notifications**: Real-time events for invoice generation, payment recording

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

These success criteria define the measurable outcomes that determine feature completion and production readiness. All criteria are testable and technology-agnostic.

**Accuracy & Correctness:**
- **SC-001: 100% Ledger Accuracy** - Ledger maintains zero incorrect entries with mathematical guarantee that debits always equal credits for every transaction (verified through integration tests with double-entry validation)
- **SC-002: Cent-Level Precision** - Balance calculations are accurate to the cent ($0.01) for all accounts using fixed-point arithmetic; no floating-point rounding errors (verified through decimal precision tests)
- **SC-007: 100% Idempotency** - System prevents 100% of duplicate charge attempts through database-level idempotency enforcement; zero duplicate charge incidents in production (verified through unique constraint violations returning HTTP 409)

**Performance:**
- **SC-003: <100ms Ledger Append (p95)** - Ledger append operations (record charge or payment) complete in under 100ms at p95 latency to support high-throughput transaction recording (measured via OpenTelemetry histogram `ledger_append_duration_ms`)
- **SC-004: <2s Invoice Generation** - Invoice generation completes in under 2 seconds for typical billing periods with up to 100 line items, enabling real-time customer service (measured via API response time monitoring)
- **SC-008: Horizontally Scalable Reads** - System supports horizontally scalable reads through read replicas while maintaining strong consistency for writes (verified through load testing with read replica configuration)

**Traceability & Audit:**
- **SC-005: 100% Financial Traceability** - System achieves complete traceability from ride → ledger entry → invoice line item → account balance; every charge can be traced through the entire financial lifecycle (verified through audit trail queries joining rides, ledger, invoices)
- **SC-009: Immutable Audit Trail** - All ledger entries and invoices remain immutable after creation; database triggers prevent UPDATE/DELETE operations (verified through trigger execution tests)

**Security & Isolation:**
- **SC-006: Zero Tenant Data Leakage** - System achieves zero tenant data leakage incidents through strict multi-tenant isolation with tenant ID filtering at all data access layers (verified through cross-tenant access attempt tests)
- **SC-010: Authentication Required** - All API endpoints require valid JWT authentication with tenant context; anonymous access returns HTTP 401 (verified through contract tests without authentication headers)

### Definition of Done

Feature is considered **complete and production-ready** when:

✅ **All 5 User Stories implemented** with passing acceptance tests (33 test scenarios across contract and integration tests)  
✅ **All 31 Functional Requirements** implemented and verified through automated tests  
✅ **All 12 Non-Functional Requirements** met and measured (performance metrics within targets, security controls active)  
✅ **All 10 Success Criteria** passing (accuracy, performance, traceability, security metrics green)  
✅ **Database migrations applied** with triggers, indexes, and constraints enforced  
✅ **API contracts published** (3 OpenAPI specs for ledger, accounts, invoices endpoints)  
✅ **Documentation complete** (quickstart guide, data model, technical research, implementation plan)  
✅ **Observability configured** (structured logging with Serilog, OpenTelemetry tracing and metrics)  
✅ **Health checks operational** (liveness, readiness, startup endpoints responding)  
✅ **Constitutional compliance verified** (all 7 NON-NEGOTIABLE principles from constitution.md satisfied)
