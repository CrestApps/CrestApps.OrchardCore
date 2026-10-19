using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// One row per phone number on a contact or lead, whatever type the number is saved as, so a record can be found by
/// any of its numbers with a single seek. <see cref="OmnichannelContactIndex"/> keeps the first cell and home numbers
/// for display and filtering; this index is what a lookup by number reads.
/// </summary>
public sealed class OmnichannelContactPhoneNumberIndex : MapIndex, IOmnichannelContactMatch
{
    /// <summary>
    /// Gets or sets the id of the content item version the row was written for.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the content item id.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the content type of the indexed item, so a lookup can tell a lead from a contact without
    /// loading the item.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets whether the indexed item is a lead that was converted.
    /// </summary>
    public bool IsConverted { get; set; }

    /// <summary>
    /// Gets or sets whether the indexed content item version is published.
    /// </summary>
    public bool Published { get; set; }

    /// <summary>
    /// Gets or sets whether the indexed content item version is the latest version.
    /// </summary>
    public bool Latest { get; set; }

    /// <summary>
    /// Gets or sets the type the number is saved as, such as <c>Cell</c>, <c>Home</c> or <c>Work</c>.
    /// </summary>
    public string PhoneType { get; set; }

    /// <summary>
    /// Gets or sets the phone number in E.164 format.
    /// </summary>
    public string NormalizedPhoneNumber { get; set; }
}
