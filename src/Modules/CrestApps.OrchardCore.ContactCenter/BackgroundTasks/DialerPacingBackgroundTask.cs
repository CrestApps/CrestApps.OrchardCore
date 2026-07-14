using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Runs one pacing cycle for each enabled dialer profile so power and progressive campaigns dial automatically.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Dialer Pacing",
    Schedule = "* * * * *",
    Description = "Reserves agents and places outbound calls for enabled dialer profiles.",
    LockTimeout = 5_000,
    LockExpiration = 60_000)]
public sealed class DialerPacingBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var dialerManager = serviceProvider.GetRequiredService<IDialerProfileManager>();
        var dialerService = serviceProvider.GetRequiredService<IDialerService>();
        var logger = serviceProvider.GetRequiredService<ILogger<DialerPacingBackgroundTask>>();

        var profiles = await dialerManager.ListEnabledAsync(cancellationToken);

        foreach (var profile in profiles)
        {
            try
            {
                await dialerService.RunCycleAsync(profile, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while running dialer profile '{Profile}'.", profile.Name);
            }
        }
    }
}
