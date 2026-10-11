using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// The filter applied to the subscription agreements report.
/// </summary>
public sealed class SubscriptionAgreementsIndexOptions
{
    /// <summary>
    /// Gets or sets the status to filter by, where an empty value means every status.
    /// </summary>
    public SubscriptionStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets a title search term.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the status filter list.
    /// </summary>
    public IList<SelectListItem> Statuses { get; set; } = [];
}

/// <summary>
/// The subscription agreements report.
/// </summary>
public sealed class SubscriptionAgreementsIndexViewModel
{
    /// <summary>
    /// Gets or sets the agreements on this page.
    /// </summary>
    public IList<Subscription> Subscriptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the filter options.
    /// </summary>
    public SubscriptionAgreementsIndexOptions Options { get; set; }

    /// <summary>
    /// Gets or sets the pager shape.
    /// </summary>
    public dynamic Pager { get; set; }
}

/// <summary>
/// The subscriptions a signed-in customer owns, shown in their own portal.
/// </summary>
public sealed class MySubscriptionsViewModel
{
    /// <summary>
    /// Gets or sets the customer's agreements on this page, most recent first.
    /// </summary>
    public IList<Subscription> Subscriptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the status shown, or <see langword="null"/> for every status.
    /// </summary>
    public SubscriptionStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the status choices.
    /// </summary>
    public IList<SelectListItem> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the pager shape.
    /// </summary>
    public dynamic Pager { get; set; }

    /// <summary>
    /// Gets or sets the current UTC time, so the view can say whether an agreement still grants access.
    /// </summary>
    public DateTime UtcNow { get; set; }
}

/// <summary>
/// A page of site provisioning jobs: the customer's own sites, or every site for an administrator.
/// </summary>
public sealed class TenantProvisioningListViewModel
{
    /// <summary>
    /// Gets or sets the jobs on this page, most recent first.
    /// </summary>
    public IList<TenantProvisioningJob> Jobs { get; set; } = [];

    /// <summary>
    /// Gets or sets the status shown, or <see langword="null"/> for every status.
    /// </summary>
    public TenantProvisioningStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the status choices.
    /// </summary>
    public IList<SelectListItem> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the pager shape.
    /// </summary>
    public dynamic Pager { get; set; }
}
