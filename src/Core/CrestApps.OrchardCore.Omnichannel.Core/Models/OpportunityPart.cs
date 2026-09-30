using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Marks a content type as an opportunity, a deal in progress, and holds the values the pipeline needs. A tenant
/// defines one content type per kind of deal, such as a sales, resell or business opportunity, and each is
/// contained in an account.
/// </summary>
public sealed class OpportunityPart : ContentPart
{
    /// <summary>
    /// Gets or sets the identifier of the opportunity's <see cref="OpportunityStage"/>.
    /// </summary>
    public string StageId { get; set; }

    /// <summary>
    /// Gets or sets whether the opportunity's stage is closed. It is copied from the stage whenever the opportunity
    /// is saved, so the opportunity index can filter without reading the stage catalog.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Gets or sets whether the opportunity's stage is a won stage. It is copied from the stage whenever the
    /// opportunity is saved.
    /// </summary>
    public bool IsWon { get; set; }

    /// <summary>
    /// Gets or sets the chance, from 0 to 100, that the opportunity closes as won.
    /// </summary>
    public int? Probability { get; set; }

    /// <summary>
    /// Gets or sets the expected value of the deal.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the date the deal is expected to close.
    /// </summary>
    public DateTime? CloseDate { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who owns the opportunity.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the lead source the opportunity came from.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the campaign the opportunity came from.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the opportunity's primary contact.
    /// </summary>
    public string PrimaryContactItemId { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the lead this opportunity was created from.
    /// </summary>
    public string ConvertedFromLeadItemId { get; set; }
}
