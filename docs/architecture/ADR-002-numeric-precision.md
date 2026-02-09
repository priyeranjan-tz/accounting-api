# ADR-002: NUMERIC(19,4) Decimal Precision

**Status**: Accepted  
**Date**: 2026-02-05  
**Deciders**: Engineering Team, Finance Team  
**Technical Story**: Accounting Ledger System - Database Schema Design

## Context

The system handles monetary values for ride charges, payments, and invoices. We must decide on the data type and precision for all financial calculations to avoid rounding errors and ensure accuracy.

## Decision

Use PostgreSQL **NUMERIC(19,4)** for all monetary columns.

### Rationale

- **19 total digits**: Supports values up to $999,999,999,999,999.9999 (quadrillion range)
- **4 decimal places**: Industry standard for currency (matches most international currencies)
- **Exact precision**: No floating-point rounding errors (critical for finance)

### Implementation

**Database Schema**:
```sql
CREATE TABLE ledger_entries (
    debit_amount  NUMERIC(19,4) NOT NULL,
    credit_amount NUMERIC(19,4) NOT NULL,
    ...
);
```

**C# Domain Model**:
```csharp
public record Money
{
    private readonly decimal _amount;

    public Money(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentException("Money cannot be negative");
        
        // Round to 4 decimal places
        _amount = Math.Round(amount, 4, MidpointRounding.AwayFromZero);
    }

    public decimal Amount => _amount;
}
```

**EF Core Configuration**:
```csharp
builder.Property(e => e.DebitAmount)
    .HasColumnType("NUMERIC(19,4)")
    .HasPrecision(19, 4);
```

## Consequences

### Positive

- **No Rounding Errors**: Exact decimal arithmetic (critical for accounting)
- **Regulatory Compliance**: Meets SOX, GAAP precision requirements
- **Cross-Currency Support**: 4 decimals handle most world currencies (JPY = 0 decimals, BTC = 8 decimals)
- **Audit Trail**: Balances always match to the cent

### Negative

- **Storage Overhead**: NUMERIC(19,4) uses 8-16 bytes (vs 4 bytes for FLOAT)
- **Performance**: Slightly slower than floating-point arithmetic (acceptable trade-off)
- **Database Size**: Ledger table grows faster (mitigated by archival strategy)

### Mitigation

- Use database indexes on monetary columns for fast range queries
- Implement archival strategy for ledger entries older than 7 years
- Monitor database size and performance metrics

## Alternatives Considered

### Alternative 1: FLOAT / DOUBLE

**Description**: Use floating-point data types for monetary values

**Rejected Because**:
- Rounding errors accumulate (e.g., 0.1 + 0.2 ≠ 0.3 in binary)
- Not suitable for financial calculations (industry anti-pattern)
- Violates accounting standards

**Example Problem**:
```csharp
decimal charge = 123.45m;
decimal payment = 123.45m;
decimal balance = charge - payment; // 0.0000 ✓

double charge = 123.45;
double payment = 123.45;
double balance = charge - payment; // 0.000000000000014210... ✗
```

### Alternative 2: NUMERIC(38,10)

**Description**: Use maximum precision (38 digits, 10 decimals)

**Rejected Because**:
- Overkill for currency (4 decimals standard)
- Larger storage footprint (12-17 bytes)
- No real-world benefit (who needs trillions of dollars to 10 decimal places?)

### Alternative 3: Store Cents as BIGINT

**Description**: Store amounts in smallest unit (e.g., $12.34 → 1234 cents)

**Rejected Because**:
- Requires conversion logic in application layer
- Harder to read database values (debugging complexity)
- Doesn't handle fractional cents (gas tax: $1.2345/gallon)
- Currency-specific (doesn't work for JPY yen, BTC satoshis)

## Related Decisions

- **ADR-001**: Double-entry accounting (requires precise balance calculations)
- **ADR-003**: Immutable ledger entries (financial values never change once recorded)

## References

- [PostgreSQL NUMERIC Documentation](https://www.postgresql.org/docs/17/datatype-numeric.html)
- [Martin Fowler: Currency Handling](https://martinfowler.com/bliki/MoneyType.html)
- [IEEE 754 Floating-Point Limitations](https://docs.oracle.com/cd/E19957-01/806-3568/ncg_goldberg.html)
- [.NET Decimal Type](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/floating-point-numeric-types#characteristics-of-the-floating-point-types)
