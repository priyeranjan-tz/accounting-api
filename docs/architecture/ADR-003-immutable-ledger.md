# ADR-003: Immutable Ledger Pattern

**Status**: Accepted  
**Date**: 2026-02-05  
**Deciders**: Engineering Team, Compliance Team  
**Technical Story**: Accounting Ledger System - Audit Trail & Compliance

## Context

Financial regulations (SOX, GAAP) require complete audit trails for accounting transactions. Once a ledger entry is recorded, it must be tamper-proof and traceable. We need to decide how to enforce immutability.

## Decision

Implement **append-only ledger entries** with database-level immutability constraints.

### Implementation Details

**1. Application Layer** (Soft Delete Only):
```csharp
// No UPDATE or DELETE methods in ILedgerRepository
public interface ILedgerRepository
{
    Task<Guid> AppendEntriesAsync(IEnumerable<LedgerEntry> entries, ...);
    Task<Money> GetAccountBalanceAsync(AccountId accountId, ...);
    // NO: UpdateEntryAsync(), DeleteEntryAsync()
}
```

**2. Database Layer** (Hard Enforcement):
```sql
-- PostgreSQL trigger to prevent UPDATEs and DELETEs
CREATE OR REPLACE FUNCTION prevent_ledger_modifications()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Ledger entries are immutable. Cannot UPDATE or DELETE.';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER immutable_ledger_entries
    BEFORE UPDATE OR DELETE ON ledger_entries
    FOR EACH ROW
    EXECUTE FUNCTION prevent_ledger_modifications();
```

**3. Corrections via Reversing Entries**:
```csharp
// Wrong charge: $100 instead of $50
// 1. Original (incorrect) entry
DR Accounts Receivable  $100
CR Service Revenue      $100

// 2. Reversing entry (cancels original)
DR Service Revenue      $100
CR Accounts Receivable  $100

// 3. Correct entry
DR Accounts Receivable  $50
CR Service Revenue      $50

// Net effect: Customer owes $50 (original $100 - reversal $100 + correct $50)
```

**4. Audit Metadata**:
```csharp
public class LedgerEntry
{
    public DateTime CreatedAt { get; private set; }  // When recorded
    public string CreatedBy { get; private set; }    // Who recorded it
    public Guid RowVersion { get; private set; }     // Optimistic concurrency token
}
```

## Consequences

### Positive

- **Regulatory Compliance**: SOX Section 404 requires immutable audit trails
- **Data Integrity**: No accidental or malicious modification of historical data
- **Forensic Analysis**: Complete transaction history for fraud investigation
- **Reversibility**: Errors corrected via reversing entries (standard accounting practice)
- **Database Backup**: Immutable data compresses better and is easier to archive

### Negative

- **Storage Growth**: Cannot delete old entries (requires archival strategy)
- **Correction Complexity**: Reversing entries add rows (vs simple UPDATE)
- **Developer Friction**: Common CRUD patterns don't apply

### Mitigation

- **Archival Policy**: Move entries >7 years to cold storage (legal retention period)
- **Soft Delete for Non-Ledger Tables**: Accounts/Invoices can be "deleted" (status = Inactive)
- **Developer Training**: Document reversing entry pattern in team handbook

## Alternatives Considered

### Alternative 1: Soft Delete with IsDeleted Flag

**Description**: Add `is_deleted` column, mark entries as deleted instead of truly deleting

**Rejected Because**:
- Still allows UPDATE operations (not truly immutable)
- Developers could accidentally update `is_deleted` flag back to false
- Doesn't prevent modification of amounts or dates
- Not compliant with strict audit requirements

### Alternative 2: Event Sourcing Only

**Description**: Store domain events (LedgerEntryCreated) and rebuild state from events

**Rejected Because**:
- Overhead of event replay for balance queries
- Complexity of projection rebuilds
- Immutable event log ≠ immutable ledger (events could be replayed differently)
- Over-engineered for this domain (ledger entries are already event-like)

### Alternative 3: Blockchain / Merkle Tree

**Description**: Use cryptographic hashing to detect tampering

**Rejected Because**:
- Extreme overkill for internal accounting system
- No regulatory requirement for blockchain
- Database triggers sufficient for internal audit compliance
- Performance overhead of hash calculations

## Related Decisions

- **ADR-001**: Double-entry accounting (reversing entries maintain double-entry balance)
- **ADR-002**: NUMERIC precision (immutable entries require precise values on first write)
- **ADR-005**: Outbox pattern (immutable events published from ledger)

## Implementation Checklist

- [x] Remove UPDATE/DELETE from `ILedgerRepository` interface
- [x] Create PostgreSQL trigger `prevent_ledger_modifications()`
- [x] Add `CreatedAt`, `CreatedBy`, `RowVersion` audit columns
- [x] Document reversing entry pattern in developer guide
- [x] Implement archival strategy for >7 year old entries (todo: future story)

## References

- [SOX Section 404 - Internal Controls](https://www.sec.gov/rules/final/33-8238.htm)
- [GAAP Audit Trail Requirements](https://www.fasb.org/)
- Martin Fowler: [Temporal Patterns](https://martinfowler.com/eaaDev/TemporalProperty.html)
- [PostgreSQL Triggers Documentation](https://www.postgresql.org/docs/17/triggers.html)
