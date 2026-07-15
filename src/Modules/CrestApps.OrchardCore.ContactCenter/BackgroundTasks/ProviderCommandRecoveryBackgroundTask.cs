using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Recovers due provider commands so ambiguous or interrupted provider operations are resumed through the
/// durable provider-command state machine.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Provider Command Recovery",
    Schedule = "* * * * *",
    Description = "Recovers due Contact Center provider commands for dispatch and reconciliation.",
    LockTimeout = 5_000,
    LockExpiration = 1_800_000)]
public sealed class ProviderCommandRecoveryBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var processor = serviceProvider.GetRequiredService<IProviderCommandProcessor>();
        var logger = serviceProvider.GetRequiredService<ILogger<ProviderCommandRecoveryBackgroundTask>>();

        try
        {
            await processor.RecoverDueAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                OperationalLogRedactor.RedactException(ex),
                "An error occurred while recovering Contact Center provider commands for feature {FeatureId}.",
                ContactCenterConstants.Feature.Voice);
        }
    }
}
