using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Records a money operation that failed and must be finished by hand, so an operator sees it under
/// <b>Commerce › Payment issues</b> instead of only in the log. Call it from the place that logs the failure, and keep
/// logging.
/// </summary>
public interface IPaymentIssueRecorder
{
    /// <summary>
    /// Saves the issue as open, stamping its ID and time. It never throws: a failure to record is logged, so the code
    /// that is already handling a failure is not interrupted by a second one.
    /// </summary>
    /// <param name="issue">The issue to record.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the issue was saved.</returns>
    Task<bool> RecordAsync(PaymentIssue issue, CancellationToken cancellationToken = default);
}
