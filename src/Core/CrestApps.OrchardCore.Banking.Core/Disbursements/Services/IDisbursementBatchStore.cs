using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Disbursements.Models;

namespace CrestApps.OrchardCore.Banking.Disbursements.Core.Services;

/// <summary>
/// Stores disbursement batches.
/// </summary>
public interface IDisbursementBatchStore : ICatalog<DisbursementBatch>
{
    /// <summary>
    /// Lists the batches of a provider released after a time, oldest first.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="releasedAfterUtc">The time.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The batches.</returns>
    Task<IReadOnlyList<DisbursementBatch>> ListReleasedAsync(string provider, DateTime releasedAfterUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the batches of a source account released between two times, such as the files sent today.
    /// </summary>
    /// <param name="sourceBankAccountId">The source account.</param>
    /// <param name="fromUtc">The start.</param>
    /// <param name="toUtc">The end.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count.</returns>
    Task<int> CountReleasedAsync(string sourceBankAccountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists released batches released before a time, oldest first, such as those to settle.
    /// </summary>
    /// <param name="releasedBeforeUtc">The time.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The batches.</returns>
    Task<IReadOnlyList<DisbursementBatch>> ListToSettleAsync(DateTime releasedBeforeUtc, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of batches, newest first.
    /// </summary>
    /// <param name="status">The status, or <see langword="null"/>.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count and the page.</returns>
    Task<(int Count, IReadOnlyList<DisbursementBatch> Batches)> ListPageAsync(DisbursementBatchStatus? status, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of batches the filter allows, newest first, with their count.
    /// </summary>
    /// <param name="status">The status, or <see langword="null"/> for every status.</param>
    /// <param name="access">The batches the reader may act on.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count and the page.</returns>
    Task<(int Count, IReadOnlyList<DisbursementBatch> Batches)> ListPageAsync(DisbursementBatchStatus? status, CrestApps.OrchardCore.SourceAccess.SourceAccessFilter access, int skip, int take, CancellationToken cancellationToken = default);
}
