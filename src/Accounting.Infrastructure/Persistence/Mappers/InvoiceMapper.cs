using Accounting.Domain.Aggregates;
using Accounting.Domain.ValueObjects;
using Accounting.Infrastructure.Persistence.Entities;
using System.Reflection;

namespace Accounting.Infrastructure.Persistence.Mappers;

/// <summary>
/// Mapper between Invoice domain aggregate and InvoiceEntity persistence model
/// </summary>
public static class InvoiceMapper
{
    /// <summary>
    /// Maps persistence entity to domain aggregate
    /// </summary>
    public static Invoice MapToDomain(InvoiceEntity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // Use reflection to create instance via private constructor
        var invoice = (Invoice)Activator.CreateInstance(typeof(Invoice), nonPublic: true)!;

        SetProperty(invoice, nameof(Invoice.Id), entity.Id);
        SetProperty(invoice, nameof(Invoice.InvoiceNumber), entity.InvoiceNumber);
        SetProperty(invoice, nameof(Invoice.AccountId), entity.AccountId);
        SetProperty(invoice, nameof(Invoice.TenantId), entity.TenantId);
        SetProperty(invoice, nameof(Invoice.BillingPeriodStart), entity.BillingPeriodStart);
        SetProperty(invoice, nameof(Invoice.BillingPeriodEnd), entity.BillingPeriodEnd);
        SetProperty(invoice, nameof(Invoice.IssueDate), entity.IssueDate);
        SetProperty(invoice, nameof(Invoice.DueDate), entity.DueDate);
        SetProperty(invoice, nameof(Invoice.TotalAmount), new Money(entity.TotalAmount));
        SetProperty(invoice, nameof(Invoice.Currency), entity.Currency);
        SetProperty(invoice, nameof(Invoice.CreatedAt), entity.CreatedAt);
        SetProperty(invoice, nameof(Invoice.CreatedBy), entity.CreatedBy);

        // Map line items using reflection to access private list
        var lineItemsField = typeof(Invoice).GetField("_lineItems", BindingFlags.NonPublic | BindingFlags.Instance);
        if (lineItemsField != null)
        {
            var lineItemsList = (List<InvoiceLineItem>)lineItemsField.GetValue(invoice)!;
            foreach (var lineItemEntity in entity.LineItems)
            {
                var lineItem = MapLineItemToDomain(lineItemEntity);
                lineItemsList.Add(lineItem);
            }
        }

        return invoice;
    }

    /// <summary>
    /// Maps domain aggregate to persistence entity
    /// </summary>
    public static InvoiceEntity MapToPersistence(Invoice invoice)
    {
        if (invoice == null)
            throw new ArgumentNullException(nameof(invoice));

        return new InvoiceEntity
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            AccountId = invoice.AccountId,
            TenantId = invoice.TenantId,
            BillingPeriodStart = invoice.BillingPeriodStart,
            BillingPeriodEnd = invoice.BillingPeriodEnd,
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            TotalAmount = invoice.TotalAmount.Amount,
            Currency = invoice.Currency,
            CreatedAt = invoice.CreatedAt,
            CreatedBy = invoice.CreatedBy,
            LineItems = invoice.LineItems.Select(MapLineItemToPersistence).ToList()
        };
    }

    /// <summary>
    /// Maps persistence line item entity to domain entity
    /// </summary>
    private static InvoiceLineItem MapLineItemToDomain(InvoiceLineItemEntity entity)
    {
        // Use reflection to create instance via internal constructor
        var lineItem = (InvoiceLineItem)Activator.CreateInstance(
            typeof(InvoiceLineItem),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new object[] { entity.Id, entity.InvoiceId, entity.RideId, entity.RideDate, entity.Description, new Money(entity.Amount) },
            null)!;

        return lineItem;
    }

    /// <summary>
    /// Maps domain line item entity to persistence entity
    /// </summary>
    private static InvoiceLineItemEntity MapLineItemToPersistence(InvoiceLineItem lineItem)
    {
        return new InvoiceLineItemEntity
        {
            Id = lineItem.Id,
            InvoiceId = lineItem.InvoiceId,
            RideId = lineItem.RideId,
            RideDate = lineItem.RideDate,
            Description = lineItem.Description,
            Amount = lineItem.Amount.Amount
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
