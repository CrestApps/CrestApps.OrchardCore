using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Promotes due callbacks into outbound activities so the dialer or an agent can handle them.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Callback Dispatch",
    Schedule = "* * * * *",
    Description = "Promotes scheduled callbacks that have become due into outbound work.",
    LockTimeout = 5_000,
    LockExpiration = 60_000)]
public sealed class CallbackDispatchBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var workManager = serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>();
        using var workLease = workManager.TryEnter(ContactCenterConstants.Feature.Dialer);

        if (workLease is null)
        {
            return;
        }

        var callbackService = serviceProvider.GetRequiredService<ICallbackService>();
        var logger = serviceProvider.GetRequiredService<ILogger<CallbackDispatchBackgroundTask>>();

        try
        {
            await callbackService.PromoteDueAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while promoting due callbacks.");
        }
    }
}
