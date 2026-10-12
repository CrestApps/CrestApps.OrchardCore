using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// The durable store for <see cref="InstallmentPlan"/> records.
/// </summary>
public interface IInstallmentPlanStore : ICatalog<InstallmentPlan>
{
    /// <summary>
    /// Returns a page of plans that match <paramref name="query"/>, most recent first.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The number of plans per page.</param>
    /// <param name="query">The filter.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PageResult<InstallmentPlan>> PageAsync(int page, int pageSize, InstallmentPlanQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the plans that still have work to do: waiting for their down payment, or collecting payments.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<InstallmentPlan>> GetOpenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Filters installment plans.
/// </summary>
public sealed class InstallmentPlanQuery
{
    /// <summary>
    /// Gets or sets the status to match.
    /// </summary>
    public InstallmentPlanStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the customer to match.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets text matched against the plan's title and the customer's name and email.
    /// </summary>
    public string Search { get; set; }
}
