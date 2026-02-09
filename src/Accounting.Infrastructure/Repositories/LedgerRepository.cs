using Accounting.Domain.Entities;
using Accounting.Domain.Interfaces;
using Accounting.Domain.ValueObjects;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.Persistence.DbContext;
using Accounting.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Accounting.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for ledger entry operations using Entity Framework Core.
/// Provides access to the append-only ledger with double-entry accounting enforcement.
/// </summary>
public class LedgerRepository : ILedgerRepository
{
    private readonly AccountingDbContext _dbContext;
    private static readonly Meter Meter = new("Accounting.API", "1.0.0");
    private static readonly Histogram<double> LedgerAppendDuration = Meter.CreateHistogram<double>(
        "ledger_append_duration_ms",
        "ms",
        "Duration of ledger append operations in milliseconds");

    public LedgerRepository(AccountingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> AppendEntriesAsync(
        IEnumerable<LedgerEntry> entries,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var entryList = entries.ToList();
        try
        {

            // CRITICAL: Enforce double-entry accounting - must have exactly 2 entries
            if (entryList.Count != 2)
            {
                throw new InvalidOperationException(
                    $"Invalid ledger transaction: expected exactly 2 entries (debit + credit), got {entryList.Count}");
            }

            // CRITICAL: Validate double-entry balance - debits must equal credits
            var totalDebits = entryList.Sum(e => e.DebitAmount.Amount);
            var totalCredits = entryList.Sum(e => e.CreditAmount.Amount);

            if (totalDebits != totalCredits)
            {
                throw new InvalidOperationException(
                    $"Invalid ledger transaction: debits ({totalDebits:C}) do NOT equal credits ({totalCredits:C}). " +
                    $"Double-entry accounting requires balanced entries.");
            }

            // Map domain entities to persistence entities
            var persistenceEntries = entryList.Select(MapToPersistence).ToList();

            // Add to DbContext
            await _dbContext.LedgerEntries.AddRangeAsync(persistenceEntries, cancellationToken);

            try
            {
                // Save changes - will throw DbUpdateException if idempotency constraint violated
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) 
            {
                // 23505 = unique_violation (duplicate key on account_id + source_reference_id)
                // This means a transaction with this source reference has already been posted (idempotency protection)
                var sourceRef = entryList.FirstOrDefault()?.SourceReferenceId ?? "unknown";
                var accountId = entryList.FirstOrDefault()?.AccountId.Value.ToString() ?? "unknown";
                throw new InvalidOperationException(
                    $"Duplicate transaction detected: SourceReferenceId '{sourceRef}' has already been posted to account '{accountId}'. " +
                    $"Each transaction must have a unique SourceReferenceId (use a GUID or unique identifier, not a description).",
                    ex);
            }

            // Return transaction ID (use first entry's ID as transaction identifier)
            return persistenceEntries.First().Id;
        }
        finally
        {
            stopwatch.Stop();
            var transactionType = entryList.FirstOrDefault()?.SourceType.ToString() ?? "Unknown";
            var tenantId = entryList.FirstOrDefault()?.TenantId.ToString() ?? "Unknown";
            
            LedgerAppendDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("transaction_type", transactionType),
                new KeyValuePair<string, object?>("tenant_id", tenantId));
        }
    }

    public async Task<Money> GetAccountBalanceAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default)
    {
        // Balance calculation using double-entry accounting:
        // For Accounts Receivable entries:
        //   - Debits increase balance (charges)
        //   - Credits decrease balance (payments)
        // Balance = SUM(debits) - SUM(credits) for AR account

        var arEntries = await _dbContext.LedgerEntries
            .Where(e => e.AccountId == accountId.Value)
            .Where(e => e.LedgerAccount == (int)LedgerAccount.AccountsReceivable)
            .ToListAsync(cancellationToken);

        var totalDebits = arEntries.Sum(e => e.DebitAmount);
        var totalCredits = arEntries.Sum(e => e.CreditAmount);

        var balance = totalDebits - totalCredits;

        return new Money(balance);
    }

    public async Task<Money> GetAccountBalanceAsync(
        AccountId accountId,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        // Balance as of a specific date - only include entries before/at the specified date
        var arEntries = await _dbContext.LedgerEntries
            .Where(e => e.AccountId == accountId.Value)
            .Where(e => e.LedgerAccount == (int)LedgerAccount.AccountsReceivable)
            .Where(e => e.CreatedAt <= asOfDate)
            .ToListAsync(cancellationToken);

        var totalDebits = arEntries.Sum(e => e.DebitAmount);
        var totalCredits = arEntries.Sum(e => e.CreditAmount);

        var balance = totalDebits - totalCredits;

        return new Money(balance);
    }

    public async Task<List<LedgerEntry>> GetByAccountIdAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<bool> RideAlreadyChargedAsync(
        AccountId accountId,
        RideId rideId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.LedgerEntries
            .AnyAsync(
                e => e.AccountId == accountId.Value &&
                     e.SourceReferenceId == rideId.Value &&
                     e.SourceType == (int)TransactionType.RideCharge,
                cancellationToken);
    }

    public async Task<bool> PaymentAlreadyRecordedAsync(
        string paymentReferenceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.LedgerEntries
            .AnyAsync(
                e => e.SourceReferenceId == paymentReferenceId &&
                     e.SourceType == (int)TransactionType.Payment,
                cancellationToken);
    }

    /// <summary>
    /// Maps domain LedgerEntry to persistence LedgerEntryEntity.
    /// </summary>
    private static LedgerEntryEntity MapToPersistence(LedgerEntry domain)
    {
        return new LedgerEntryEntity
        {
            Id = domain.Id,
            AccountId = domain.AccountId.Value,
            LedgerAccount = (int)domain.LedgerAccount,
            DebitAmount = domain.DebitAmount.Amount,
            CreditAmount = domain.CreditAmount.Amount,
            SourceType = (int)domain.SourceType,
            SourceReferenceId = domain.SourceReferenceId,
            TenantId = domain.TenantId,
            CreatedAt = domain.CreatedAt,
            CreatedBy = domain.CreatedBy,
            Description = domain.Description
        };
    }

    /// <summary>
    /// Maps persistence LedgerEntryEntity to domain LedgerEntry.
    /// Note: Uses reflection to bypass private constructor - required for reconstitution from database.
    /// </summary>
    private static LedgerEntry MapToDomain(LedgerEntryEntity entity)
    {
        // Since LedgerEntry has private constructor and factory methods,
        // we use the appropriate factory based on whether it's a debit or credit
        if (entity.DebitAmount > 0)
        {
            return LedgerEntry.Debit(
                accountId: new AccountId(entity.AccountId),
                ledgerAccount: (LedgerAccount)entity.LedgerAccount,
                amount: new Money(entity.DebitAmount),
                sourceType: (TransactionType)entity.SourceType,
                sourceReferenceId: entity.SourceReferenceId,
                tenantId: entity.TenantId,
                createdBy: entity.CreatedBy,
                description: entity.Description);
        }
        else
        {
            return LedgerEntry.Credit(
                accountId: new AccountId(entity.AccountId),
                ledgerAccount: (LedgerAccount)entity.LedgerAccount,
                amount: new Money(entity.CreditAmount),
                sourceType: (TransactionType)entity.SourceType,
                sourceReferenceId: entity.SourceReferenceId,
                tenantId: entity.TenantId,
                createdBy: entity.CreatedBy,
                description: entity.Description);
        }
    }
}
