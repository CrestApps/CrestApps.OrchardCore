using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.DialPad.Services;

/// <summary>
/// Implements the Contact Center voice provider boundary over the DialPad telephony provider so the
/// Contact Center routes voice work while DialPad executes provider-specific call operations.
/// </summary>
public sealed class DialPadContactCenterVoiceProvider : IContactCenterVoiceProvider
{
    private readonly ITelephonyProviderResolver _telephonyResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialPadContactCenterVoiceProvider"/> class.
    /// </summary>
    /// <param name="telephonyResolver">The telephony provider resolver.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialPadContactCenterVoiceProvider(
        ITelephonyProviderResolver telephonyResolver,
        IStringLocalizer<DialPadContactCenterVoiceProvider> stringLocalizer)
    {
        _telephonyResolver = telephonyResolver;
        Name = stringLocalizer["DialPad"];
    }

    /// <inheritdoc/>
    public string TechnicalName => DialPadConstants.ProviderTechnicalName;

    /// <inheritdoc/>
    public LocalizedString Name { get; }

    /// <inheritdoc/>
    public ContactCenterVoiceProviderCapabilities Capabilities => ContactCenterVoiceProviderCapabilities.DialerDial | ContactCenterVoiceProviderCapabilities.CallTransfer;

    /// <inheritdoc/>
    public VoiceProviderDeliveryModel DeliveryModel => VoiceProviderDeliveryModel.AgentDeviceNative;

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> DialAsync(ContactCenterDialRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await DialCoreAsync(request.Destination, request.CallerId, request.Metadata, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ContactCenterVoiceProviderResult> AssignCallAsync(ContactCenterCallAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Failure("not_supported", "DialPad does not support provider-side Contact Center call assignment."));
    }

    /// <inheritdoc/>
    public Task<ContactCenterVoiceProviderResult> ConnectToAgentAsync(ContactCenterConnectRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // DialPad uses the agent-device-native delivery model: the live call already rings the agent's
        // registered device, so the Contact Center does not bridge media. The agent answers on the soft
        // phone and the connect operation succeeds as a no-op.
        return Task.FromResult(new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = request.ProviderCallId,
        });
    }

    /// <inheritdoc/>
    public Task<ContactCenterVoiceProviderResult> QueueCallAsync(ContactCenterQueueCallRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Failure("not_supported", "DialPad does not support provider-side Contact Center queue placement."));
    }

    private async Task<ContactCenterVoiceProviderResult> DialCoreAsync(
        string destination,
        string callerId,
        IDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var provider = await _telephonyResolver.GetAsync(DialPadConstants.ProviderTechnicalName);

        if (provider is null)
        {
            return Failure("provider_unavailable", "The DialPad telephony provider is not configured.");
        }

        var result = await provider.DialAsync(new DialRequest
        {
            To = destination,
            From = callerId,
            Metadata = metadata,
        }, cancellationToken);

        if (!result.Succeeded)
        {
            return Failure("dial_failed", result.Error);
        }

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = result.Call?.CallId,
        };
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
