using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Marks a contact-capable content type as a lead and holds the lead's state. A lead type also carries
/// <see cref="OmnichannelContactPart"/>, so it can be called, texted and loaded into activities exactly like a
/// contact, while it is listed, matched and reported apart from contacts until it is converted.
/// </summary>
public sealed class LeadPart : ContentPart
{
    /// <summary>
    /// Gets or sets the identifier of the lead's <see cref="LeadStatus"/>.
    /// </summary>
    public string StatusId { get; set; }

    /// <summary>
    /// Gets or sets whether the lead's status is closed. It is copied from the status whenever the lead is saved,
    /// so the lead index can filter closed leads without reading the status catalog.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Gets or sets where the lead came from, such as a web form, a purchased list or a trade show.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the name of the list or import the lead arrived in, so a list can be loaded, reported on and
    /// cleaned up as one unit.
    /// </summary>
    public string ListName { get; set; }

    /// <summary>
    /// Gets or sets the company the lead works for. It names the account created when the lead is converted.
    /// </summary>
    public string Company { get; set; }

    /// <summary>
    /// Gets or sets the lead's rating, one of the <see cref="LeadRatings"/> values.
    /// </summary>
    public string Rating { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who owns the lead.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets whether the lead was converted. A converted lead is read-only and hidden from the lead list,
    /// inventory loads and caller matching.
    /// </summary>
    public bool IsConverted { get; set; }

    /// <summary>
    /// Gets or sets when the lead was converted, in UTC.
    /// </summary>
    public DateTime? ConvertedUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who converted the lead.
    /// </summary>
    public string ConvertedById { get; set; }

    /// <summary>
    /// Gets or sets the user name of the user who converted the lead.
    /// </summary>
    public string ConvertedByUsername { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the contact the lead became or was merged into.
    /// </summary>
    public string ConvertedContactItemId { get; set; }

    /// <summary>
    /// Gets or sets the content type of the contact the lead became or was merged into.
    /// </summary>
    public string ConvertedContactType { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the account the converted contact was placed in.
    /// </summary>
    public string ConvertedAccountItemId { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the opportunity created when the lead was converted.
    /// </summary>
    public string ConvertedOpportunityItemId { get; set; }

    /// <summary>
    /// Gets or sets when the lead's numbers were last checked against a do-not-call registry, in UTC. An import that
    /// checks registries sets it, including for a row it imports marked Do not call.
    /// </summary>
    public DateTime? LastScrubbedUtc { get; set; }
}
