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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITelnyxAgentCredentialStore _credentialStore;
    private readonly IClock _clock;
    private readonly ILogger<TelnyxContactCenterVoiceProvider> _logger;
    private readonly TelnyxOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxContactCenterVoiceProvider"/> class.
    /// </summary>
    public TelnyxContactCenterVoiceProvider(
        ITelephonyProviderResolver telephonyResolver,
        IContactCenterFeatureWorkManager workManager,
        IHttpClientFactory httpClientFactory,
        ITelnyxAgentCredentialStore credentialStore,
        IClock clock,
        ILogger<TelnyxContactCenterVoiceProvider> logger,
        IOptionsMonitor<TelnyxOptions> telnyxOptions,
        IStringLocalizer<TelnyxContactCenterVoiceProvider> stringLocalizer)
    {
        _telephonyResolver = telephonyResolver;
        _workManager = workManager;
        _httpClientFactory = httpClientFactory;
        _credentialStore = credentialStore;
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
            using var client = CreateClient();

            var callerCallControlId = request.ProviderCallId.Trim();

            // Answer the inbound caller leg first. Telnyx rejects a bridge whose legs are not yet answered
            // ("call not answered yet", code 90034), so the caller must be connected before the agent leg is
            // bridged in. A caller leg that is already answered simply returns an error here, which is ignored.
            using (var answerContent = JsonContent.Create(new Dictionary<string, object>(), options: TelnyxJsonSerializerOptions.Default))
            using (var answerResponse = await client.PostAsync(
                $"calls/{Uri.EscapeDataString(callerCallControlId)}/actions/answer",
                answerContent,
                cancellationToken))
            {
                if (!answerResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Telnyx returned {StatusCode} answering the caller leg before an agent bridge (it may already be answered).",
                        answerResponse.StatusCode);
                }
            }

            // Originate the agent leg to the agent's registered browser SIP endpoint. The browser auto-answers
            // the invite; when its call.answered webhook arrives, the outbound-bridge orchestration bridges it
            // to the caller leg carried in client_state. Bridging is deferred to then because Telnyx requires
            // both legs to be answered first.
            var originateBody = new Dictionary<string, object>
            {
                ["connection_id"] = _options.ConnectionId,
                ["to"] = agentEndpoint,
                ["client_state"] = new TelnyxOutboundBridgeState
                {
                    Intent = TelnyxOutboundBridgeState.ContactCenterAgentLegIntent,
                    PeerCallControlId = callerCallControlId,
                }.ToClientState(),
            };

            if (!string.IsNullOrWhiteSpace(_options.DefaultOutboundCallerId))
            {
                originateBody["from"] = _options.DefaultOutboundCallerId;
            }

            using var originateContent = JsonContent.Create(originateBody, options: TelnyxJsonSerializerOptions.Default);
            using var originateResponse = await client.PostAsync("calls", originateContent, cancellationToken);

            if (!originateResponse.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected an agent-leg origination with status code {StatusCode}. Response: {Response}",
                    originateResponse.StatusCode,
                    (await SafeReadContentAsync(originateResponse, cancellationToken)).SanitizeLogValue());

                return Failure("agent_connect_failed", "The Telnyx agent leg could not be originated.");
            }

            var agentCallControlId = await ReadDataStringAsync(originateResponse, "call_control_id", cancellationToken);

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
            using var client = CreateClient();
            var body = new Dictionary<string, object> { ["to"] = request.Target };

            if (!string.IsNullOrWhiteSpace(_options.DefaultOutboundCallerId))
            {
                body["from"] = _options.DefaultOutboundCallerId;
            }

            using var content = JsonContent.Create(body, options: TelnyxJsonSerializerOptions.Default);
            using var response = await client.PostAsync(
                $"calls/{Uri.EscapeDataString(request.ProviderCallId.Trim())}/actions/transfer",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telnyx rejected a Contact Center transfer with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    (await SafeReadContentAsync(response, cancellationToken)).SanitizeLogValue());

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

        var live = await _credentialStore.ListLiveByUserAsync(request.AgentUserId.Trim(), _clock.UtcNow, cancellationToken);
        var credential = live.Count > 0 ? live[0] : null;

        if (credential is null || string.IsNullOrWhiteSpace(credential.SipUsername))
        {
            return null;
        }

        var sipDomain = string.IsNullOrWhiteSpace(_options.SipDomain) ? TelnyxConstants.DefaultSipDomain : _options.SipDomain;

        return $"sip:{credential.SipUsername}@{sipDomain}";
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(TelnyxConstants.ProviderTechnicalName);
        client.BaseAddress = new Uri(_options.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        return client;
    }

    private static ContactCenterVoiceProviderResult Failure(string errorCode, string errorMessage)
        => new()
        {
            Succeeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ProviderName = TelnyxConstants.ProviderTechnicalName,
        };

    private static async Task<string> ReadDataStringAsync(HttpResponseMessage response, string propertyName, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (document.RootElement.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        catch (JsonException)
        {
            // Ignore malformed responses.
        }

        return null;
    }

    private static async Task<string> SafeReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return string.Empty;
        }
    }
}
