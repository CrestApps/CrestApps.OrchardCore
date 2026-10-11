using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// Raises <see cref="IPaymentRefundHandler.RefundSucceededAsync"/> the same way from every place a refund can
/// finish, so telling the customer never depends on how it finished.
/// </summary>
public static class PaymentRefundHandlerExtensions
{
    /// <summary>
    /// Tells every handler that <paramref name="refund"/> succeeded, when it has just become succeeded. A refund that
    /// was already succeeded before this change is not reported again. A handler that throws is logged and the rest
    /// still run: the refund is already saved and must not be undone because a message could not be sent.
    /// </summary>
    /// <param name="serviceProvider">The provider the handlers are resolved from. They are resolved here rather than
    /// injected, because a handler may itself depend on the services that complete refunds.</param>
    /// <param name="refund">The refund, as saved.</param>
    /// <param name="previousStatus">The status the refund had before this change.</param>
    /// <param name="logger">The logger for handler failures.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static async Task RefundSucceededAsync(
        this IServiceProvider serviceProvider,
        PaymentRefund refund,
        RefundStatus previousStatus,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(refund);

        if (refund.Status != RefundStatus.Succeeded || previousStatus == RefundStatus.Succeeded)
        {
            return;
        }

        foreach (var handler in serviceProvider.GetServices<IPaymentRefundHandler>())
        {
            try
            {
                await handler.RefundSucceededAsync(refund, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "A refund handler '{Handler}' failed for refund '{RefundId}'; the refund itself stays recorded.", handler.GetType().Name, refund.ItemId);
            }
        }
    }
}
