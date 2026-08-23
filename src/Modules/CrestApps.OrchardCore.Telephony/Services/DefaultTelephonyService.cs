using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

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

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyService"/> class.
    /// </summary>
    /// <param name="resolver">The provider resolver.</param>
    /// <param name="screeningService">The outbound origination screening gate.</param>
    /// <param name="extensionResolver">The internal extension resolver.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultTelephonyService(
        ITelephonyProviderResolver resolver,
        IOutboundCallScreeningService screeningService,
        ITelephonyExtensionResolver extensionResolver,
        IStringLocalizer<DefaultTelephonyService> stringLocalizer)
    {
        _resolver = resolver;
        _screeningService = screeningService;
        _extensionResolver = extensionResolver;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    public async Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

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
    public Task<TelephonyResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        // Consulting the destination before releasing the call is a different provider capability from simply
        // releasing it, so the two modes are gated separately instead of one contract answering for both.
        if (request?.Mode == TransferMode.Warm)
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
    public Task<TelephonyResult> SendToVoicemailAsync(CallReference call, CancellationToken cancellationToken = default)
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

        return await InvokeAsync<ITelephonyExtensionDialProvider>(
            TelephonyCapabilities.ExtensionDial,
            (provider, token) => provider.DialExtensionAsync(request, token),
            cancellationToken);
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
