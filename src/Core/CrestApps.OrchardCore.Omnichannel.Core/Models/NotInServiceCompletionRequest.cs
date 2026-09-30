namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// A request to complete an activity whose call found the number not in service.
/// </summary>
public sealed class NotInServiceCompletionRequest
{
    /// <summary>
    /// Gets or sets the activity to complete.
    /// </summary>
    public OmnichannelActivity Activity { get; set; }

    /// <summary>
    /// Gets or sets the number that was dialed. When not set, the activity's destination is used.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets what dialed the number, one of <see cref="OmnichannelConstants.NotInServiceSources"/>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets what the provider said, in its own words (for example <c>unallocated_number (SIP 404)</c>).
    /// </summary>
    public string Reason { get; set; }
}
