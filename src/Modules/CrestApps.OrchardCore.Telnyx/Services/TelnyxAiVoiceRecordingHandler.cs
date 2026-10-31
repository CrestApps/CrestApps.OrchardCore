using CrestApps.Core.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Records an automated voice agent's call from the moment it is answered, when the tenant records every call, so it
/// can be played back beside its transcript on the call recordings page.
/// </summary>
/// <remarks>
/// <para>
/// The recording is started on a scope of its own, beside the conversation rather than in front of it. Awaited
/// here, the request to start it (about half a second) came before the assistant could even begin opening its
/// session, and the caller who answered with "hello?" heard the greeting four and a half seconds later. Telnyx records
/// from the moment the command lands either way, and the assistant takes longer than that to say anything.
/// </para>
/// <para>
/// Its own scope also keeps what the recording writes off the answered webhook's session. That request is held open
/// by a realtime conversation for as long as the AI talks, and a write left pending in its session kept the
/// database's write lock until the conversation stored its first line; on SQLite every other write in the tenant
/// waited behind it.
/// </para>
/// </remarks>
public sealed class TelnyxAiVoiceRecordingHandler : ITelnyxAiVoiceEventHandler
{
    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxAiVoiceRecordingHandler"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host, used to open the scope the recording is started on.</param>
    /// <param name="shellSettings">The tenant the call belongs to.</param>
    /// <param name="logger">The logger.</param>
    public TelnyxAiVoiceRecordingHandler(
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger<TelnyxAiVoiceRecordingHandler> logger)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, CancellationToken cancellationToken = default)
    {
        if (callEvent is null ||
            state is null ||
            state.Intent == TelnyxOutboundBridgeState.AiTransferLegIntent ||
            !string.Equals(callEvent.EventType?.Trim(), "call.answered", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var (isInbound, customerNumber) = ResolveCustomer(callEvent, state);
        var callControlId = callEvent.CallControlId;
        var activityId = state.ActivityId;

        var scope = await _shellHost.GetScopeAsync(_shellSettings);

        // On the thread pool, not inline: an un-awaited scope runs on this thread until something truly yields, and
        // on SQLite a YesSql call never does -- the conversation would wait for the recording after all. Not tied to
        // the request's token either: the recording outlives the webhook that started it.
        _ = Task.Run(() => RecordInOwnScopeAsync(scope, callControlId, activityId, customerNumber, isInbound), CancellationToken.None);
    }

    /// <summary>
    /// Tells whether the AI voice leg is a call the customer placed, and which number is the customer's.
    /// </summary>
    /// <remarks>
    /// Telnyx calls a leg the caller placed "incoming", but says so on <c>call.initiated</c> only: <c>call.answered</c>
    /// carries no direction, so an entry point's AI call is marked inbound in its state. The customer is whoever is not
    /// the platform's number.
    /// </remarks>
    /// <param name="callEvent">The answered event.</param>
    /// <param name="state">The AI voice leg's state.</param>
    /// <returns>Whether the call is inbound, and the customer's number.</returns>
    internal static (bool IsInbound, string CustomerNumber) ResolveCustomer(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state)
    {
        var isInbound = state.Inbound == true ||
            string.Equals(callEvent.Direction?.Trim(), "incoming", StringComparison.OrdinalIgnoreCase);

        return (isInbound, isInbound ? callEvent.From : callEvent.To);
    }

    private async Task RecordInOwnScopeAsync(
        ShellScope scope,
        string callControlId,
        string activityId,
        string customerNumber,
        bool isInbound)
    {
        try
        {
            // The scope commits its session when it ends, which lists the recording.
            await scope.UsingAsync(childScope => childScope.ServiceProvider
                .GetRequiredService<ITelnyxAutomaticCallRecorder>()
                .RecordAiCallAsync(callControlId, activityId, customerNumber, isInbound, CancellationToken.None));
        }
        catch (Exception ex)
        {
            // Around the scope rather than inside it, so a commit refused when the scope ends is reported too.
            _logger.LogError(
                ex,
                "Failed to start recording the automated voice agent call for activity '{ActivityId}'.",
                activityId.SanitizeLogValue());
        }
    }
}
