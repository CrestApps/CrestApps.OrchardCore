using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Default <see cref="IVoiceContactCenterCallRouter"/> and <see cref="IInboundVoiceService"/> implementation.
/// It is a thin routing facade: outbound dial requests go to provider implementations, inbound events are
/// processed by <see cref="IInboundVoiceCallProcessor"/>, and agent offering is delegated to
/// <see cref="IVoiceQueueOfferService"/>, while Telephony remains responsible for media execution.
/// </summary>
public sealed class VoiceContactCenterCallRouter : IVoiceContactCenterCallRouter, IInboundVoiceService
{
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IInboundVoiceCallProcessor _inboundProcessor;
    private readonly IVoiceQueueOfferService _offerService;
    private readonly IContactCenterFeatureWorkManager _workManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceContactCenterCallRouter"/> class.
    /// </summary>
    /// <param name="voiceProviderResolver">The voice provider resolver used for outbound voice calls.</param>
    /// <param name="inboundProcessor">The processor that routes inbound voice events into Contact Center work.</param>
    /// <param name="offerService">The offer service used to reserve and offer queued calls to available agents.</param>
    /// <param name="workManager">The feature work manager used to reject outbound routing while Voice is quiescing.</param>
    public VoiceContactCenterCallRouter(
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IInboundVoiceCallProcessor inboundProcessor,
        IVoiceQueueOfferService offerService,
        IContactCenterFeatureWorkManager workManager)
    {
        _voiceProviderResolver = voiceProviderResolver;
        _inboundProcessor = inboundProcessor;
        _offerService = offerService;
        _workManager = workManager;
    }

    /// <inheritdoc/>
    public bool CanRouteOutbound(string providerName = null)
    {
        var provider = _voiceProviderResolver.Get(providerName);

        return provider is IContactCenterVoiceCallControlProvider &&
            provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.DialerDial);
    }

    /// <inheritdoc/>
    public string GetOutboundProviderName(string providerName = null)
    {
        return _voiceProviderResolver.Get(providerName)?.TechnicalName;
    }

    /// <inheritdoc/>
    public Task<InboundVoiceRoutingResult> HandleInboundAsync(InboundVoiceEvent inboundEvent, CancellationToken cancellationToken = default)
    {
        return _inboundProcessor.RouteInboundAsync(inboundEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<InboundVoiceRoutingResult> RouteInboundAsync(InboundVoiceEvent inboundEvent, CancellationToken cancellationToken = default)
    {
        return _inboundProcessor.RouteInboundAsync(inboundEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> OfferNextAsync(string queueId, CancellationToken cancellationToken = default)
    {
        return _offerService.OfferNextAsync(queueId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> OfferToAgentAsync(
        string activityItemId,
        string queueId,
        string agentId,
        int? ringTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        return _offerService.OfferToAgentAsync(activityItemId, queueId, agentId, ringTimeoutSeconds, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> RouteOutboundAsync(
        ContactCenterDialRequest request,
        string providerName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(ContactCenterConstants.Feature.Voice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Contact Center Voice feature is temporarily unavailable.");
        }

        var provider = _voiceProviderResolver.Get(providerName);

        if (provider is null)
        {
            return Failure("provider_unavailable", "No Contact Center voice provider is registered for outbound voice routing.");
        }

        if (!provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.DialerDial) ||
            provider is not IContactCenterVoiceCallControlProvider callControlProvider)
        {
            return Failure("dialing_not_supported", "The Contact Center voice provider does not support outbound dialing.");
        }

        var result = await callControlProvider.DialAsync(request, cancellationToken);

        return result ?? Failure("provider_returned_no_result", "The Contact Center voice provider did not return a result.");
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
