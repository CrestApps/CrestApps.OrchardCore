using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the editor of an opportunity.
/// </summary>
public class OpportunityPartViewModel
{
    /// <summary>
    /// Gets or sets the stage identifier.
    /// </summary>
    public string StageId { get; set; }

    /// <summary>
    /// Gets or sets the chance, from 0 to 100, that the opportunity is won.
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
    /// Gets or sets the owner.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the campaign identifier.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the primary contact.
    /// </summary>
    public string PrimaryContactItemId { get; set; }

    /// <summary>
    /// Gets or sets the display text of the primary contact.
    /// </summary>
    [BindNever]
    public string PrimaryContactDisplayText { get; set; }

    /// <summary>
    /// Gets or sets the account the opportunity belongs to, which narrows the contact search.
    /// </summary>
    [BindNever]
    public string AccountContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the stages the editor can choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Stages { get; set; } = [];

    /// <summary>
    /// Gets or sets the campaigns the editor can choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Campaigns { get; set; } = [];

    /// <summary>
    /// Gets or sets the display name of the stage, for the summary.
    /// </summary>
    [BindNever]
    public string StageName { get; set; }

    /// <summary>
    /// Gets or sets whether the opportunity is closed, for the summary.
    /// </summary>
    [BindNever]
    public bool IsClosed { get; set; }

    /// <summary>
    /// Gets or sets whether the opportunity is won, for the summary.
    /// </summary>
    [BindNever]
    public bool IsWon { get; set; }
}
