# Data Model: Dual-Entry Accounting & Invoicing Service

**Feature**: [spec.md](spec.md)  
**Date**: 2026-02-06  
**Status**: Complete

**Note**: This data model describes domain entities and their relationships (DDD perspective). Implementation uses separate persistence entities per Constitutional Principle VII.

---

## Aggregates

### Account (Aggregate Root)

Represents a financially responsible entity (organization or individual) that incurs charges and makes payments.

**Attributes**:
- `Id` (AccountId): Unique identifier - ValueObject wrapping Guid
- `Name` (string, 1-200 chars): Account display name (e.g., "Metro Rehab Center", "John Doe")
- `Type` (AccountType): Enum - Organization | Individual
- `Status` (AccountStatus): Enum - Active | Inactive
- `TenantId` (Guid): Multi-tenant isolation identifier
- `Currency` (string, fixed): "USD" (hardcoded per requirements)
- `CreatedAt` (DateTime): UTC timestamp
- `CreatedBy` (string): User who created the account
- `ModifiedAt` (DateTime?): UTC timestamp of last modification
- `ModifiedBy` (string?): User who last modified the account

**Invariants**:
- Name cannot be empty or whitespace
- Active accounts can receive transactions; Inactive accounts cannot
- TenantId must match current authenticated tenant (enforced at infrastructure layer)
- Currency is always "USD" (no mutation allowed)

**Behaviors**:
- `Activate()`: Change status to Active (if currently Inactive)
- `Deactivate()`: Change status to Inactive (if currently Active)
- `CanReceiveTransactions()`: Returns true if Status == Active

**Relationships**:
- Has many LedgerEntries (referenced by AccountId foreign key)
- Has many Invoices (referenced by AccountId foreign key)

**State Transitions**:
```
[Created with Active status] ──Deactivate()──> [Inactive]
           ↑                                         │
           └────────────Activate()───────────────────┘
```

---

### LedgerEntry (Entity within Ledger Context)

Represents a single line in the double-entry ledger. Each financial transaction produces exactly two ledger entries: one debit and one credit.

**Attributes**:
- `Id` (Guid): Unique identifier for the ledger entry
- `AccountId` (AccountId): Foreign key to Account aggregate
- `LedgerAccount` (LedgerAccount): Enum - AccountsReceivable | ServiceRevenue | Cash
- `DebitAmount` (Money): Amount debited (zero if this entry is a credit)
- `CreditAmount` (Money): Amount credited (zero if this entry is a debit)
- `SourceType` (TransactionType): Enum - RideCharge | Payment
- `SourceReferenceId` (string): External reference (RideId for charges, PaymentId for payments)
- `TenantId` (Guid): Multi-tenant isolation identifier
- `CreatedAt` (DateTime): UTC timestamp (immutable)
- `CreatedBy` (string): User or system that created the entry (immutable)
- `Description` (string?): Optional human-readable description

**Invariants**:
- Exactly one of DebitAmount or CreditAmount must be non-zero (enforced by factory methods + database check constraint)
- Both amounts cannot be non-zero (single-sided entry only)
- Both amounts cannot be zero (entry must have an effect)
- CreatedAt cannot be modified (append-only, immutable)
- SourceReferenceId + AccountId + SourceType must be unique for idempotency (e.g., prevent duplicate ride charges)

**Behaviors** (Factory Methods - prevent direct construction):
- `Debit(accountId, ledgerAccount, amount, source, referenceId)`: Create debit entry
- `Credit(accountId, ledgerAccount, amount, source, referenceId)`: Create credit entry

**Relationships**:
- Belongs to one Account (via AccountId)
- Referenced by InvoiceLineItem (via LedgerEntryId)

**Immutability**:
- Once created, LedgerEntry cannot be updated or deleted (enforced by database triggers per research.md)
- RowVersion/Timestamp tracks concurrent modification attempts

**Validation Rules**:
- DebitAmount and CreditAmount must use Money value object (enforces decimal precision)
- LedgerAccount must be valid enum value
- SourceType determines allowed LedgerAccount combinations (e.g., RideCharge only uses AR/Revenue)

---

### Invoice (Aggregate Root)

Represents a formal billing document generated for an account, containing line items that reference ledger entries.

**Attributes**:
- `Id` (Guid): Unique identifier
- `InvoiceNumber` (InvoiceNumber): ValueObject - unique per tenant (e.g., "INV-2026-001")
- `AccountId` (AccountId): Foreign key to Account aggregate
- `BillingPeriodStart` (DateTime): Start of billing period (inclusive)
- `BillingPeriodEnd` (DateTime): End of billing period (inclusive)
- `TenantId` (Guid): Multi-tenant isolation identifier
- `Subtotal` (Money): Sum of all line item amounts (calculated)
- `PaymentsApplied` (Money): Total payments applied during period (calculated)
- `OutstandingBalance` (Money): Subtotal - PaymentsApplied (calculated)
- `LineItems` (List<InvoiceLineItem>): Collection of billable rides
- `GeneratedAt` (DateTime): UTC timestamp of invoice creation
- `GeneratedBy` (string): User or system that generated the invoice

**Invariants**:
- InvoiceNumber must be unique per tenant
- BillingPeriodEnd must be >= BillingPeriodStart
- Once generated, invoice is immutable (no updates to amounts or line items)
- Subtotal must equal sum of LineItems.Amount
- All LineItems must reference ledger entries within the billing period

**Behaviors**:
- `Generate(accountId, periodStart, periodEnd, ledgerEntries)`: Factory method to create invoice from ledger entries
- `CalculateSubtotal()`: Returns sum of all line item amounts
- `CalculatePaymentsApplied()`: Returns sum of payment ledger entries in period
- `CalculateOutstandingBalance()`: Returns Subtotal - PaymentsApplied

**Relationships**:
- Belongs to one Account (via AccountId)
- Contains many InvoiceLineItems (composition - child entities)

**State Transitions**:
```
[Generated] ──> [Immutable - No further transitions]
```

---

### InvoiceLineItem (Entity within Invoice Aggregate)

Represents a single billable ride on an invoice, with traceability to the source ledger entry.

**Attributes**:
- `Id` (Guid): Unique identifier
- `InvoiceId` (Guid): Foreign key to parent Invoice
- `LedgerEntryId` (Guid): Foreign key to LedgerEntry (provides traceability)
- `RideId` (RideId): ValueObject - external ride reference
- `ServiceDate` (DateTime): Date the ride occurred
- `Amount` (Money): Fare amount (matches ledger entry amount)
- `Description` (string?): Optional description (e.g., "Ride from A to B")

**Invariants**:
- Amount must match the corresponding LedgerEntry debit/credit amount
- ServiceDate must fall within parent Invoice billing period
- LedgerEntryId must reference a valid, existing ledger entry

**Relationships**:
- Belongs to one Invoice (via InvoiceId)
- References one LedgerEntry (via LedgerEntryId) for audit trail

**Traceability Chain**:
```
RideId → LedgerEntry → InvoiceLineItem → Invoice → Account Balance
```

---

## Value Objects

### Money

Represents a monetary amount with fixed-point precision.

**Attributes**:
- `Amount` (decimal): Underlying value, rounded to 4 decimal places

**Invariants**:
- Always uses decimal type (no floating point)
- Automatically rounds to 4 decimal places (matches PostgreSQL NUMERIC(19,4))
- Immutable (readonly record struct)

**Behaviors**:
- `+ operator`: Add two Money values
- `- operator`: Subtract two Money values
- `IsPositive`, `IsNegative`, `IsZero`: Convenience properties
- `Abs()`: Absolute value
- `Zero`: Static constant for $0.00

**Rationale**: Enforces fixed-point arithmetic, prevents floating-point errors, ensures cent-level precision per Constitutional Principle VI.

---

### AccountId

Strongly-typed identifier for Account aggregate.

**Attributes**:
- `Value` (Guid): Underlying unique identifier

**Rationale**: Prevents mixing Account IDs with other GUIDs (type safety), follows DDD value object pattern.

---

### RideId

Strongly-typed identifier for external ride reference (owned by Ride Management service).

**Attributes**:
- `Value` (string): External ride identifier

**Rationale**: Clearly distinguishes ride references from internal identifiers, enables idempotency checks.

---

### InvoiceNumber

Strongly-typed invoice number with tenant-scoped uniqueness.

**Attributes**:
- `Value` (string): Formatted invoice number (e.g., "INV-2026-001")

**Generation Pattern**:
- Format: `INV-{year}-{sequence}` where sequence increments per tenant per year
- Example: INV-2026-001, INV-2026-002, INV-2027-001

**Rationale**: Human-readable invoice numbers, prevents collisions across tenants.

---

## Enumerations

### AccountType
- `Organization`: Corporate or institutional account (e.g., rehab centers, hospitals)
- `Individual`: Personal account (e.g., passengers, guardians)

### AccountStatus
- `Active`: Account can receive new transactions
- `Inactive`: Account cannot receive new transactions (historical data still accessible)

### LedgerAccount
- `AccountsReceivable`: Asset account (debit increases balance)
- `ServiceRevenue`: Revenue account (credit increases revenue)
- `Cash`: Asset account (debit increases cash)

**Double-Entry Patterns**:
- **Ride Charge**: Debit AR, Credit Revenue
- **Payment**: Debit Cash, Credit AR

### TransactionType
- `RideCharge`: Transaction originates from completed ride
- `Payment`: Transaction originates from payment received

---

## Entity Relationships (ERD)

```
┌─────────────────┐
│  Account (AR)   │
├─────────────────┤
│ + Id            │
│ + Name          │
│ + Type          │◄──────────────┐
│ + Status        │               │
│ + TenantId      │               │ AccountId (FK)
│ + Currency      │               │
│ + CreatedAt     │               │
└─────────────────┘               │
        │                         │
        │ 1:N                     │
        │                         │
        ▼                         │
┌─────────────────┐        ┌─────────────────┐
│ LedgerEntry (E) │        │  Invoice (AR)   │
├─────────────────┤        ├─────────────────┤
│ + Id            │        │ + Id            │
│ + AccountId     │────────┤ + InvoiceNumber │
│ + LedgerAccount │        │ + AccountId     │
│ + DebitAmount   │◄───┐   │ + PeriodStart   │
│ + CreditAmount  │    │   │ + PeriodEnd     │
│ + SourceType    │    │   │ + Subtotal      │
│ + SourceRefId   │    │   │ + PaymentsApp   │
│ + TenantId      │    │   │ + Outstanding   │
│ + CreatedAt     │    │   │ + GeneratedAt   │
└─────────────────┘    │   └─────────────────┘
                       │           │
                       │           │ 1:N
                       │           │
                       │           ▼
                       │   ┌─────────────────┐
                       │   │InvoiceLineItem(E)│
                       │   ├─────────────────┤
                       │   │ + Id            │
                       │   │ + InvoiceId     │
                       └───┤ + LedgerEntryId │
                           │ + RideId        │
                           │ + ServiceDate   │
                           │ + Amount        │
                           └─────────────────┘

Legend:
  AR = Aggregate Root
  E  = Entity
  FK = Foreign Key
```

---

## Database Schema (PostgreSQL)

### Tables

**accounting.accounts**
```sql
CREATE TABLE accounting.accounts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(200) NOT NULL,
    account_type VARCHAR(20) NOT NULL CHECK (account_type IN ('Organization', 'Individual')),
    status VARCHAR(20) NOT NULL CHECK (status IN ('Active', 'Inactive')),
    tenant_id UUID NOT NULL,
    currency CHAR(3) NOT NULL DEFAULT 'USD' CHECK (currency = 'USD'),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(100) NOT NULL,
    modified_at TIMESTAMPTZ,
    modified_by VARCHAR(100),
    
    CONSTRAINT uq_accounts_tenant_name UNIQUE (tenant_id, name)
);

CREATE INDEX ix_accounts_tenant_id ON accounting.accounts (tenant_id);
CREATE INDEX ix_accounts_status ON accounting.accounts (status) WHERE status = 'Active';
```

**accounting.ledger_entries**
```sql
CREATE TABLE accounting.ledger_entries (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES accounting.accounts(id),
    ledger_account VARCHAR(30) NOT NULL CHECK (ledger_account IN ('AccountsReceivable', 'ServiceRevenue', 'Cash')),
    debit_amount NUMERIC(19,4) NOT NULL,
    credit_amount NUMERIC(19,4) NOT NULL,
    source_type VARCHAR(20) NOT NULL CHECK (source_type IN ('RideCharge', 'Payment')),
    source_reference_id VARCHAR(100) NOT NULL,
    tenant_id UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(100) NOT NULL,
    description VARCHAR(500),
    row_version BIGINT NOT NULL DEFAULT 0,
    
    CONSTRAINT ck_ledger_entries_single_side CHECK (
        (debit_amount > 0 AND credit_amount = 0) OR 
        (credit_amount > 0 AND debit_amount = 0)
    ),
    CONSTRAINT uq_ledger_entries_idempotency UNIQUE (account_id, source_reference_id, source_type)
);

CREATE INDEX ix_ledger_entries_account_id ON accounting.ledger_entries (account_id);
CREATE INDEX ix_ledger_entries_created_at ON accounting.ledger_entries (created_at);
CREATE INDEX ix_ledger_entries_tenant_id ON accounting.ledger_entries (tenant_id);
CREATE INDEX ix_ledger_entries_source_ref ON accounting.ledger_entries (source_reference_id);
```

**accounting.invoices**
```sql
CREATE TABLE accounting.invoices (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    invoice_number VARCHAR(50) NOT NULL,
    account_id UUID NOT NULL REFERENCES accounting.accounts(id),
    billing_period_start TIMESTAMPTZ NOT NULL,
    billing_period_end TIMESTAMPTZ NOT NULL,
    tenant_id UUID NOT NULL,
    subtotal NUMERIC(19,4) NOT NULL,
    payments_applied NUMERIC(19,4) NOT NULL,
    outstanding_balance NUMERIC(19,4) NOT NULL,
    generated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    generated_by VARCHAR(100) NOT NULL,
    
    CONSTRAINT uq_invoices_number_tenant UNIQUE (tenant_id, invoice_number),
    CONSTRAINT ck_invoices_period CHECK (billing_period_end >= billing_period_start)
);

CREATE INDEX ix_invoices_tenant_id ON accounting.invoices (tenant_id);
CREATE INDEX ix_invoices_account_id ON accounting.invoices (account_id);
CREATE INDEX ix_invoices_generated_at ON accounting.invoices (generated_at);
```

**accounting.invoice_line_items**
```sql
CREATE TABLE accounting.invoice_line_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    invoice_id UUID NOT NULL REFERENCES accounting.invoices(id) ON DELETE CASCADE,
    ledger_entry_id UUID NOT NULL REFERENCES accounting.ledger_entries(id),
    ride_id VARCHAR(100) NOT NULL,
    service_date TIMESTAMPTZ NOT NULL,
    amount NUMERIC(19,4) NOT NULL,
    description VARCHAR(500),
    
    CONSTRAINT uq_invoice_line_items_ledger UNIQUE (invoice_id, ledger_entry_id)
);

CREATE INDEX ix_invoice_line_items_invoice_id ON accounting.invoice_line_items (invoice_id);
CREATE INDEX ix_invoice_line_items_ledger_entry_id ON accounting.invoice_line_items (ledger_entry_id);
```

---

## Indexes Strategy

### Primary Indexes (Automatically Created)
- All `id` columns (PRIMARY KEY)
- All UNIQUE constraints create indexes

### Performance Indexes

**High-Priority Indexes** (Required for Sub-100ms Queries):
1. `ix_ledger_entries_account_id` - Balance calculation (SUM debit/credit WHERE account_id = X)
2. `ix_ledger_entries_created_at` - Statement generation (ORDER BY created_at)
3. `ix_accounts_tenant_id` - Multi-tenant isolation filter
4. `ix_ledger_entries_tenant_id` - Multi-tenant isolation filter
5. `ix_invoices_account_id` - Fetch invoices for account

**Idempotency Indexes**:
6. `uq_ledger_entries_idempotency` (account_id, source_reference_id, source_type) - Prevent duplicate ride charges

**Partial Indexes** (Optimize WHERE Clauses):
7. `ix_accounts_status` WHERE status = 'Active' - Filter active accounts only

### Query Patterns Optimized
- **Balance Query**: `SELECT SUM(debit_amount) - SUM(credit_amount) FROM ledger_entries WHERE account_id = ? AND tenant_id = ?` ⚡ Uses `ix_ledger_entries_account_id`, `ix_ledger_entries_tenant_id`
- **Statement Query**: `SELECT * FROM ledger_entries WHERE account_id = ? AND created_at BETWEEN ? AND ? ORDER BY created_at` ⚡ Uses `ix_ledger_entries_account_id`, `ix_ledger_entries_created_at`
- **Invoice Generation**: `SELECT * FROM ledger_entries WHERE account_id = ? AND source_type = 'RideCharge' AND created_at BETWEEN ? AND ?` ⚡ Uses `ix_ledger_entries_account_id`, `ix_ledger_entries_created_at`
- **Idempotency Check**: `SELECT 1 FROM ledger_entries WHERE account_id = ? AND source_reference_id = ? AND source_type = 'RideCharge'` ⚡ Uses `uq_ledger_entries_idempotency`

---

## Domain vs Persistence Entity Mapping

Per Constitutional Principle VII, domain entities are separate from persistence entities.

| Domain Entity | Persistence Entity | Mapper | Notes |
|--------------|-------------------|--------|-------|
| Account | AccountEntity | AccountMapper | Status mapped to string in DB, enum in domain |
| LedgerEntry | LedgerEntryEntity | LedgerMapper | Money converts to decimal, enums to strings |
| Invoice | InvoiceEntity | InvoiceMapper | Calculated fields (Subtotal, Outstanding) stored for performance |
| InvoiceLineItem | InvoiceLineItemEntity | InvoiceLineItemMapper | Simple POCO mapping |

**Mapping Responsibility**: Infrastructure layer only. Domain layer never references EF Core types.

---

## Summary

This data model supports all 31 functional requirements from spec.md:
- ✅ Account management (FR-001 to FR-005)
- ✅ Double-entry ledger operations (FR-006 to FR-013)
- ✅ Balance calculation (FR-014 to FR-016)
- ✅ Invoice generation (FR-017 to FR-023)
- ✅ Account statements (FR-024 to FR-025)
- ✅ Audit trail (FR-026 to FR-027)
- ✅ Data integrity (FR-028 to FR-031)

**Constitutional Compliance**: Follows all 7 NON-NEGOTIABLE principles including DDD aggregates, value objects, entity separation, PostgreSQL naming conventions, and optimized indexes.
