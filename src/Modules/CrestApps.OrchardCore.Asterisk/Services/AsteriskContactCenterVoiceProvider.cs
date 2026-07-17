using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Exposes Asterisk outbound dialing through the Contact Center voice provider boundary.
/// </summary>
public sealed class AsteriskContactCenterVoiceProvider :
    IContactCenterVoiceProvider,
    IContactCenterVoiceCallControlProvider
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
    public ContactCenterVoiceProviderCapabilities Capabilities => ContactCenterVoiceProviderCapabilities.DialerDial;

    /// <inheritdoc/>
    public VoiceProviderDeliveryModel DeliveryModel => VoiceProviderDeliveryModel.ServerSideAcd;

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

    /// <summary>
    /// Connects a live, server-side-parked provider call to the selected agent.
    /// </summary>
    /// <remarks>
    /// The Asterisk adapter delivers agent media through dynamically provisioned WebRTC PJSIP endpoints
    /// (see <see cref="IAsteriskPjsipRealtimeCredentialStore"/>). Those endpoints are created just in time
    /// per browser session and are not addressable from a static Asterisk queue or dialplan, so bridging a
    /// parked call to the agent legitimately requires a .NET-side ARI originate/bridge rather than
    /// delegation to Asterisk's own ACD. That ARI originate/bridge implementation is scheduled for
    /// Plan-2 Part 3. Until it lands, this method fails closed so the orchestration layer never records a
    /// false success for a call whose media was never bridged to the agent.
    /// </remarks>
    /// <param name="request">The connect request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A failed provider result indicating browser-media agent bridging is not yet available.</returns>
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

        // Part 3: Replace this fail-closed guard with a real ARI originate/bridge that rings the agent's
        // WebRTC PJSIP endpoint and bridges it to the parked provider call. Returning a bare success here
        // would silently report a connected agent while no media is bridged.
        return Task.FromResult(new ContactCenterVoiceProviderResult
        {
            Succeeded = false,
            ErrorCode = "agent_bridge_unavailable",
            ErrorMessage = "Browser-media agent bridging is not yet available for the Asterisk Contact Center voice provider.",
            ProviderCallId = request.ProviderCallId,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
        });
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
