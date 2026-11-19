using CrestApps.OrchardCore.Agreements.Models;

namespace CrestApps.OrchardCore.Agreements.Services;

/// <summary>
/// Reports the time spent under agreements, such as billable ticket time or work order labor. Each module that does
/// work under an agreement registers one; the balance adds up what every provider reports.
/// </summary>
public interface IAgreementUsageProvider
{
    /// <summary>
    /// Gets the provider's name, kept in <see cref="AgreementUsage.Source"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Lists the time spent under an agreement in a range.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="fromUtc">The start of the range, in UTC.</param>
    /// <param name="toUtc">The end of the range, in UTC, exclusive.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The entries.</returns>
    Task<IReadOnlyList<AgreementUsage>> ListAsync(Agreement agreement, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
