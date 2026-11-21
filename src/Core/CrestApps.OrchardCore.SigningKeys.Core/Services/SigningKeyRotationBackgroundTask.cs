using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.SigningKeys.Core.Services;

/// <summary>
/// Once a day, rotates the key of every registered key set that reached its rotation age.
/// </summary>
[BackgroundTask(
    Title = "Signing key rotation",
    Schedule = "17 3 * * *",
    Description = "Replaces the signing key of each key set (agentic commerce, external tools) when it reaches its rotation age.",
    LockTimeout = 3_000,
    LockExpiration = 60_000)]
public sealed class SigningKeyRotationBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run =>
        {
            var ring = serviceProvider.GetRequiredService<ISigningKeyRing>();
            var options = serviceProvider.GetRequiredService<IOptions<SigningKeyOptions>>().Value;
            var logger = serviceProvider.GetRequiredService<ILogger<SigningKeyRotationBackgroundTask>>();

            foreach (var keySet in options.KeySets.Keys)
            {
                try
                {
                    if (await ring.RotateIfDueAsync(keySet, cancellationToken))
                    {
                        run.AddItems(1, "rotated");
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    run.ReportError(exception);
                    logger.LogError(exception, "The signing key of the key set '{KeySet}' could not be rotated.", keySet);
                }
            }
        });
}
