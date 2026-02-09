using Accounting.Domain.Enums;
using Accounting.Domain.Interfaces;
using Accounting.Infrastructure.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Infrastructure.Services;

/// <summary>
/// Implementation of ledger query service using Entity Framework Core
/// </summary>
public class LedgerQueryService : ILedgerQueryService
{
    private readonly AccountingDbContext _dbContext;

    public LedgerQueryService(AccountingDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<RideChargeDto>> GetUnbilledRideChargesAsync(
        Guid accountId,
        Guid tenantId,
        DateTime billingPeriodStart,
        DateTime billingPeriodEnd,
        List<Guid> excludeRideIds,
        CancellationToken cancellationToken)
    {
        var rideCharges = await _dbContext.LedgerEntries
            .Where(e => e.TenantId == tenantId)
            .Where(e => e.AccountId == accountId)
            .Where(e => e.SourceType == (int)TransactionType.RideCharge)
            .Where(e => e.CreatedAt >= billingPeriodStart && e.CreatedAt < billingPeriodEnd)
            .Where(e => e.DebitAmount > 0) // Only debit entries (charges to AR account)
            .Select(e => new RideChargeDto(
                Guid.Parse(e.SourceReferenceId),
                e.CreatedAt,
                e.Description ?? "Ride charge",
                e.DebitAmount))
            .ToListAsync(cancellationToken);

        // Filter out already invoiced rides
        return rideCharges
            .Where(rc => !excludeRideIds.Contains(rc.RideId))
            .ToList();
    }
}
