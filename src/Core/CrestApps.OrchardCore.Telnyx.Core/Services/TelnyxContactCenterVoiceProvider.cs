using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Exposes Telnyx as a Contact Center voice provider. Telnyx delivers calls server-side and the platform
/// bridges the live call to the agent's browser SIP endpoint, so the provider uses the
/// <see cref="VoiceProviderDeliveryModel.ServerSideAcd"/> delivery model.
/// </summary>
public sealed partial class TelnyxContactCenterVoiceProvider :
    IContactCenterVoiceProvider,
    IContactCenterVoiceCallControlProvider,
    IContactCenterVoiceTransferProvider,
    IContactCenterVoiceRecordingProvider
{
    private readonly ITelephonyProviderResolver _telephonyResolver;
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly ITelnyxAgentCredentialStore _credentialStore;
    private readonly ITelnyxAgentEndpointResolver _agentEndpointResolver;
    private readonly TelnyxApiClient _apiClient;
    private readonly IClock _clock;
    private readonly ILogger<TelnyxContactCenterVoiceProvider> _logger;
    private readonly TelnyxOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxContactCenterVoiceProvider"/> class.
    /// </summary>
    public TelnyxContactCenterVoiceProvider(
        ITelephonyProviderResolver telephonyResolver,
        IContactCenterFeatureWorkManager workManager,
        ITelnyxAgentCredentialStore credentialStore,
        ITelnyxAgentEndpointResolver agentEndpointResolver,
        TelnyxApiClient apiClient,
        IClock clock,
        ILogger<TelnyxContactCenterVoiceProvider> logger,
        IOptionsMonitor<TelnyxOptions> telnyxOptions,
        IStringLocalizer<TelnyxContactCenterVoiceProvider> stringLocalizer)
    {
        _telephonyResolver = telephonyResolver;
        _workManager = workManager;
        _credentialStore = credentialStore;
        _agentEndpointResolver = agentEndpointResolver;
        _apiClient = apiClient;
        _clock = clock;
        _logger = logger;
        _options = telnyxOptions.CurrentValue;
        Name = stringLocalizer["Telnyx"];
    }

    /// <inheritdoc/>
    public string TechnicalName => TelnyxConstants.ProviderTechnicalName;

    /// <inheritdoc/>
    public LocalizedString Name { get; }

    /// <inheritdoc/>
    public ContactCenterVoiceProviderCapabilities Capabilities
        => ContactCenterVoiceProviderCapabilities.DialerDial |
            ContactCenterVoiceProviderCapabilities.AgentConnect |
            ContactCenterVoiceProviderCapabilities.CallTransfer |
            ContactCenterVoiceProviderCapabilities.Recording;

    /// <inheritdoc/>
    public VoiceProviderDeliveryModel DeliveryModel => VoiceProviderDeliveryModel.ServerSideAcd;

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> DialAsync(ContactCenterDialRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(TelnyxConstants.ContactCenterVoiceWorkPartition);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Telnyx Contact Center voice provider is temporarily unavailable.");
        }

        var provider = await _telephonyResolver.GetAsync(TelnyxConstants.ProviderTechnicalName);

        if (provider is null)
        {
            return Failure("provider_unavailable", "The Telnyx telephony provider is not configured.");
        }

        if (!provider.Capabilities.HasFlag(TelephonyCapabilities.Dial) ||
            provider is not ITelephonyCallControlProvider callControlProvider)
        {
            return Failure("provider_unavailable", "The Telnyx telephony provider cannot place outbound calls.");
        }

        var result = await callControlProvider.DialAsync(new DialRequest
        {
            To = request.Destination,
            From = request.CallerId,
            Metadata = request.Metadata,
        }, cancellationToken);

        if (!result.Succeeded)
        {
            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                OutcomeUnknown = result.OutcomeUnknown,
                ErrorCode = result.OutcomeUnknown ? "dial_outcome_unknown" : "dial_failed",
                ErrorMessage = result.Error,
                ProviderName = TechnicalName,
            };
        }

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = result.Call?.CallId,
            ProviderName = TechnicalName,
        };
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> ConnectToAgentAsync(ContactCenterConnectRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(TelnyxConstants.ContactCenterVoiceWorkPartition);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Telnyx Contact Center voice provider is temporarily unavailable.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderCallId))
        {
            return Failure("caller_call_missing", "A Telnyx caller call id is required to connect the caller to the agent.");
        }

        if (!_options.IsConfigured)
        {
            return Failure("provider_unavailable", "The Telnyx telephony provider is not configured.");
        }

        var agentEndpoint = await ResolveAgentEndpointAsync(request, cancellationToken);

        if (string.IsNullOrWhiteSpace(agentEndpoint))
        {
            return Failure("agent_endpoint_missing", "The selected agent has no live Telnyx soft-phone registration to connect the caller to.");
        }

        try
        {
            var callerCallControlId = request.ProviderCallId.Trim();

            // Answer the inbound caller leg first. Telnyx rejects a bridge whose legs are not yet answered
            // ("call not answered yet", code 90034), so the caller must be connected before the agent leg is
            // bridged in. A caller leg that is already answered simply fails here, which is ignored.
            var answerResult = await _apiClient.AnswerAsync(callerCallControlId, cancellationToken: cancellationToken);

            if (!answerResult.Succeeded)
            {
                _logger.LogWarning(
                    "Telnyx returned {StatusCode} answering the caller leg before an agent bridge (it may already be answered).",
                    answerResult.StatusCode);
            }

            // Originate the agent leg to the agent's registered browser SIP endpoint. The browser auto-answers
            // the invite; when its call.answered webhook arrives, the outbound-bridge orchestration bridges it
            // to the caller leg carried in client_state. Bridging is deferred to then because Telnyx requires
            // both legs to be answered first.
            var originateResult = await _apiClient.OriginateAsync(
                new TelnyxOriginateRequest
                {
                    ConnectionId = _options.ConnectionId,
                    To = agentEndpoint,
                    From = _options.DefaultOutboundCallerId,
                    ClientState = new TelnyxOutboundBridgeState
                    {
                        Intent = TelnyxOutboundBridgeState.ContactCenterAgentLegIntent,
                        PeerCallControlId = callerCallControlId,
                    }.ToClientStateJson(),
                },
                cancellationToken);

            if (!originateResult.Succeeded)
            {
                _logger.LogError(
                    "Telnyx rejected an agent-leg origination with status code {StatusCode}. Response: {Response}",
                    originateResult.StatusCode,
                    originateResult.ErrorBody.SanitizeLogValue());

                return Failure("agent_connect_failed", "The Telnyx agent leg could not be originated.");
            }

            var agentCallControlId = originateResult.CallControlId;

            if (string.IsNullOrWhiteSpace(agentCallControlId))
            {
                return Failure("agent_connect_failed", "Telnyx did not return an agent call control id.");
            }

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = true,
                ProviderName = TechnicalName,
                ProviderCallId = callerCallControlId,
                ProviderLegId = agentCallControlId,

                // Telnyx accepted the invite for delivery; the agent's browser has not answered it yet. The leg
                // is answered only when its call.answered webhook arrives, which is also what triggers the
                // bridge, so reporting it as dialing keeps the topology honest until then.
                ProviderLegState = VoiceCallState.Dialing,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while connecting a Telnyx caller to an agent.");

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                ProviderName = TechnicalName,
                ProviderCallId = request.ProviderCallId,
                ErrorCode = "agent_connect_failed",
                ErrorMessage = "The Telnyx caller-to-agent bridge could not be completed.",
                OutcomeUnknown = true,
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> TransferAsync(ContactCenterVoiceTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(TelnyxConstants.ContactCenterVoiceWorkPartition);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Telnyx Contact Center voice provider is temporarily unavailable.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderCallId) || string.IsNullOrWhiteSpace(request.Target))
        {
            return Failure("transfer_invalid", "A call id and destination are required to transfer the call.");
        }

        if (!_options.IsConfigured)
        {
            return Failure("provider_unavailable", "The Telnyx telephony provider is not configured.");
        }

        try
        {
            var result = await _apiClient.TransferAsync(
                request.ProviderCallId.Trim(),
                request.Target,
                _options.DefaultOutboundCallerId,
                cancellationToken: cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogError(
                    "Telnyx rejected a Contact Center transfer with status code {StatusCode}. Response: {Response}",
                    result.StatusCode,
                    result.ErrorBody.SanitizeLogValue());

                return Failure("transfer_failed", "The Telnyx call could not be transferred.");
            }

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = true,
                ProviderName = TechnicalName,
                ProviderCallId = request.ProviderCallId.Trim(),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while transferring a Telnyx Contact Center call.");

            return Failure("transfer_failed", "The Telnyx call could not be transferred.");
        }
    }

    private async Task<string> ResolveAgentEndpointAsync(ContactCenterConnectRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.AgentEndpoint))
        {
            return request.AgentEndpoint.Trim();
        }

        if (string.IsNullOrWhiteSpace(request.AgentUserId))
        {
            return null;
        }

        // One resolver for every path that dials an agent. Taking the first live credential here took the newest
        // issued one, which is not necessarily the one the browser is registered on, and dialling the wrong one
        // came back SIP 486 with the agent's phone sitting idle.
        return await _agentEndpointResolver.ResolveAsync(request.AgentUserId, cancellationToken);
    }


    // One shape, on the result type. This stays as a local name so every call site reads the same.
    private static ContactCenterVoiceProviderResult Failure(string errorCode, string errorMessage)
        => ContactCenterVoiceProviderResult.Failure(TelnyxConstants.ProviderTechnicalName, errorCode, errorMessage);
}
