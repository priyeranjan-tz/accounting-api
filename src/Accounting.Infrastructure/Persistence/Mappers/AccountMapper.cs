using Accounting.Domain.Aggregates;
using Accounting.Infrastructure.Persistence.Entities;
using System.Reflection;

namespace Accounting.Infrastructure.Persistence.Mappers;

/// <summary>
/// Mapper between Account domain aggregate and AccountEntity persistence model
/// </summary>
public static class AccountMapper
{
    /// <summary>
    /// Maps persistence entity to domain aggregate
    /// </summary>
    public static Account MapToDomain(AccountEntity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // Use reflection to create instance via private constructor and set properties
        var account = (Account)Activator.CreateInstance(typeof(Account), nonPublic: true)!;

        SetProperty(account, nameof(Account.Id), entity.Id);
        SetProperty(account, nameof(Account.Name), entity.Name);
        SetProperty(account, nameof(Account.Type), entity.Type);
        SetProperty(account, nameof(Account.Status), entity.Status);
        SetProperty(account, nameof(Account.InvoiceFrequency), entity.InvoiceFrequency);
        SetProperty(account, nameof(Account.TenantId), entity.TenantId);
        SetProperty(account, nameof(Account.Currency), entity.Currency);
        SetProperty(account, nameof(Account.CreatedAt), entity.CreatedAt);
        SetProperty(account, nameof(Account.CreatedBy), entity.CreatedBy);
        SetProperty(account, nameof(Account.ModifiedAt), entity.ModifiedAt);
        SetProperty(account, nameof(Account.ModifiedBy), entity.ModifiedBy);

        return account;
    }

    /// <summary>
    /// Maps domain aggregate to persistence entity
    /// </summary>
    public static AccountEntity MapToPersistence(Account account)
    {
        if (account == null)
            throw new ArgumentNullException(nameof(account));

        return new AccountEntity
        {
            Id = account.Id,
            Name = account.Name,
            Type = account.Type,
            Status = account.Status,
            InvoiceFrequency = account.InvoiceFrequency,
            TenantId = account.TenantId,
            Currency = account.Currency,
            CreatedAt = account.CreatedAt,
            CreatedBy = account.CreatedBy,
            ModifiedAt = account.ModifiedAt,
            ModifiedBy = account.ModifiedBy
        };
    }

    private static void SetProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
            throw new InvalidOperationException($"Property '{propertyName}' not found on type '{target.GetType().Name}'");

        property.SetValue(target, value);
    }
}
