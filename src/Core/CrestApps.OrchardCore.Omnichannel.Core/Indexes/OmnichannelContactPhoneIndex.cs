using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// Represents searchable phone numbers for an omnichannel contact content item version.
/// </summary>
public sealed class OmnichannelContactPhoneIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the content item identifier.
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
    /// Gets or sets the primary cell phone number in E.164 format.
    /// </summary>
    public string E164PrimaryCellPhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the primary cell phone number in national format.
    /// </summary>
    public string NationalPrimaryCellPhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the primary home phone number in E.164 format.
    /// </summary>
    public string E164PrimaryHomePhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the primary home phone number in national format.
    /// </summary>
    public string NationalPrimaryHomePhoneNumber { get; set; }
}
