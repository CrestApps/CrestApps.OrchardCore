using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Revalidates active provider-backed interactions against the telephony server so restarts and missed
/// live events do not leave queued voice work out of sync.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Provider Call Reconciliation",
    Schedule = "* * * * *",
    Description = "Reconciles active Contact Center voice interactions against current provider call state.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class ProviderCallStateReconciliationBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var workManager = serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>();
        using var workLease = workManager.TryEnter(ContactCenterConstants.Feature.Voice);

        if (workLease is null)
        {
            return;
        }

        var participant = serviceProvider.GetRequiredService<ContactCenterVoiceTenantEvents>();
        var logger = serviceProvider.GetRequiredService<ILogger<ProviderCallStateReconciliationBackgroundTask>>();

        try
        {
            await participant.ReconcileProviderStateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while reconciling Contact Center provider call state.");
        }
    }
}
