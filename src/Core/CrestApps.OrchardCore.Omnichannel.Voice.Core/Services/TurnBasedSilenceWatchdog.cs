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
    public static readonly TimeSpan SilenceBeforeSpeakingUp = TimeSpan.FromSeconds(12);

    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

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
        _ = WatchAsync(silence);

        return Task.CompletedTask;
    }

    private async Task WatchAsync(TurnBasedSilence silence)
    {
        try
        {
            await Task.Delay(SilenceBeforeSpeakingUp);

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
