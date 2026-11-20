using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Names where a customer who accepted an estimate goes to pay, when the estimate stands for something paid through
/// its own checkout, such as an order's quote: accepting the estimate then sends them to that order's pay page.
/// </summary>
public interface IEstimatePayLinkProvider
{
    /// <summary>
    /// Returns the absolute address that pays what <paramref name="estimate"/> was accepted for, or
    /// <see langword="null"/> when it is not paid that way.
    /// </summary>
    /// <param name="estimate">The accepted estimate.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<string> GetPayUrlAsync(Estimate estimate, CancellationToken cancellationToken = default);
}
