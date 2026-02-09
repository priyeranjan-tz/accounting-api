# ADR-001: Double-Entry Accounting Implementation

**Status**: Accepted  
**Date**: 2026-02-05  
**Deciders**: Engineering Team, Finance Team  
**Technical Story**: Accounting Ledger System - User Story 1 (Ledger Entries)

## Context

The accounting system needs to track ride charges and payments for customer accounts in a financially accurate and auditable manner. We must decide how to implement the core accounting logic.

## Decision

Use **double-entry bookkeeping** with separate Accounts Receivable and Service Revenue ledger accounts.

### Implementation Details

Every transaction creates **exactly 2 ledger entries**:

**Ride Charge**:
```
DR Accounts Receivable  $25.50  (increases amount customer owes)
CR Service Revenue      $25.50  (recognizes earned revenue)
```

**Payment Received**:
```
DR Cash                 $25.50  (increases cash on hand)
CR Accounts Receivable  $25.50  (reduces amount customer owes)
```

**Enforcement Mechanisms**:
1. Repository validates `entries.Count == 2` before persistence
2. Total debits must equal total credits (`SUM(debits) == SUM(credits)`)
3. Database transactions wrap both entries (ACID compliance)

**Balance Calculation**:
```csharp
Balance = SUM(AR Debits) - SUM(AR Credits)
```
- Customer owes $100 → Balance = +$100  
- Customer paid $60 → Balance = +$40  
- Customer has $20 credit → Balance = -$20

## Consequences

### Positive

- **GAAP Compliance**: Industry-standard accounting principles
- **Audit Trail**: Complete transaction history with balancing proof
- **Financial Accuracy**: Debits always equal credits (self-balancing)
- **Revenue Recognition**: Service revenue tracked separately from receivables
- **Regulatory Compliance**: Auditors expect double-entry systems

### Negative

- **Complexity**: 2x ledger writes per transaction (vs single-entry)
- **Performance**: More database rows (mitigated by indexes)
- **Developer Training**: Team must understand accounting principles

### Mitigation

- Encapsulate double-entry logic in domain methods (`LedgerEntry.Debit()`, `LedgerEntry.Credit()`)
- Repository enforces invariants (cannot save unbalanced transactions)
- Comprehensive tests validate double-entry constraints

## Alternatives Considered

### Alternative 1: Single-Entry Ledger

**Description**: Store only net change per transaction (e.g., +$25 for charge, -$25 for payment)

**Rejected Because**:
- No separation of revenue from receivables
- Difficult to track transaction types
- Not auditable by accountants
- Violates GAAP principles

### Alternative 2: Event Sourcing Only

**Description**: Store domain events (RideCharged, PaymentReceived) and derive balances

**Rejected Because**:
- Requires rebuilding state from events (slow balance queries)
- No industry-standard accounting trail
- Difficult for finance team to audit
- Over-engineered for this domain

## Related Decisions

- **ADR-002**: NUMERIC(19,4) precision (financial calculations)
- **ADR-003**: Immutable ledger entries (audit compliance)

## References

- [Double-Entry Bookkeeping - Wikipedia](https://en.wikipedia.org/wiki/Double-entry_bookkeeping)
- [GAAP Revenue Recognition Principles](https://www.fasb.org/)
- Martin Fowler: [Accounting Patterns](https://martinfowler.com/eaaDev/AccountingEntry.html)
