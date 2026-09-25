using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Default <see cref="ITelephonyService"/> implementation that resolves the configured default
/// provider and delegates each operation to it.
/// </summary>
public sealed class DefaultTelephonyService : ITelephonyService
{
    private readonly ITelephonyProviderResolver _resolver;
    private readonly IOutboundCallScreeningService _screeningService;
    private readonly ITelephonyExtensionResolver _extensionResolver;
    private readonly IDialDestinationPolicy _destinationPolicy;
    private readonly ITelephonyVoicemailSendGuard _voicemailSendGuard;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyService"/> class.
    /// </summary>
    /// <param name="resolver">The provider resolver.</param>
    /// <param name="screeningService">The outbound origination screening gate.</param>
    /// <param name="extensionResolver">The internal extension resolver.</param>
    /// <param name="destinationPolicy">The safety policy deciding which destinations may be reached.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultTelephonyService(
        ITelephonyProviderResolver resolver,
        IOutboundCallScreeningService screeningService,
        ITelephonyExtensionResolver extensionResolver,
        IDialDestinationPolicy destinationPolicy,
        IStringLocalizer<DefaultTelephonyService> stringLocalizer)
    {
        _resolver = resolver;
        _screeningService = screeningService;
        _extensionResolver = extensionResolver;
        _destinationPolicy = destinationPolicy;
        S = stringLocalizer;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyService"/> class that sends each call to voicemail
    /// once, however many requests ask for it.
    /// </summary>
    /// <param name="resolver">The provider resolver.</param>
    /// <param name="screeningService">The outbound origination screening gate.</param>
    /// <param name="extensionResolver">The internal extension resolver.</param>
    /// <param name="destinationPolicy">The safety policy deciding which destinations may be reached.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="voicemailSendGuard">The guard that lets a call be sent to voicemail once.</param>
    /// <param name="logger">The logger.</param>
    public DefaultTelephonyService(
        ITelephonyProviderResolver resolver,
        IOutboundCallScreeningService screeningService,
        ITelephonyExtensionResolver extensionResolver,
        IDialDestinationPolicy destinationPolicy,
        IStringLocalizer<DefaultTelephonyService> stringLocalizer,
        ITelephonyVoicemailSendGuard voicemailSendGuard,
        ILogger<DefaultTelephonyService> logger)
        : this(resolver, screeningService, extensionResolver, destinationPolicy, stringLocalizer)
    {
        _voicemailSendGuard = voicemailSendGuard;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The destination policy runs first and applies to every origination, including one typed on the soft
        // phone keypad, so emergency and premium destinations cannot be reached from the platform at all.
        var decision = _destinationPolicy.Evaluate(request.To, new DialDestinationContext
        {
            Operation = DialDestinationOperation.Dial,
        });

        if (!decision.IsAllowed)
        {
            return TelephonyResult.Failed(decision.Reason);
        }

        // Every origination passes the shared compliance gate before it can reach a provider, so a soft-phone
        // dial cannot bypass the do-not-call and calling-window rules that a compliance module contributes.
        var screening = await _screeningService.ScreenAsync(
            new OutboundCallScreeningContext
            {
                Request = request,
                Origin = OutboundCallOrigin.SoftPhone,
            },
            cancellationToken);

        if (screening is null || !screening.IsAllowed)
        {
            return TelephonyResult.Failed(
                screening?.Description ?? S["This call cannot be placed because it did not pass outbound compliance screening."].Value);
        }

        return await InvokeAsync<ITelephonyCallControlProvider>(
            TelephonyCapabilities.Dial,
            (provider, token) => provider.DialAsync(request, token),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync<ITelephonyCallControlProvider>(
            TelephonyCapabilities.Hangup,
            (provider, token) => provider.HangupAsync(call, token),
            cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync<ITelephonyHoldProvider>(
            TelephonyCapabilities.Hold,
            (provider, token) => provider.HoldAsync(call, token),
            cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync<ITelephonyHoldProvider>(
            TelephonyCapabilities.Resume,
            (provider, token) => provider.ResumeAsync(call, token),
            cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync<ITelephonyMuteProvider>(
            TelephonyCapabilities.Mute,
            (provider, token) => provider.MuteAsync(call, token),
            cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync<ITelephonyMuteProvider>(
            TelephonyCapabilities.Mute,
            (provider, token) => provider.UnmuteAsync(call, token),
            cancellationToken);

    /// <inheritdoc/>
    public async Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // An extension is a colleague, not a phone number: it is resolved to the user it rings, so the provider can
        // reach that user's own endpoint the way an extension call does, and one nobody owns fails closed here.
        if (request.IsExtension)
        {
            var resolution = await _extensionResolver.ResolveAsync(request.GetExtension(), cancellationToken);

            if (!resolution.Found)
            {
                return TelephonyResult.Failed(S["Extension {0} was not found.", request.To].Value);
            }

            request.To = resolution.Number;
            request.TargetUserId = resolution.UserId;
        }

        return await TransferToDestinationAsync(request, cancellationToken);
    }

    private Task<TelephonyResult> TransferToDestinationAsync(TransferRequest request, CancellationToken cancellationToken)
    {
        // A transfer reaches the outside world exactly like a dial does, so it answers to the same policy. The
        // transfer field on the soft phone therefore cannot be used to reach a refused destination. A target the
        // policy cannot parse is left alone here, because a transfer target may also be an internal address the
        // provider resolves; the refused categories are what this guard exists for.
        var decision = _destinationPolicy.Evaluate(request.To, new DialDestinationContext
        {
            Operation = DialDestinationOperation.Transfer,
        });

        if (decision.Outcome is DialDestinationOutcome.Emergency
            or DialDestinationOutcome.Premium
            or DialDestinationOutcome.Blocked)
        {
            return Task.FromResult(TelephonyResult.Failed(decision.Reason));
        }

        // Consulting the destination before releasing the call is a different provider capability from simply
        // releasing it, so the two modes are gated separately instead of one contract answering for both.
        if (request.Mode == TransferMode.Warm)
        {
            return InvokeAsync<ITelephonyAttendedTransferProvider>(
                TelephonyCapabilities.AttendedTransfer,
                (provider, token) => provider.StartAttendedTransferAsync(request, token),
                cancellationToken);
        }

        return InvokeAsync<ITelephonyTransferProvider>(
            TelephonyCapabilities.Transfer,
            (provider, token) => provider.TransferAsync(request, token),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TelephonyResult> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
        => InvokeAsync<ITelephonyConferenceProvider>(
            TelephonyCapabilities.Merge,
            (provider, token) => provider.MergeAsync(request, token),
            cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> SendDigitsAsync(SendDigitsRequest request, CancellationToken cancellationToken = default)
        => InvokeAsync<ITelephonyDtmfProvider>(
            TelephonyCapabilities.SendDigits,
            (provider, token) => provider.SendDigitsAsync(request, token),
            cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> AnswerAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync<ITelephonyInboundCallProvider>(
            TelephonyCapabilities.ReceiveCalls,
            (provider, token) => provider.AnswerAsync(call, token),
            cancellationToken);

    /// <inheritdoc/>
    public Task<TelephonyResult> RejectAsync(CallReference call, CancellationToken cancellationToken = default)
        => InvokeAsync<ITelephonyInboundCallProvider>(
            TelephonyCapabilities.ReceiveCalls,
            (provider, token) => provider.RejectAsync(call, token),
            cancellationToken);

    /// <inheritdoc/>
    public async Task<TelephonyResult> SendToVoicemailAsync(CallReference call, CancellationToken cancellationToken = default)
    {
        var callId = call?.CallId;

        if (_voicemailSendGuard is null || string.IsNullOrWhiteSpace(callId))
        {
            return await SendToVoicemailCoreAsync(call, cancellationToken);
        }

        // Sending a caller to voicemail answers their leg and plays the greeting. A second request for a call already
        // on its way to voicemail would answer and greet them again, so it is told the call is there instead.
        if (!await _voicemailSendGuard.TryClaimAsync(callId, cancellationToken))
        {
            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(
                    "Call {CallId} is already being sent to voicemail; the repeated request was not sent to the provider.",
                    callId.SanitizeLogValue());
            }

            return TelephonyResult.Success(new TelephonyCall
            {
                CallId = callId,
                State = CallState.Connected,
                Direction = CallDirection.Inbound,
                Metadata = call.Metadata ?? new Dictionary<string, object>(),
            });
        }

        TelephonyResult result = null;

        try
        {
            result = await SendToVoicemailCoreAsync(call, cancellationToken);
        }
        finally
        {
            // Only a call that reached voicemail is held against a later request; a failed attempt may be retried.
            if (result?.Succeeded != true)
            {
                await _voicemailSendGuard.ReleaseAsync(callId, CancellationToken.None);
            }
        }

        return result;
    }

    private Task<TelephonyResult> SendToVoicemailCoreAsync(CallReference call, CancellationToken cancellationToken)
        => InvokeAsync<ITelephonyVoicemailProvider>(
            TelephonyCapabilities.Voicemail,
            (provider, token) => provider.SendToVoicemailAsync(call, token),
            cancellationToken);

    /// <inheritdoc/>
    public async Task<TelephonyResult> DialExtensionAsync(ExtensionDialRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Extension))
        {
            return TelephonyResult.Failed(S["An extension is required to place an internal call."].Value);
        }

        // An emergency or premium code typed into the extension field is refused with the reason that explains
        // why, rather than reported as an unknown extension.
        var decision = _destinationPolicy.Evaluate(request.Extension, new DialDestinationContext
        {
            Operation = DialDestinationOperation.Dial,
        });

        if (decision.Outcome is DialDestinationOutcome.Emergency
            or DialDestinationOutcome.Premium
            or DialDestinationOutcome.Blocked)
        {
            return TelephonyResult.Failed(decision.Reason);
        }

        // Internal extension calls are not consumer outreach, so they skip outbound compliance screening. The
        // extension must resolve to an enabled on-platform user, otherwise the call fails closed here rather
        // than reaching a provider with an unknown destination.
        var resolution = await _extensionResolver.ResolveAsync(request.Extension, cancellationToken);

        if (!resolution.Found)
        {
            return TelephonyResult.Failed(S["Extension {0} was not found.", request.Extension].Value);
        }

        request.TargetUserId = resolution.UserId;
        request.TargetDisplayName = resolution.DisplayName;

        var result = await InvokeAsync<ITelephonyExtensionDialProvider>(
            TelephonyCapabilities.ExtensionDial,
            (provider, token) => provider.DialExtensionAsync(request, token),
            cancellationToken);

        // Carry the dialed extension number on the resulting call so it is recorded as an extension interaction
        // (the call's To holds the target's display name, not a dialable number) and can be redialed by extension
        // from the Recent tab.
        if (result?.Call is not null && !string.IsNullOrWhiteSpace(request.Extension))
        {
            result.Call.Metadata ??= new Dictionary<string, object>();
            result.Call.Metadata[TelephonyConstants.CallMetadata.ExtensionNumber] = request.Extension;
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<TelephonyResult> AddExtensionToConferenceAsync(ExtensionConferenceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Extension))
        {
            return TelephonyResult.Failed(S["An extension is required to add to the conference."].Value);
        }

        if (request.ActiveCall is null || string.IsNullOrWhiteSpace(request.ActiveCall.CallId))
        {
            return TelephonyResult.Failed(S["An active call is required to add an extension to a conference."].Value);
        }

        var resolution = await _extensionResolver.ResolveAsync(request.Extension, cancellationToken);

        if (!resolution.Found)
        {
            return TelephonyResult.Failed(S["Extension {0} was not found.", request.Extension].Value);
        }

        request.TargetUserId = resolution.UserId;
        request.TargetDisplayName = resolution.DisplayName;

        return await InvokeAsync<ITelephonyExtensionDialProvider>(
            TelephonyCapabilities.ExtensionConference,
            (provider, token) => provider.AddExtensionToConferenceAsync(request, token),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var provider = await _resolver.GetAsync();

        if (provider is not ITelephonySoftPhoneCredentialsProvider credentialsProvider)
        {
            return null;
        }

        var credentials = await credentialsProvider.GetClientCredentialsAsync(cancellationToken);

        if (credentials is null)
        {
            return null;
        }

        if (provider is ITelephonyAudioProvider audioProvider)
        {
            credentials.AudioCapabilities = audioProvider.AudioCapabilities;
            credentials.AudioMode = TelephonyAudioModeResolver.Resolve(
                audioProvider.AudioCapabilities,
                audioProvider.ConfiguredAudioMode,
                audioProvider.BrowserMediaAdapterName);
            credentials.BrowserMediaAdapterName = audioProvider.BrowserMediaAdapterName;
        }

        return credentials;
    }

    /// <inheritdoc/>
    public async Task<TelephonyDirectoryResult> GetDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var provider = await _resolver.GetAsync();

        if (provider is null)
        {
            return new TelephonyDirectoryResult
            {
                Succeeded = false,
                Error = S["No telephony provider is configured."].Value,
            };
        }

        if (!provider.Capabilities.HasFlag(TelephonyCapabilities.Directory) ||
            provider is not ITelephonyDirectoryProvider directoryProvider)
        {
            return new TelephonyDirectoryResult
            {
                Succeeded = false,
                Error = S["The configured telephony provider does not support directory lookup."].Value,
            };
        }

        return await directoryProvider.GetDirectoryAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TelephonyCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var provider = await _resolver.GetAsync();

        return provider?.Capabilities ?? TelephonyCapabilities.None;
    }

    private async Task<TelephonyResult> InvokeAsync<TContract>(
        TelephonyCapabilities requiredCapability,
        Func<TContract, CancellationToken, Task<TelephonyResult>> operation,
        CancellationToken cancellationToken)
        where TContract : class
    {
        var provider = await _resolver.GetAsync();

        if (provider is null)
        {
            return TelephonyResult.Failed(S["No telephony provider is configured."].Value);
        }

        // Advertising a capability is a claim, not an implementation. Both must hold, so a provider that
        // advertises an operation it cannot execute is refused rather than dispatched to a missing contract.
        if (!provider.Capabilities.HasFlag(requiredCapability) || provider is not TContract contract)
        {
            return TelephonyResult.Failed(S["The configured telephony provider does not support this operation."].Value);
        }

        return await operation(contract, cancellationToken);
    }
}
