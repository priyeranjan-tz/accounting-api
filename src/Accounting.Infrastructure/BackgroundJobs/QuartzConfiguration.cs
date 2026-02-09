using Quartz;
using Accounting.Infrastructure.Persistence.Outbox;

namespace Accounting.Infrastructure.BackgroundJobs;

/// <summary>
/// Configuration for Quartz.NET background job scheduling
/// </summary>
public static class QuartzConfiguration
{
    /// <summary>
    /// Configures scheduled jobs for automated invoice generation
    /// </summary>
    public static void ConfigureJobs(IServiceCollectionQuartzConfigurator quartz)
    {
        // Daily Invoice Job - Runs every day at 00:05 UTC
        var dailyJobKey = new JobKey("DailyInvoiceJob");
        quartz.AddJob<DailyInvoiceJob>(opts => opts.WithIdentity(dailyJobKey));
        quartz.AddTrigger(opts => opts
            .ForJob(dailyJobKey)
            .WithIdentity("DailyInvoiceTrigger")
            .WithCronSchedule("0 5 0 * * ?") // Every day at 00:05 UTC
            .WithDescription("Generates daily invoices at midnight UTC"));

        // Weekly Invoice Job - Runs every Sunday at 00:10 UTC
        var weeklyJobKey = new JobKey("WeeklyInvoiceJob");
        quartz.AddJob<WeeklyInvoiceJob>(opts => opts.WithIdentity(weeklyJobKey));
        quartz.AddTrigger(opts => opts
            .ForJob(weeklyJobKey)
            .WithIdentity("WeeklyInvoiceTrigger")
            .WithCronSchedule("0 10 0 ? * SUN") // Every Sunday at 00:10 UTC
            .WithDescription("Generates weekly invoices every Sunday"));

        // Monthly Invoice Job - Runs on the 1st of each month at 00:15 UTC
        var monthlyJobKey = new JobKey("MonthlyInvoiceJob");
        quartz.AddJob<MonthlyInvoiceJob>(opts => opts.WithIdentity(monthlyJobKey));
        quartz.AddTrigger(opts => opts
            .ForJob(monthlyJobKey)
            .WithIdentity("MonthlyInvoiceTrigger")
            .WithCronSchedule("0 15 0 1 * ?") // 1st of every month at 00:15 UTC
            .WithDescription("Generates monthly invoices on the first day of each month"));

        // T146: Outbox Processor Job - Runs every 30 seconds for at-least-once event delivery
        var outboxJobKey = new JobKey("OutboxProcessorJob");
        quartz.AddJob<OutboxProcessorJob>(opts => opts.WithIdentity(outboxJobKey));
        quartz.AddTrigger(opts => opts
            .ForJob(outboxJobKey)
            .WithIdentity("OutboxProcessorTrigger")
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(30)
                .RepeatForever())
            .WithDescription("Processes unpublished outbox events every 30 seconds"));
    }
}