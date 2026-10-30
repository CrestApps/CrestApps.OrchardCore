using System.Collections.Concurrent;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Collects a turn-based call's keys in memory and answers them on a shell scope of its own once the caller stops.
/// </summary>
/// <remarks>
/// A tenant singleton, the same shape as <see cref="TurnBasedSilenceWatchdog"/> and for the same reasons: every key's
/// webhook on this node has to reach the same collection, and the answer is given after those requests are gone.
/// </remarks>
public sealed class TurnBasedCallerKeys : ITurnBasedCallerKeys
{
    private readonly ConcurrentDictionary<string, (VoiceAgentEvent FirstKey, CallerKeyBuffer Keys)> _calls = new(StringComparer.Ordinal);
    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TurnBasedCallerKeys"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host, used to open a scope that outlives the request.</param>
    /// <param name="shellSettings">The tenant the call belongs to.</param>
    /// <param name="logger">The logger.</param>
    public TurnBasedCallerKeys(
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger<TurnBasedCallerKeys> logger)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Collect(VoiceAgentEvent voiceEvent)
    {
        ArgumentNullException.ThrowIfNull(voiceEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(voiceEvent.ActivityId);

        var keys = voiceEvent.Digits?.Trim();

        if (string.IsNullOrEmpty(keys))
        {
            return;
        }

        var call = _calls.GetOrAdd(voiceEvent.ActivityId, _ => (voiceEvent, new CallerKeyBuffer()));

        // The first key of a burst answers for all of them.
        if (call.Keys.Add(keys, voiceEvent.OccurredUtc))
        {
            _ = AnswerWhenSettledAsync(voiceEvent.ActivityId, call);
        }
    }

    /// <inheritdoc/>
    public void Forget(string activityId)
    {
        if (!string.IsNullOrEmpty(activityId))
        {
            _calls.TryRemove(activityId, out _);
        }
    }

    private async Task AnswerWhenSettledAsync(string activityId, (VoiceAgentEvent FirstKey, CallerKeyBuffer Keys) call)
    {
        try
        {
            var keys = await call.Keys.TakeWhenSettledAsync(RealtimeVoiceConversationRunner.CallerKeysSettle, CancellationToken.None);

            // The call hung up while the caller was pressing keys.
            if (!_calls.TryGetValue(activityId, out var current) || !ReferenceEquals(current.Keys, call.Keys))
            {
                return;
            }

            var scope = await _shellHost.GetScopeAsync(_shellSettings);

            await scope.UsingAsync(async childScope =>
            {
                await childScope.ServiceProvider
                    .GetRequiredService<VoiceAgentConversationLoop>()
                    .OnCallerKeysAsync(call.FirstKey, keys, CancellationToken.None);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to answer the keys the caller pressed on automated call activity '{ActivityId}'.",
                activityId.SanitizeLogValue());
        }
    }
}
