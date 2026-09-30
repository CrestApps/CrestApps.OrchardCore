namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// A report that a phone number is not in service, and what said so.
/// </summary>
public sealed class NotInServiceMark
{
    /// <summary>
    /// Gets or sets the number. Any form the phone number service can read; it is stored in E.164 form.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets what found the number to be out of service, one of <see cref="OmnichannelConstants.NotInServiceSources"/>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets what the provider or the lookup said, in its own words.
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
    /// Gets or sets the user who marked the number, when a person did.
    /// </summary>
    public string MarkedById { get; set; }

    /// <summary>
    /// Gets or sets the name of the user who marked the number, when a person did.
    /// </summary>
    public string MarkedByUsername { get; set; }
}
