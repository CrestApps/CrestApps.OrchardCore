using CrestApps.OrchardCore.Omnichannel.Automation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Email.BackgroundTasks;

/// <summary>
/// Sends the campaign cadence's follow-up emails to automated email contacts who stopped answering, within business hours.
/// </summary>
[BackgroundTask(
    Title = "Automated Email Follow-Ups",
    Schedule = "*/5 * * * *",
    Description = "Sends follow-up emails to automated email contacts who have gone quiet, on the campaign's cadence and within business hours.",
    LockTimeout = 5_000,
    LockExpiration = LeaseMilliseconds)]
public sealed class AutomatedConversationFollowUpBackgroundTask : IBackgroundTask
{
    private const int LeaseMilliseconds = 300_000;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var followUps = serviceProvider.GetRequiredService<AutomatedFollowUpService>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var logger = serviceProvider.GetRequiredService<ILogger<AutomatedConversationFollowUpBackgroundTask>>();

        try
        {
            await followUps.SendDueAsync(clock.UtcNow.AddMilliseconds(LeaseMilliseconds * 0.6), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Sending automated email follow-ups failed.");
        }
    }
}
