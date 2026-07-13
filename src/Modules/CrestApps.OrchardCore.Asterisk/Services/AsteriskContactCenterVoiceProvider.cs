using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Exposes Asterisk outbound dialing through the Contact Center voice provider boundary.
/// </summary>
public sealed class AsteriskContactCenterVoiceProvider : IContactCenterVoiceProvider
{
    private readonly ITelephonyProviderResolver _telephonyResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskContactCenterVoiceProvider"/> class.
    /// </summary>
    /// <param name="telephonyResolver">The telephony provider resolver.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AsteriskContactCenterVoiceProvider(
        ITelephonyProviderResolver telephonyResolver,
        IStringLocalizer<AsteriskContactCenterVoiceProvider> stringLocalizer)
    {
        _telephonyResolver = telephonyResolver;
        Name = stringLocalizer["Asterisk"];
    }

    /// <inheritdoc/>
    public string TechnicalName => AsteriskConstants.ProviderTechnicalName;

    /// <inheritdoc/>
    public LocalizedString Name { get; }

    /// <inheritdoc/>
    public ContactCenterVoiceProviderCapabilities Capabilities =>
        ContactCenterVoiceProviderCapabilities.DialerDial |
        ContactCenterVoiceProviderCapabilities.BidirectionalMedia;

    /// <inheritdoc/>
    public VoiceProviderDeliveryModel DeliveryModel => VoiceProviderDeliveryModel.AgentDeviceNative;

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> DialAsync(
        ContactCenterDialRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var providerName = AsteriskConstants.ProviderTechnicalName;
        var provider = await _telephonyResolver.GetAsync(providerName);

        if (provider is null)
        {
            providerName = AsteriskConstants.DefaultProviderTechnicalName;
            provider = await _telephonyResolver.GetAsync(providerName);
        }

        if (provider is null)
        {
            return Failure("provider_unavailable", "The Asterisk telephony provider is not configured.");
        }

        var result = await provider.DialAsync(new DialRequest
        {
            To = request.Destination,
            From = request.CallerId,
            Metadata = request.Metadata,
        }, cancellationToken);

        if (!result.Succeeded)
        {
            return Failure("dial_failed", result.Error);
        }

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = result.Call?.CallId,
            ProviderName = providerName,
        };
    }

    /// <inheritdoc/>
    public Task<ContactCenterVoiceProviderResult> ConnectToAgentAsync(
        ContactCenterConnectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = request.ProviderCallId,
        });
    }

    /// <inheritdoc/>
    public Task<ContactCenterVoiceProviderResult> AssignCallAsync(
        ContactCenterCallAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Failure("not_supported", "Asterisk does not support provider-side Contact Center call assignment."));
    }

    /// <inheritdoc/>
    public Task<ContactCenterVoiceProviderResult> QueueCallAsync(
        ContactCenterQueueCallRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Failure("not_supported", "Asterisk does not support provider-side Contact Center queue placement."));
    }

    private static ContactCenterVoiceProviderResult Failure(string errorCode, string errorMessage)
    {
        return new ContactCenterVoiceProviderResult
        {
            Succeeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
        };
    }
}
