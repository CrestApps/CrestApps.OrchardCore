using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.DialPad.Services;

/// <summary>
/// Implements the Contact Center dialer-agnostic <see cref="IDialerProvider"/> over the DialPad telephony
/// provider so the Contact Center can place outbound campaign calls through DialPad while owning all
/// assignment, queue, pacing, and compliance logic.
/// </summary>
public sealed class DialPadDialerProvider : IDialerProvider
{
    private readonly ITelephonyProviderResolver _telephonyResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialPadDialerProvider"/> class.
    /// </summary>
    /// <param name="telephonyResolver">The telephony provider resolver.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialPadDialerProvider(
        ITelephonyProviderResolver telephonyResolver,
        IStringLocalizer<DialPadDialerProvider> stringLocalizer)
    {
        _telephonyResolver = telephonyResolver;
        DisplayName = stringLocalizer["DialPad"];
    }

    /// <inheritdoc/>
    public string TechnicalName => DialPadConstants.ProviderTechnicalName;

    /// <inheritdoc/>
    public LocalizedString DisplayName { get; }

    /// <inheritdoc/>
    public DialerProviderCapabilities Capabilities => DialerProviderCapabilities.Outbound | DialerProviderCapabilities.CallerId | DialerProviderCapabilities.Cancellation;

    /// <inheritdoc/>
    public async Task<DialerDialResult> PlaceCallAsync(DialerDialRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = await _telephonyResolver.GetAsync(DialPadConstants.ProviderTechnicalName);

        if (provider is null)
        {
            return DialerDialResult.Failure("provider_unavailable", "The DialPad telephony provider is not configured.");
        }

        var result = await provider.DialAsync(new DialRequest
        {
            To = request.Destination,
            From = request.CallerId,
            Metadata = request.Metadata,
        }, cancellationToken);

        if (!result.Succeeded)
        {
            return DialerDialResult.Failure("dial_failed", result.Error);
        }

        return DialerDialResult.Success(result.Call?.CallId);
    }

    /// <inheritdoc/>
    public async Task<DialerDialResult> EndCallAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        var provider = await _telephonyResolver.GetAsync(DialPadConstants.ProviderTechnicalName);

        if (provider is null)
        {
            return DialerDialResult.Failure("provider_unavailable", "The DialPad telephony provider is not configured.");
        }

        var result = await provider.HangupAsync(new CallReference { CallId = providerCallId }, cancellationToken);

        return result.Succeeded
            ? DialerDialResult.Success(providerCallId)
            : DialerDialResult.Failure("hangup_failed", result.Error);
    }
}
