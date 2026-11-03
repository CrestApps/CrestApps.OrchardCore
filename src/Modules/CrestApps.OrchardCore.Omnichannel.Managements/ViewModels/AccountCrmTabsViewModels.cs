using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// The Contacts tab of an account's page.
/// </summary>
public class AccountContactsTabViewModel
{
    public ContentItem Account { get; set; }

    public int Count { get; set; }

    public IReadOnlyList<AccountContactRow> Contacts { get; set; } = [];

    /// <summary>
    /// Gets or sets the contact types the user may add to the account.
    /// </summary>
    public IReadOnlyList<ContentTypeDefinition> CreatableTypes { get; set; } = [];
}

/// <summary>
/// One of an account's contacts.
/// </summary>
public sealed record AccountContactRow(ContentItem Contact, string Email, string Phone);

/// <summary>
/// The Opportunities tab of an account's page.
/// </summary>
public class AccountOpportunitiesTabViewModel
{
    public ContentItem Account { get; set; }

    public int Count { get; set; }

    public IReadOnlyList<AccountOpportunityRow> Opportunities { get; set; } = [];

    /// <summary>
    /// Gets or sets the opportunity types the user may add to the account.
    /// </summary>
    public IReadOnlyList<ContentTypeDefinition> CreatableTypes { get; set; } = [];
}

/// <summary>
/// One of an account's opportunities.
/// </summary>
public sealed record AccountOpportunityRow(ContentItem Opportunity, string Stage, decimal? Amount, DateTime? CloseDate, bool IsClosed, bool IsWon);

/// <summary>
/// The Leads tab of an account's page: the leads that became its contacts.
/// </summary>
public class AccountLeadsTabViewModel
{
    public int Count { get; set; }

    public IReadOnlyList<AccountLeadRow> Leads { get; set; } = [];
}

/// <summary>
/// A lead that became one of the account's contacts.
/// </summary>
public sealed record AccountLeadRow(ContentItem Lead, string ContactContentItemId, DateTime? ConvertedUtc);

/// <summary>
/// The Activities tab of an account's page.
/// </summary>
public class AccountActivitiesTabViewModel
{
    public string AccountContentItemId { get; set; }

    public int ScheduledCount { get; set; }

    public int CompletedCount { get; set; }

    public DateTime? NextScheduledUtc { get; set; }

    public DateTime? LastCompletedUtc { get; set; }
}
