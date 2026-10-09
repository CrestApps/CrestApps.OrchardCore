using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Concluding a call whose end the provider never reported.
/// </summary>
public sealed partial class VoiceAgentConversationLoop
{
    /// <inheritdoc/>
    public async Task<bool> ConcludeStrandedCallAsync(string activityId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(activityId))
        {
            return false;
        }

        var activity = await _activityStore.FindByIdAsync(activityId, cancellationToken);

        // Concluded already, or handed to a person whose outcome it now is: exactly what a hangup would leave alone.
        if (!VoiceCallConclusionPolicy.ShouldConclude(activity))
        {
            return false;
        }

        ReleaseVoicemailClaim(activityId);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "AI voice activity '{ActivityId}' was still {Status} with nothing heard from its call for a long time; its end was never reported, so it is concluded now.",
                activityId.SanitizeLogValue(),
                activity.Status);
        }

        // There is no conversation to review without a session, and the review is what concludes a call. Left as it
        // is, the sweep would find it again every time and never move it on.
        if (string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            activity.Status = ActivityStatus.Failed;
            activity.TerminalReasonCode = StrandedReasonCode;
            activity.CompletedUtc = _clock.UtcNow;
            await _activityStore.UpdateAsync(activity, cancellationToken);

            return true;
        }

        // The same conclusion a reported hangup runs, after this scope commits.
        ShellScope.AddDeferredTask(async scope =>
        {
            try
            {
                await ConcludeAsync(scope.ServiceProvider, activityId);

                // A call that could not be reviewed -- no profile left to review it with, say -- is failed rather than
                // left open, or the sweep finds it again every few minutes for ever.
                var store = scope.ServiceProvider.GetRequiredService<IOmnichannelActivityStore>();
                var concluded = await store.FindByIdAsync(activityId);

                if (concluded is not null && !concluded.Status.IsTerminal())
                {
                    concluded.Status = ActivityStatus.Failed;
                    concluded.TerminalReasonCode = StrandedReasonCode;
                    concluded.CompletedUtc ??= scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
                    await store.UpdateAsync(concluded);
                }
            }
            catch (Exception ex)
            {
                scope.ServiceProvider.GetRequiredService<ILogger<VoiceAgentConversationLoop>>()
                    .LogError(ex, "Failed to conclude stranded AI voice activity '{ActivityId}'.", activityId.SanitizeLogValue());
            }
        });

        return true;
    }

    // Why a stranded call with no conversation to review was failed.
    private const string StrandedReasonCode = "voice-call-end-not-reported";
}
