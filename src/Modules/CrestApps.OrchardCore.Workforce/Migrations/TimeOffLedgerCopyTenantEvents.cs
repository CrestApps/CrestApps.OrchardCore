using CrestApps.OrchardCore.Workforce.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Migrations;

/// <summary>
/// Runs the time-off ledger copy at each start of the tenant, after the migrations, until a later release drops the
/// old entries: a copy cut short is finished, and entries a node of the earlier release wrote meanwhile are picked up.
/// </summary>
internal sealed class TimeOffLedgerCopyTenantEvents : ModularTenantEvents
{
    public override Task ActivatedAsync()
    {
        // In a scope of its own after the activation commits: on SQLite a second connection during the activation
        // would wait for the write lock the migrations hold.
        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;

            try
            {
                await services.GetRequiredService<TimeOffLedgerCopy>().CopyAsync();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                services.GetRequiredService<ILogger<TimeOffLedgerCopyTenantEvents>>().LogError(exception, "The time-off ledger copy failed; it runs again at the next start, and accrual waits until it completes.");
            }
        });

        return Task.CompletedTask;
    }
}
