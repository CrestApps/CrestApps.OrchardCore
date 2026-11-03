using System.Collections.Concurrent;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Waits out a listening turn on a shell scope of its own, then asks the conversation loop whether anybody spoke.
/// </summary>
/// <remarks>
/// The same shape as <see cref="RealtimeCallCompletionRunner"/>, for the same reason: the webhook that began
/// listening is answered long before the wait is over, and anything attached to that request is gone by then.
/// A scope taken from the shell host belongs to the tenant and outlives it.
/// <para>
/// A tenant singleton, so a watch sees the caller lines every request on this node has heard. A call's later
/// webhooks reach the node that holds it, as they must for its media to work at all.
/// </para>
/// </remarks>
public sealed class TurnBasedSilenceWatchdog : ITurnBasedSilenceWatchdog
{
    /// <summary>
    /// How long the line may be silent before the assistant speaks up.
    /// </summary>
    /// <remarks>
    /// The same wait a live session uses before it asks whether the caller is still there: long enough to be a
    /// silence rather than a pause for thought.
    /// </remarks>
    public static readonly TimeSpan SilenceBeforeSpeakingUp = TimeSpan.FromSeconds(8);

    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    // How many caller lines each call has heard. A watch notes the count when it is armed and stands down if it
    // has changed by the time it fires.
    private readonly ConcurrentDictionary<string, long> _heard = new(StringComparer.Ordinal);

    // How many watches each call has armed. Only the latest may fire: the assistant can say two lines back to back,
    // and the silence is counted from the end of the last one.
    private readonly ConcurrentDictionary<string, long> _armed = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="TurnBasedSilenceWatchdog"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host, used to open a scope that outlives the request.</param>
    /// <param name="shellSettings">The tenant the call belongs to.</param>
    /// <param name="logger">The logger.</param>
    public TurnBasedSilenceWatchdog(
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger<TurnBasedSilenceWatchdog> logger)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task ArmAsync(TurnBasedSilence silence)
    {
        ArgumentNullException.ThrowIfNull(silence);

        // Not awaited: the request that armed this has to be answered now, and the wait must not depend on
        // whether that request lives long enough to see it through.
        _ = WatchAsync(silence, Begin(silence.ActivityId));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public long CallerHeard(string activityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityId);

        return _heard.AddOrUpdate(activityId, 1, static (_, count) => count + 1);
    }

    /// <inheritdoc/>
    public void Forget(string activityId)
    {
        if (!string.IsNullOrEmpty(activityId))
        {
            _heard.TryRemove(activityId, out _);
            _armed.TryRemove(activityId, out _);
        }
    }

    /// <inheritdoc/>
    public long HeardCount(string activityId)
        => string.IsNullOrEmpty(activityId) ? 0 : _heard.GetValueOrDefault(activityId);

    /// <summary>
    /// Starts a watch on a call, replacing any watch already running on it.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <returns>What the watch was started with, for <see cref="IsStillSilent"/>.</returns>
    internal (long Heard, long Armed) Begin(string activityId)
        => string.IsNullOrEmpty(activityId)
            ? (0, 0)
            : (HeardCount(activityId), _armed.AddOrUpdate(activityId, 1, static (_, count) => count + 1));

    /// <summary>
    /// Whether a watch that has waited its time should speak up: it is still the call's latest watch, and the
    /// caller has not been heard since it began.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="watch">What the watch was started with.</param>
    internal bool IsStillSilent(string activityId, (long Heard, long Armed) watch)
    {
        if (string.IsNullOrEmpty(activityId))
        {
            return true;
        }

        // The assistant said another line after this watch began, and that line's watch counts from its end.
        // Live, a watch begun when "are you still there?" ended fired 1.3 seconds after the reply queued behind it,
        // while the caller was answering, and cut their sentence off.
        if (_armed.GetValueOrDefault(activityId) != watch.Armed)
        {
            return false;
        }

        // The caller spoke during the wait. The turn answering them listens again, and watches again, when it is
        // done.
        return HeardCount(activityId) == watch.Heard;
    }

    private async Task WatchAsync(TurnBasedSilence silence, (long Heard, long Armed) watch)
    {
        try
        {
            await Task.Delay(silence.Wait ?? SilenceBeforeSpeakingUp);

            if (!IsStillSilent(silence.ActivityId, watch))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "The listening turn on automated call activity '{ActivityId}' was not silent: the caller spoke, or the assistant said something after it began.",
                        silence.ActivityId.SanitizeLogValue());
                }

                return;
            }

            var scope = await _shellHost.GetScopeAsync(_shellSettings);

            await scope.UsingAsync(async childScope =>
            {
                await childScope.ServiceProvider
                    .GetRequiredService<VoiceAgentConversationLoop>()
                    .OnListeningTimedOutAsync(silence, CancellationToken.None);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to check automated call activity '{ActivityId}' for silence.",
                silence.ActivityId.SanitizeLogValue());
        }
    }
}
