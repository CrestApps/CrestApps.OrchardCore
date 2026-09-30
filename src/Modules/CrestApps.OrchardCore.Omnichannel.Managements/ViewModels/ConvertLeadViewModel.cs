using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the lead conversion screen.
/// </summary>
public class ConvertLeadViewModel
{
    /// <summary>
    /// Gets or sets the contact to merge into, or empty to create a new contact.
    /// </summary>
    public string ExistingContactItemId { get; set; }

    /// <summary>
    /// Gets or sets the type of a new contact.
    /// </summary>
    public string ContactContentType { get; set; }

    /// <summary>
    /// Gets or sets how the account is chosen.
    /// </summary>
    public LeadConversionAccountMode AccountMode { get; set; }

    /// <summary>
    /// Gets or sets the name of a new account.
    /// </summary>
    public string AccountName { get; set; }

    /// <summary>
    /// Gets or sets the existing account.
    /// </summary>
    public string ExistingAccountItemId { get; set; }

    /// <summary>
    /// Gets or sets whether an opportunity is created.
    /// </summary>
    public bool CreateOpportunity { get; set; }

    /// <summary>
    /// Gets or sets the opportunity type.
    /// </summary>
    public string OpportunityContentType { get; set; }

    /// <summary>
    /// Gets or sets the opportunity name.
    /// </summary>
    public string OpportunityName { get; set; }

    /// <summary>
    /// Gets or sets the opportunity amount.
    /// </summary>
    public decimal? OpportunityAmount { get; set; }

    /// <summary>
    /// Gets or sets the opportunity close date.
    /// </summary>
    public DateTime? OpportunityCloseDate { get; set; }

    /// <summary>
    /// Gets or sets what happens to the lead's open activities.
    /// </summary>
    public LeadOpenActivityMode OpenActivities { get; set; } = LeadOpenActivityMode.Move;

    /// <summary>
    /// Gets or sets the lead.
    /// </summary>
    [BindNever]
    public ContentItem Lead { get; set; }

    /// <summary>
    /// Gets or sets the lead's company.
    /// </summary>
    [BindNever]
    public string Company { get; set; }

    /// <summary>
    /// Gets or sets the contacts that share the lead's phone number or email.
    /// </summary>
    [BindNever]
    public IList<ContentItem> MatchingContacts { get; set; } = [];

    /// <summary>
    /// Gets or sets the accounts named after the lead's company.
    /// </summary>
    [BindNever]
    public IList<ContentItem> MatchingAccounts { get; set; } = [];

    /// <summary>
    /// Gets or sets the contact types a new contact can be created as.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ContactContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the opportunity types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> OpportunityContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets whether any account type exists.
    /// </summary>
    [BindNever]
    public bool HasAccountTypes { get; set; }

    /// <summary>
    /// Gets or sets the number of the lead's open activities.
    /// </summary>
    [BindNever]
    public int OpenActivityCount { get; set; }

    /// <summary>
    /// Gets or sets the number of the lead's finished activities.
    /// </summary>
    [BindNever]
    public int FinishedActivityCount { get; set; }
}
