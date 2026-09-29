namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Describes how a lead is converted: which contact it becomes, which account the contact joins, whether an
/// opportunity is created, and what happens to the lead's open activities.
/// </summary>
public sealed class LeadConversionRequest
{
    /// <summary>
    /// Gets or sets the content item identifier of the lead to convert.
    /// </summary>
    public string LeadContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of an existing contact to merge the lead into. When empty, a new
    /// contact of <see cref="ContactContentType"/> is created.
    /// </summary>
    public string ExistingContactItemId { get; set; }

    /// <summary>
    /// Gets or sets the contact type a new contact is created as. When empty, the lead type's
    /// <see cref="LeadPartSettings.TargetContactContentType"/> is used.
    /// </summary>
    public string ContactContentType { get; set; }

    /// <summary>
    /// Gets or sets what account the contact joins.
    /// </summary>
    public LeadConversionAccountMode AccountMode { get; set; } = LeadConversionAccountMode.None;

    /// <summary>
    /// Gets or sets the name of a new account. When empty, the lead's company is used.
    /// </summary>
    public string AccountName { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the existing account the contact joins.
    /// </summary>
    public string ExistingAccountItemId { get; set; }

    /// <summary>
    /// Gets or sets whether an opportunity is created with the contact.
    /// </summary>
    public bool CreateOpportunity { get; set; }

    /// <summary>
    /// Gets or sets the opportunity type. When empty, the lead type's
    /// <see cref="LeadPartSettings.DefaultOpportunityContentType"/> is used.
    /// </summary>
    public string OpportunityContentType { get; set; }

    /// <summary>
    /// Gets or sets the opportunity name. When empty, it is built from the company or the lead's name.
    /// </summary>
    public string OpportunityName { get; set; }

    /// <summary>
    /// Gets or sets the opportunity stage. When empty, the type's first open stage is used.
    /// </summary>
    public string OpportunityStageId { get; set; }

    /// <summary>
    /// Gets or sets the opportunity amount.
    /// </summary>
    public decimal? OpportunityAmount { get; set; }

    /// <summary>
    /// Gets or sets the opportunity close date.
    /// </summary>
    public DateTime? OpportunityCloseDate { get; set; }

    /// <summary>
    /// Gets or sets the campaign the opportunity came from.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets what happens to the lead's activities that are not finished yet.
    /// </summary>
    public LeadOpenActivityMode OpenActivities { get; set; } = LeadOpenActivityMode.Move;

    /// <summary>
    /// Gets or sets the identifier of an activity that is being completed by this conversion, such as the
    /// activity whose disposition runs the convert action. It is moved like the others, but its in-progress state
    /// does not block the conversion.
    /// </summary>
    public string CompletingActivityId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user converting the lead.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the user converting the lead.
    /// </summary>
    public string UserName { get; set; }
}

/// <summary>
/// What account a converted lead's contact joins.
/// </summary>
public enum LeadConversionAccountMode
{
    /// <summary>
    /// The contact joins no account.
    /// </summary>
    None,

    /// <summary>
    /// A new account is created, named after the lead's company unless another name is given.
    /// </summary>
    CreateNew,

    /// <summary>
    /// The contact joins an existing account.
    /// </summary>
    UseExisting,

    /// <summary>
    /// An account whose name matches the lead's company is used when there is exactly one, a new one is created
    /// when there is none and the lead has a company, and no account is used otherwise.
    /// </summary>
    Automatic,
}

/// <summary>
/// What happens to a converted lead's activities that are not finished yet.
/// </summary>
public enum LeadOpenActivityMode
{
    /// <summary>
    /// They move to the contact with the lead's history, so the work continues against the contact.
    /// </summary>
    Move,

    /// <summary>
    /// They are cancelled. The lead's finished activities still move to the contact.
    /// </summary>
    Cancel,
}
