using CrestApps.OrchardCore.Agreements.Models;

namespace CrestApps.OrchardCore.Agreements.Services;

/// <summary>
/// Hears about agreements: when one is about to end, and when one passes a share of its hours.
/// </summary>
public interface IAgreementObserver
{
    /// <summary>
    /// Called once, when an agreement is within its notice of its end.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task OnExpiringAsync(Agreement agreement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called once a period, when an agreement's used hours pass a share of what the period covers (80% and 100%).
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="percent">The share passed, in percent.</param>
    /// <param name="balance">How the hours stand.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task OnHoursUsedAsync(Agreement agreement, int percent, AgreementBalance balance, CancellationToken cancellationToken = default);
}
