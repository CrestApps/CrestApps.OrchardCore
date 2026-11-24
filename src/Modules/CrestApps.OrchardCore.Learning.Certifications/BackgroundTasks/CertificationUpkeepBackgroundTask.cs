using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Learning.Certifications.BackgroundTasks;

/// <summary>
/// Opens renewals whose window has come and issues the certificates whose completion event was lost.
/// </summary>
[BackgroundTask(
    Title = "Learning certification upkeep",
    Schedule = "25 * * * *",
    Description = "Opens certification renewals whose window has come, and issues certificates that a lost completion event missed.",
    LockTimeout = 1_000,
    LockExpiration = 900_000)]
public sealed class CertificationUpkeepBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run =>
        {
            await serviceProvider.GetRequiredService<CertificationUpkeep>().RunAsync(cancellationToken);
        });
    }
}
