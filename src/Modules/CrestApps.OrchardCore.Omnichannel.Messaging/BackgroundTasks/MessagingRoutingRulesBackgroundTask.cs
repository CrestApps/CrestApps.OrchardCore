using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.RoutingRules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.BackgroundTasks;

/// <summary>
/// Tries the routing rules again, every minute, on conversations nobody owns whose contact is still waiting for a person.
/// </summary>
[BackgroundTask(
    Title = "Messaging Routing Rules",
    Schedule = "* * * * *",
    Description = "Assigns waiting messaging conversations nobody owns to agents through the routing rules.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class MessagingRoutingRulesBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run =>
        {
            var sweep = serviceProvider.GetRequiredService<MessagingRoutingRulesSweep>();
            var logger = serviceProvider.GetRequiredService<ILogger<MessagingRoutingRulesBackgroundTask>>();

            try
            {
                run.AddItems(await sweep.RouteWaitingAsync(cancellationToken), "assigned");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while assigning waiting conversations through the routing rules.");
                run.ReportError(ex);
            }
        });
    }
}
