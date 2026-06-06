using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// Represents the omnichannel contact index.
/// </summary>
public sealed class OmnichannelContactIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the content item id.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the contact time zone identifier.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the primary cell phone number.
    /// </summary>
    public string PrimaryCellPhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the normalized primary cell phone number.
    /// </summary>
    public string NormalizedPrimaryCellPhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the primary home phone number.
    /// </summary>
    public string PrimaryHomePhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the normalized primary home phone number.
    /// </summary>
    public string NormalizedPrimaryHomePhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the primary email address.
    /// </summary>
    public string PrimaryEmailAddress { get; set; }
}
