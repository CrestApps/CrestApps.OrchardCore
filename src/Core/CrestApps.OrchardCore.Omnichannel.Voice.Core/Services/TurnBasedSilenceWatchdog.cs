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
        _ = WatchAsync(silence, HeardCount(silence.ActivityId));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void CallerHeard(string activityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityId);

        _heard.AddOrUpdate(activityId, 1, static (_, count) => count + 1);
    }

    /// <inheritdoc/>
    public void Forget(string activityId)
    {
        if (!string.IsNullOrEmpty(activityId))
        {
            _heard.TryRemove(activityId, out _);
        }
    }

    internal long HeardCount(string activityId)
        => string.IsNullOrEmpty(activityId) ? 0 : _heard.GetValueOrDefault(activityId);

    private async Task WatchAsync(TurnBasedSilence silence, long heardWhenArmed)
    {
        try
        {
            await Task.Delay(silence.Wait ?? SilenceBeforeSpeakingUp);

            // The caller spoke during the wait. The turn answering them listens again, and watches again, when it
            // is done.
            if (HeardCount(silence.ActivityId) != heardWhenArmed)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "The caller on automated call activity '{ActivityId}' spoke during the listening turn, so it is not treated as silent.",
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
