using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Accounting.Domain.ValueObjects;
using Accounting.Infrastructure.Persistence.Entities;
using System.Reflection;

namespace Accounting.Infrastructure.Persistence.Mappers;

/// <summary>
/// Mapper between LedgerEntry domain entity and LedgerEntryEntity persistence model
/// </summary>
public static class LedgerMapper
{
    /// <summary>
    /// Maps persistence entity to domain entity
    /// </summary>
    public static LedgerEntry MapToDomain(LedgerEntryEntity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // Use reflection to create instance via private constructor
        var ledgerEntry = (LedgerEntry)Activator.CreateInstance(
            typeof(LedgerEntry),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new object[]
            {
                entity.Id,
                new AccountId(entity.AccountId),
                (LedgerAccount)entity.LedgerAccount,
                new Money(entity.DebitAmount),
                new Money(entity.CreditAmount),
                (TransactionType)entity.SourceType,
                entity.SourceReferenceId,
                entity.TenantId,
                entity.CreatedAt,
                entity.CreatedBy,
                entity.Description
            },
            null)!;

        return ledgerEntry;
    }

    /// <summary>
    /// Maps domain entity to persistence entity
    /// </summary>
    public static LedgerEntryEntity MapToPersistence(LedgerEntry ledgerEntry)
    {
        if (ledgerEntry == null)
            throw new ArgumentNullException(nameof(ledgerEntry));

        return new LedgerEntryEntity
        {
            Id = ledgerEntry.Id,
            AccountId = ledgerEntry.AccountId.Value,
            LedgerAccount = (int)ledgerEntry.LedgerAccount,
            DebitAmount = ledgerEntry.DebitAmount.Amount,
            CreditAmount = ledgerEntry.CreditAmount.Amount,
            SourceType = (int)ledgerEntry.SourceType,
            SourceReferenceId = ledgerEntry.SourceReferenceId,
            TenantId = ledgerEntry.TenantId,
            CreatedAt = ledgerEntry.CreatedAt,
            CreatedBy = ledgerEntry.CreatedBy,
            Description = ledgerEntry.Description
        };
    }
}
