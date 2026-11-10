using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.AgenticCommerce.BackgroundTasks;

/// <summary>
/// Expires the agent checkouts whose time ran out, every five minutes, which releases the stock they held.
/// </summary>
[BackgroundTask(
    Title = "Agent checkout expiry",
    Schedule = "*/5 * * * *",
    Description = "Expires the AI agent checkouts that weren't completed in time and releases the stock they held.",
    LockTimeout = 3_000,
    LockExpiration = 300_000)]
public sealed class AgentCheckoutExpiryBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var expired = await serviceProvider.GetRequiredService<IAgentCheckoutService>().ExpireAsync(cancellationToken);

        if (expired > 0)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<AgentCheckoutExpiryBackgroundTask>>();

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("{Count} AI agent checkouts expired.", expired);
            }
        }
    }
}
