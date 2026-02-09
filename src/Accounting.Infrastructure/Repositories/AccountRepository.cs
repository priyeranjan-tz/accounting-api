using Accounting.Domain.Aggregates;
using Accounting.Domain.Enums;
using Accounting.Domain.Interfaces;
using Accounting.Infrastructure.Persistence.DbContext;
using Accounting.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for account operations using Entity Framework Core
/// </summary>
public class AccountRepository : IAccountRepository
{
    private readonly AccountingDbContext _dbContext;

    public AccountRepository(AccountingDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Account> CreateAsync(Account account, CancellationToken cancellationToken = default)
    {
        if (account == null)
            throw new ArgumentNullException(nameof(account));

        var entity = AccountMapper.MapToPersistence(account);
        await _dbContext.Accounts.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return account;
    }

    public async Task<Account?> GetByIdAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Accounts
            .Where(a => a.TenantId == tenantId)
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        return entity == null ? null : AccountMapper.MapToDomain(entity);
    }

    public async Task<(List<Account> Accounts, int TotalCount)> ListAsync(
        Guid tenantId,
        AccountStatus? status,
        AccountType? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Accounts
            .Where(a => a.TenantId == tenantId);

        // Apply optional filters
        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (type.HasValue)
            query = query.Where(a => a.Type == type.Value);

        // Get total count for pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var entities = await query
            .OrderBy(a => a.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var accounts = entities.Select(AccountMapper.MapToDomain).ToList();

        return (accounts, totalCount);
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Account name cannot be empty", nameof(name));

        return await _dbContext.Accounts
            .Where(a => a.TenantId == tenantId)
            .AnyAsync(a => a.Name == name.Trim(), cancellationToken);
    }

    public async Task<IEnumerable<Account>> GetByInvoiceFrequencyAsync(
        InvoiceFrequency frequency,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Accounts
            .Where(a => a.TenantId == tenantId && 
                       a.InvoiceFrequency == frequency &&
                       a.Status == AccountStatus.Active)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(AccountMapper.MapToDomain);
    }

    public async Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        if (account == null)
            throw new ArgumentNullException(nameof(account));

        var entity = AccountMapper.MapToPersistence(account);
        _dbContext.Accounts.Update(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
