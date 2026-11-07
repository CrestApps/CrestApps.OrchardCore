using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The durable store of <see cref="PaymentIssue"/> records.
/// </summary>
public interface IPaymentIssueStore : ICatalog<PaymentIssue>
{
    /// <summary>
    /// Returns a page of issues, newest first.
    /// </summary>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The number of issues per page.</param>
    /// <param name="status">The status to list, or <see langword="null"/> for all.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<PageResult<PaymentIssue>> PageAsync(int page, int pageSize, PaymentIssueStatus? status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns how many issues are still open.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<int> CountOpenAsync(CancellationToken cancellationToken = default);
}
