using CrestApps.Core.Omnichannel.Voice.Models;
using CrestApps.Core.Omnichannel.Voice.Services;
using CrestApps.Core.Omnichannel.Voice.Tools;
using CrestApps.Core.Omnichannel.Voice;
using CrestApps.Core;
using CrestApps.Core.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Finishes a live call on a scope of its own, off the request thread.
/// </summary>
/// <remarks>
/// The same shape the SMS webhook endpoint uses, and for the same reason: a provider gives a webhook seconds to
/// answer, and a realtime voice session holds one open for the length of a call. By the time the session ends the
/// request has timed out, been retried, and had its connection aborted — so anything still attached to it, its
/// services and its cancellation token included, is already gone. A scope taken from the shell host belongs to
/// the tenant rather than to the request, and outlives it.
/// </remarks>
public sealed class RealtimeCallCompletionRunner : IRealtimeCallCompletionRunner
{
    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RealtimeCallCompletionRunner"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host, used to open a scope that outlives the request.</param>
    /// <param name="shellSettings">The tenant the call belongs to.</param>
    /// <param name="logger">The logger.</param>
    public RealtimeCallCompletionRunner(
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger<RealtimeCallCompletionRunner> logger)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(RealtimeCallCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);

        // Logged before the scope is opened. When this went wrong the symptom was silence — no handoff, no
        // hangup, and not one line saying anything had been attempted — so the attempt itself is now on the
        // record, whatever happens next.
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Finishing automated call for activity '{ActivityId}' off the request thread (handoff requested: {HandoffRequested}, end requested: {EndCallRequested}).",
                completion.ActivityId.SanitizeLogValue(),
                completion.HandoffRequested,
                completion.EndCallRequested);
        }

        var scope = await _shellHost.GetScopeAsync(_shellSettings);

        // Not awaited, exactly as the SMS webhook does it: the request this was called from is finished (or
        // abandoned), and the work must not be tied to whether it lives long enough to see it through.
        _ = scope.UsingAsync(async childScope =>
        {
            try
            {
                await childScope.ServiceProvider
                    .GetRequiredService<VoiceAgentConversationLoop>()
                    .FinishRealtimeCallAsync(completion);
            }
            catch (Exception ex)
            {
                childScope.ServiceProvider
                    .GetRequiredService<ILogger<RealtimeCallCompletionRunner>>()
                    .LogError(
                        ex,
                        "Failed to finish the automated call for activity '{ActivityId}'.",
                        completion.ActivityId.SanitizeLogValue());
            }
        });
    }
}
