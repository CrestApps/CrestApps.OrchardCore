namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// The lead filters of an inventory load whose contact type is a lead type. It is stored on the batch, so it only
/// exists for loads that target leads.
/// </summary>
public sealed class LeadBatchFilter
{
    /// <summary>
    /// Gets or sets the statuses to load. When empty, every open status is loaded.
    /// </summary>
    public string[] StatusIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether leads in a closed status are loaded too.
    /// </summary>
    public bool IncludeClosedLeads { get; set; }

    /// <summary>
    /// Gets or sets the list to load, such as a purchased list imported earlier.
    /// </summary>
    public string ListName { get; set; }

    /// <summary>
    /// Gets or sets the lead source to load.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the owner whose leads are loaded.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the ratings to load. When empty, every rating is loaded.
    /// </summary>
    public string[] Ratings { get; set; } = [];

    /// <summary>
    /// Gets or sets whether a lead whose phone number already belongs to a contact is skipped, so a customer is not
    /// called again as a stranger.
    /// </summary>
    public bool SkipLeadsThatAreContacts { get; set; } = true;
}
