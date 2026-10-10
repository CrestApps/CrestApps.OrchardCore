using CrestApps.OrchardCore.Omnichannel.Automation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Email.BackgroundTasks;

/// <summary>
/// Re-drives the automated email conversations whose AI reply was lost before it was sent.
/// </summary>
[BackgroundTask(
    Title = "Automated Email Owed-Reply Recovery",
    Schedule = "*/10 * * * *",
    Description = "Re-drives automated email conversations whose AI reply was lost before it was sent.",
    LockTimeout = 5_000,
    LockExpiration = LeaseMilliseconds)]
public sealed class AutomatedConversationRecoveryBackgroundTask : IBackgroundTask
{
    private const int LeaseMilliseconds = 300_000;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var recovery = serviceProvider.GetRequiredService<AutomatedOwedReplyRecovery>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var logger = serviceProvider.GetRequiredService<ILogger<AutomatedConversationRecoveryBackgroundTask>>();

        try
        {
            // Stop well before the lease ends; whatever is left is picked up by the next run.
            await recovery.RecoverAsync(clock.UtcNow.AddMilliseconds(LeaseMilliseconds * 0.6), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Recovering owed automated email replies failed.");
        }
    }
}
