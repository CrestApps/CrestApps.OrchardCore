using CrestApps.Core.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Timeline.Models;

/// <summary>
/// One entry on the timeline of a record: a comment, an internal note or an event, with who wrote it, what it says,
/// the field changes it records and its attachments. Entries are kept as written; a body can only be redacted.
/// </summary>
public sealed class TimelineEntry : CatalogItem
{
    /// <summary>
    /// Gets or sets the record the entry is on.
    /// </summary>
    public RecordRef Subject { get; set; }

    /// <summary>
    /// Gets or sets what the entry is: one of <see cref="TimelineConstants.Kinds"/> or a kind a module adds.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets who may read it.
    /// </summary>
    public TimelineVisibility Visibility { get; set; }

    /// <summary>
    /// Gets or sets who wrote it or did what it records; empty for the site itself.
    /// </summary>
    public PartyRef Author { get; set; }

    /// <summary>
    /// Gets or sets the author's name when the entry was written, for an author no party source knows, such as a guest
    /// who wrote by e-mail or a person imported from another system.
    /// </summary>
    public string AuthorName { get; set; }

    /// <summary>
    /// Gets or sets the text, as plain text with line breaks.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the field changes the entry records.
    /// </summary>
    public List<TimelineFieldChange> Changes { get; set; } = [];

    /// <summary>
    /// Gets or sets the ids of its attachments.
    /// </summary>
    public List<string> AttachmentIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the channel it came in or went out on, such as <c>Email</c> or <c>Portal</c>; empty for the admin.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the id the entry has outside the site, such as the message id of an e-mail it came in as, so a
    /// channel can find it again (threading, delivery status). At most <see cref="TimelineConstants.MaxExternalIdLength"/>
    /// characters.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Gets or sets when it was written, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when its body was redacted, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? RedactedUtc { get; set; }

    /// <summary>
    /// Gets or sets who redacted its body.
    /// </summary>
    public PartyRef RedactedBy { get; set; }
}
