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
    /// Gets or sets whether the indexed content item version is published.
    /// </summary>
    public bool Published { get; set; }

    /// <summary>
    /// Gets or sets whether the indexed content item version is the latest version.
    /// </summary>
    public bool Latest { get; set; }

    /// <summary>
    /// Gets or sets the contact time zone identifier.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the primary cell phone number as national digits.
    /// </summary>
    public string PrimaryCellPhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the normalized primary cell phone number in E.164 format.
    /// </summary>
    public string NormalizedPrimaryCellPhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the primary home phone number as national digits.
    /// </summary>
    public string PrimaryHomePhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the normalized primary home phone number in E.164 format.
    /// </summary>
    public string NormalizedPrimaryHomePhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the primary email address.
    /// </summary>
    public string PrimaryEmailAddress { get; set; }
}
