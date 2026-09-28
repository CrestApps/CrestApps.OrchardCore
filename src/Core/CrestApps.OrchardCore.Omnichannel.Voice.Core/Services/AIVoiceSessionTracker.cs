using System.Collections.Concurrent;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <inheritdoc/>
/// <remarks>
/// A tenant singleton: the turn-based measurements outlive any one request, and are dropped once they are old
/// enough that the call they belong to cannot still be going.
/// </remarks>
internal sealed class AIVoiceSessionTracker : IAIVoiceSessionTracker
{
    /// <summary>
    /// How long a turn-based call is followed before it is assumed to have ended without this node hearing.
    /// </summary>
    private static readonly TimeSpan _abandonedAfter = TimeSpan.FromHours(6);

    private readonly ConcurrentDictionary<string, TurnBasedCall> _turnBased = new(StringComparer.Ordinal);
    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceSessionTracker"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host, used to write a summary in a scope that outlives the request.</param>
    /// <param name="shellSettings">The tenant the calls belong to.</param>
    /// <param name="logger">The logger.</param>
    public AIVoiceSessionTracker(IShellHost shellHost, ShellSettings shellSettings, ILogger<AIVoiceSessionTracker> logger)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void BeginTurnBased(string activityId, DateTime answeredUtc)
    {
        if (string.IsNullOrEmpty(activityId))
        {
            return;
        }

        ForgetAbandoned(answeredUtc);

        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: false);
        meter.Start(answeredUtc.Ticks);

        // A redelivered answer keeps the call that is already being measured.
        _turnBased.TryAdd(activityId, new TurnBasedCall(meter, answeredUtc));
    }

    /// <inheritdoc/>
    public void TurnBasedSpeech(string activityId, bool started, DateTime occurredUtc)
    {
        if (string.IsNullOrEmpty(activityId) || !_turnBased.TryGetValue(activityId, out var call))
        {
            return;
        }

        if (started)
        {
            call.Meter.AssistantSpeechStarted(occurredUtc.Ticks);
        }
        else
        {
            call.Meter.AssistantSpeechEnded(occurredUtc.Ticks);
        }
    }

    /// <inheritdoc/>
    public AIVoiceSessionMeasurements EndTurnBased(string activityId, DateTime endedUtc)
    {
        if (string.IsNullOrEmpty(activityId) || !_turnBased.TryRemove(activityId, out var call))
        {
            return null;
        }

        call.Meter.Stop(endedUtc.Ticks);

        return call.Meter.Measure();
    }

    /// <inheritdoc/>
    public async Task RecordAsync(AIVoiceSessionDraft draft)
    {
        if (string.IsNullOrEmpty(draft?.ActivityId))
        {
            return;
        }

        try
        {
            var scope = await _shellHost.GetScopeAsync(_shellSettings);

            // Not awaited, exactly as the finished live session is handed on: the request this was called from
            // may already be abandoned, and the summary must not depend on it living long enough to write it.
            _ = scope.UsingAsync(async childScope =>
            {
                try
                {
                    await childScope.ServiceProvider
                        .GetRequiredService<AIVoiceSessionSummaryWriter>()
                        .WriteAsync(draft);
                }
                catch (Exception ex)
                {
                    childScope.ServiceProvider
                        .GetRequiredService<ILogger<AIVoiceSessionTracker>>()
                        .LogWarning(ex, "Could not record the AI voice session of activity '{ActivityId}'.", draft.ActivityId.SanitizeLogValue());
                }
            });
        }
        catch (Exception ex)
        {
            // A report that cannot be written must never cost the call anything.
            _logger.LogWarning(ex, "Could not open a scope to record the AI voice session of activity '{ActivityId}'.", draft.ActivityId.SanitizeLogValue());
        }
    }

    private void ForgetAbandoned(DateTime nowUtc)
    {
        foreach (var (activityId, call) in _turnBased)
        {
            if (nowUtc - call.BeganUtc > _abandonedAfter)
            {
                _turnBased.TryRemove(activityId, out _);
            }
        }
    }

    private sealed record TurnBasedCall(AIVoiceSessionMeter Meter, DateTime BeganUtc);
}
