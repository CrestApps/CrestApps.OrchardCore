using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Records the calls that are not Contact Center interactions -- an automated voice agent's call and a number an
/// agent dialed on the soft phone's keypad -- when the tenant records every call.
/// </summary>
/// <remarks>
/// A Contact Center interaction is recorded by the recording service, which gates it on the recording governance
/// policy. These calls have no interaction to hang that on, so the same policy is asked directly (it exists only while
/// the Call Recording feature is enabled) and the recording carries what the call recordings page needs to list it.
/// Recording is best-effort: a refusal never affects the call.
/// </remarks>
public sealed class TelnyxAutomaticCallRecorder : ITelnyxAutomaticCallRecorder
{
    private readonly TelnyxApiClient _apiClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<TelnyxOptions> _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxAutomaticCallRecorder"/> class.
    /// </summary>
    /// <param name="apiClient">The Telnyx API client.</param>
    /// <param name="serviceProvider">The service provider the recording governance policy is resolved from, when the Call Recording feature is enabled.</param>
    /// <param name="options">The Telnyx options.</param>
    /// <param name="logger">The logger.</param>
    public TelnyxAutomaticCallRecorder(
        TelnyxApiClient apiClient,
        IServiceProvider serviceProvider,
        IOptionsMonitor<TelnyxOptions> options,
        ILogger<TelnyxAutomaticCallRecorder> logger)
    {
        _apiClient = apiClient;
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<bool> RecordAiCallAsync(
        string callControlId,
        string activityId,
        string customerNumber,
        bool isInbound,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(activityId))
        {
            return Task.FromResult(false);
        }

        return RecordAsync(
            callControlId,
            TelnyxRecordingClientState.ForAiCall(activityId, customerNumber, isInbound),
            "automated voice agent",
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> RecordSoftPhoneCallAsync(
        string callControlId,
        string agentUserId,
        string telephonyCallId,
        string dialedNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentUserId))
        {
            return Task.FromResult(false);
        }

        return RecordAsync(
            callControlId,
            TelnyxRecordingClientState.ForSoftPhoneCall(agentUserId, telephonyCallId, dialedNumber),
            "soft phone",
            cancellationToken);
    }

    private async Task<bool> RecordAsync(
        string callControlId,
        TelnyxRecordingClientState clientState,
        string callKind,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callControlId) || !_options.CurrentValue.IsConfigured)
        {
            return false;
        }

        var governancePolicy = _serviceProvider.GetService<IRecordingGovernancePolicy>();

        if (governancePolicy is null)
        {
            return false;
        }

        try
        {
            var decision = await governancePolicy.EvaluateAutomaticStartAsync(cancellationToken);

            if (!decision.Allowed)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "The {CallKind} call on leg {CallControlId} is not recorded automatically: {Reason}.",
                        callKind,
                        callControlId.SanitizeLogValue(),
                        decision.DenyReasonCode);
                }

                return false;
            }

            var result = await _apiClient.PostCallActionAsync(callControlId, "record_start", new Dictionary<string, object>
            {
                ["format"] = TelnyxConstants.Recording.Format,
                ["channels"] = "single",
                ["client_state"] = clientState.ToClientState(),
                // Telnyx redelivers webhooks; the command id makes the redelivered answer's start a no-op.
                ["command_id"] = $"auto-record-{callControlId}",
            }, cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Telnyx refused to record the {CallKind} call on leg {CallControlId} ({StatusCode}): {Response}",
                    callKind,
                    callControlId.SanitizeLogValue(),
                    result.StatusCode,
                    result.ErrorBody.SanitizeLogValue());

                return false;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Started recording the {CallKind} call on leg {CallControlId} automatically.",
                    callKind,
                    callControlId.SanitizeLogValue());
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Could not start recording the {CallKind} call on leg {CallControlId}; the call goes on unrecorded.",
                callKind,
                callControlId.SanitizeLogValue());

            return false;
        }
    }
}
