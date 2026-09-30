using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// Indexes the published and latest versions of opportunity content items for the pipeline.
/// </summary>
public sealed class OpportunityIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the content item id.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the opportunity content type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets whether the indexed version is published.
    /// </summary>
    public bool Published { get; set; }

    /// <summary>
    /// Gets or sets whether the indexed version is the latest version.
    /// </summary>
    public bool Latest { get; set; }

    /// <summary>
    /// Gets or sets the stage identifier.
    /// </summary>
    public string StageId { get; set; }

    /// <summary>
    /// Gets or sets whether the opportunity is closed.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Gets or sets whether the opportunity is won.
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
    /// Gets or sets the expected close date.
    /// </summary>
    public DateTime? CloseDate { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who owns the opportunity.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the account that contains the opportunity.
    /// </summary>
    public string AccountContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the campaign the opportunity came from.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the primary contact.
    /// </summary>
    public string PrimaryContactItemId { get; set; }

    /// <summary>
    /// Gets or sets the opportunity source.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the lead the opportunity was created from.
    /// </summary>
    public string ConvertedFromLeadItemId { get; set; }

    /// <summary>
    /// Gets or sets when the opportunity was created.
    /// </summary>
    public DateTime? CreatedUtc { get; set; }
}
