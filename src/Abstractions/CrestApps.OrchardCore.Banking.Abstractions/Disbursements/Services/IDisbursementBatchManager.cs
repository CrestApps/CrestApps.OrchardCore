using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Disbursements.Models;

namespace CrestApps.OrchardCore.Banking.Disbursements.Services;

/// <summary>
/// Stores disbursement batches.
/// </summary>
public interface IDisbursementBatchManager : ICatalogManager<DisbursementBatch>
{
    /// <summary>
    /// Lists the batches released since a time, oldest first, such as those a pipeline has not delivered yet.
    /// </summary>
    /// <param name="provider">The provider, or <see langword="null"/> for every provider.</param>
    /// <param name="releasedAfterUtc">Only batches released after this time.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The batches.</returns>
    Task<IReadOnlyList<DisbursementBatch>> ListReleasedAsync(string provider, DateTime releasedAfterUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the batches of a bank account released on a day, so a bank file gets a new file id modifier.
    /// </summary>
    /// <param name="sourceBankAccountId">The bank account.</param>
    /// <param name="fromUtc">The start of the day.</param>
    /// <param name="toUtc">The end of the day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count.</returns>
    Task<int> CountReleasedAsync(string sourceBankAccountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
