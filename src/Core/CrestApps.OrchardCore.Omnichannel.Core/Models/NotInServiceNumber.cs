using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// A phone number known not to be in service: the network said it is unallocated, disconnected or invalid when it
/// was dialed, a number lookup said so, or somebody marked it by hand.
/// </summary>
/// <remarks>
/// Recorded against the number rather than the contact. A dead number is dead whoever it is filed under, the same
/// number is often on file for more than one contact, and a contact with one dead number and one working number is
/// still reachable on the working one. Keeping the mark off the contact record also means nothing writes to a
/// contact in the background while an agent may be editing it.
/// </remarks>
public sealed class NotInServiceNumber : CatalogItem
{
    /// <summary>
    /// Gets or sets the number, in E.164 form.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets what found the number to be out of service, one of <see cref="OmnichannelConstants.NotInServiceSources"/>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets what the provider or the lookup said, in its own words (for example
    /// <c>unallocated_number (SIP 404)</c>), so the mark can be checked against the evidence.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the activity whose attempt found the number out of service, when one did.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the campaign of that activity, when it had one.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the contact the number was dialed for, when known.
    /// </summary>
    public string ContactContentItemId { get; set; }

    /// <summary>
    /// Gets or sets when the number was first found out of service.
    /// </summary>
    public DateTime FirstDetectedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the number was most recently found out of service.
    /// </summary>
    public DateTime LastDetectedUtc { get; set; }

    /// <summary>
    /// Gets or sets how many times the number has been found out of service.
    /// </summary>
    public int DetectionCount { get; set; }

    /// <summary>
    /// Gets or sets the user who marked the number, when a person did.
    /// </summary>
    public string MarkedById { get; set; }

    /// <summary>
    /// Gets or sets the name of the user who marked the number, when a person did.
    /// </summary>
    public string MarkedByUsername { get; set; }
}
