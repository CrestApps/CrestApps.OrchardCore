using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Bleeps the personal details customers said in the recordings of AI conversations kept without personal details.
/// </summary>
[BackgroundTask(
    Title = "Call Recording Bleeping",
    Schedule = "*/10 * * * *",
    Description = "Covers the personal details customers said in AI call recordings with a tone, from the redacted transcript.",
    LockTimeout = 10_000,
    LockExpiration = 900_000)]
public sealed class CallRecordingBleepBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<CallRecordingBleepBackgroundTask>>();

        try
        {
            await serviceProvider.GetRequiredService<CallRecordingBleeper>().RunAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "An error occurred while bleeping call recordings.");
        }
    }
}
