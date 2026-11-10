using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.AgenticCommerce.BackgroundTasks;

/// <summary>
/// Gives the store a new signing key every 90 days, once a day at most.
/// </summary>
[BackgroundTask(
    Title = "Agentic commerce signing key rotation",
    Schedule = "17 3 * * *",
    Description = "Replaces the store's signing key for AI agents when it is 90 days old.",
    LockTimeout = 3_000,
    LockExpiration = 60_000)]
public sealed class MerchantKeyRotationBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IMerchantSigningKeyService>().RotateIfDueAsync(cancellationToken);
}
