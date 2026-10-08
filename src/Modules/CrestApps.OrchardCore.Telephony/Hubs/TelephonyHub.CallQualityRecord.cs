using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Hubs;

/// <summary>
/// Hands each call's end-of-call quality summary to whatever keeps a record of it.
/// </summary>
/// <remarks>
/// The summary used to be logged and nothing else, so a poor call could be found only by searching the log for the
/// agent and the minute it happened, and nothing tied it to the call. An observer, such as the contact center, now
/// stores it against the interaction and agent it belongs to.
/// </remarks>
public sealed partial class TelephonyHub
{
    private async Task ObserveCallQualityAsync(IServiceProvider services, CallQualityReport report)
    {
        var observers = services.GetServices<ICallQualityObserver>();

        if (!observers.Any())
        {
            return;
        }

        var observation = new CallQualityObservation
        {
            Source = CallQualitySource.Browser,
            UserId = Context.UserIdentifier,
            ProviderCallControlId = report.ProviderCallControlId,
            ProviderLegId = report.ProviderLegId,
            ProviderSessionId = report.ProviderSessionId,
            Rating = TelephonyCallQualityEvaluator.EvaluateSummary(report),
            ObservedUtc = services.GetRequiredService<IClock>().UtcNow,
            Browser = report,
        };

        foreach (var observer in observers)
        {
            try
            {
                await observer.ObserveAsync(observation, Context.ConnectionAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A record that could not be kept is a gap in the reports, not a reason to fail the soft phone's
                // report of a call that is already over.
                _logger.LogWarning(
                    ex,
                    "The call quality summary for user {UserId} could not be recorded by {Observer}.",
                    RedactedUserId(),
                    observer.GetType().Name.SanitizeLogValue());
            }
        }
    }
}
