using CrestApps.OrchardCore.Webhooks;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Holds back the provider webhook inbox retries while Contact Center Voice is quiescing, the way the inbox task was
/// gated when it was part of Contact Center: the retries run voice event handlers, which must not start while the
/// Voice feature is being disabled or its shell replaced and its in-flight work drains.
/// </summary>
internal sealed class VoiceProviderWebhookInboxDispatchGate : IProviderWebhookInboxDispatchGate
{
    private readonly IContactCenterFeatureWorkManager _workManager;

    public VoiceProviderWebhookInboxDispatchGate(IContactCenterFeatureWorkManager workManager)
    {
        _workManager = workManager;
    }

    /// <inheritdoc/>
    public IDisposable TryEnter()
        => _workManager.TryEnter(ContactCenterConstants.Feature.Voice);
}
