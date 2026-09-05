using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents the result of a Contact Center voice provider operation.
/// </summary>
public sealed class ContactCenterVoiceProviderResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the provider operation succeeded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider may have executed the operation but its outcome could not be observed.
    /// </summary>
    public bool OutcomeUnknown { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier returned by the provider.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the provider identifier of the call leg the operation created, when it created one, such
    /// as a supervisor's monitoring leg or the private leg of a consult.
    /// </summary>
    public string ProviderLegId { get; set; }

    /// <summary>
    /// Gets or sets the state the provider observed on the leg named by <see cref="ProviderLegId"/>, when the
    /// provider reports one. A provider that only originates the leg -- the invite is accepted for delivery but
    /// the endpoint has not picked up -- reports <see cref="VoiceCallState.Dialing"/> or
    /// <see cref="VoiceCallState.Ringing"/> here, so the topology records a leg that is still being reached
    /// instead of asserting a party who is already talking. <see langword="null"/> when the provider does not
    /// report a leg state, in which case a leg it returned is taken to be answered, because the operation that
    /// created it is one that answers.
    /// </summary>
    public VoiceCallState? ProviderLegState { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the provider that executed the operation.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider error code when the operation failed.
    /// </summary>
    public string ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the provider error message when the operation failed.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets provider-specific result metadata.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
