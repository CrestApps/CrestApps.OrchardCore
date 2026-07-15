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
    private readonly IContactCenterFeatureWorkManager _workManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskContactCenterVoiceProvider"/> class.
    /// </summary>
    /// <param name="telephonyResolver">The telephony provider resolver.</param>
    /// <param name="workManager">The feature work manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AsteriskContactCenterVoiceProvider(
        ITelephonyProviderResolver telephonyResolver,
        IContactCenterFeatureWorkManager workManager,
        IStringLocalizer<AsteriskContactCenterVoiceProvider> stringLocalizer)
    {
        _telephonyResolver = telephonyResolver;
        _workManager = workManager;
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

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

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
            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                OutcomeUnknown = result.OutcomeUnknown,
                ErrorCode = result.OutcomeUnknown ? "dial_outcome_unknown" : "dial_failed",
                ErrorMessage = result.Error,
                ProviderName = providerName,
            };
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

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Task.FromResult(Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable."));
        }

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
        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Task.FromResult(Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable."));
        }

        return Task.FromResult(Failure("not_supported", "Asterisk does not support provider-side Contact Center call assignment."));
    }

    /// <inheritdoc/>
    public Task<ContactCenterVoiceProviderResult> QueueCallAsync(
        ContactCenterQueueCallRequest request,
        CancellationToken cancellationToken = default)
    {
        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Task.FromResult(Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable."));
        }

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
