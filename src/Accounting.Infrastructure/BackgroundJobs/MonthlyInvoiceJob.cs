using Accounting.Application.Commands;
using Accounting.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Accounting.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job for generating monthly invoices
/// Runs on the 1st day of each month at midnight UTC
/// </summary>
[DisallowConcurrentExecution]
public class MonthlyInvoiceJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MonthlyInvoiceJob> _logger;

    public MonthlyInvoiceJob(
        IServiceProvider serviceProvider,
        ILogger<MonthlyInvoiceJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobId = context.FireInstanceId;
        var scheduledTime = context.ScheduledFireTimeUtc?.UtcDateTime ?? DateTime.UtcNow;

        _logger.LogInformation(
            "MonthlyInvoiceJob started - JobId: {JobId}, ScheduledTime: {ScheduledTime}",
            jobId, scheduledTime);

        var startTime = DateTime.UtcNow;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            
            // Get all tenants and process invoices for each
            // For now, we'll use a system tenant ID - in production this should iterate all tenants
            var systemTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

            var handler = new GenerateScheduledInvoicesCommandHandler(
                scope.ServiceProvider.GetRequiredService<Domain.Interfaces.IAccountRepository>(),
                scope.ServiceProvider.GetRequiredService<Domain.Interfaces.IInvoiceRepository>(),
                scope.ServiceProvider.GetRequiredService<Domain.Interfaces.ILedgerQueryService>(),
                scope.ServiceProvider.GetRequiredService<Domain.Interfaces.IInvoiceNumberGenerator>(),
                scope.ServiceProvider.GetRequiredService<ILogger<GenerateScheduledInvoicesCommandHandler>>(),
                systemTenantId);

            var command = new GenerateScheduledInvoicesCommand(
                InvoiceFrequency.Monthly,
                DateTime.UtcNow);

            var result = await handler.HandleAsync(command, context.CancellationToken);

            var duration = DateTime.UtcNow - startTime;

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "MonthlyInvoiceJob completed successfully - JobId: {JobId}, Duration: {Duration}ms, " +
                    "AccountsProcessed: {AccountsProcessed}, InvoicesGenerated: {InvoicesGenerated}, Failed: {Failed}",
                    jobId, duration.TotalMilliseconds,
                    result.Value.AccountsProcessed, result.Value.InvoicesGenerated, result.Value.FailedAccounts);
            }
            else
            {
                _logger.LogError(
                    "MonthlyInvoiceJob failed - JobId: {JobId}, Duration: {Duration}ms, Error: {Error}",
                    jobId, duration.TotalMilliseconds, result.Error.Message);
            }
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex,
                "MonthlyInvoiceJob failed with exception - JobId: {JobId}, Duration: {Duration}ms",
                jobId, duration.TotalMilliseconds);
            throw;
        }
    }
}
