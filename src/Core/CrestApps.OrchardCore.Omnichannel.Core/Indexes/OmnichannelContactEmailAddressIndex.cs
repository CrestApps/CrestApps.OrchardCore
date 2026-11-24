using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// One row per email address on a contact or lead, in its canonical form (see <see cref="OmnichannelEmailAddress"/>),
/// so a record can be found by any of its addresses with a single seek: an inbound email from a contact's second
/// address, or an unsubscribe of it, finds the contact as surely as one from the first.
/// <see cref="OmnichannelContactIndex"/> keeps the first address for display and filtering; this index is what a
/// lookup by address reads.
/// </summary>
public sealed class OmnichannelContactEmailAddressIndex : MapIndex, IOmnichannelContactMatch
{
    /// <summary>
    /// The length of the address column, which holds the longest address <see cref="OmnichannelEmailAddress.IsValid"/>
    /// accepts.
    /// </summary>
    public const int EmailAddressLength = 255;

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
    /// Gets or sets the email address in its canonical lower-case form.
    /// </summary>
    public string NormalizedEmailAddress { get; set; }
}
