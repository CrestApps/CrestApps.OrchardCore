using CrestApps.OrchardCore.Banking.Disbursements.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Services;

/// <summary>
/// Sends a batch of disbursements one way: a bank file, checks, a payment provider's payouts, or by hand. Register one
/// with <see cref="DisbursementsServiceCollectionExtensions.AddDisbursementProvider{TProvider}"/>.
/// </summary>
/// <remarks>
/// <see cref="SubmitAsync"/> is called once per batch: the batch is persisted as Releasing before the call, so a
/// provider that talks to an API uses the batch id (or each disbursement's idempotency key) as its idempotency key.
/// </remarks>
public interface IDisbursementProvider
{
    /// <summary>
    /// Gets the provider's name, stored on the batches and disbursements it sends.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name shown to the person who chooses how to pay.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Gets the method of the disbursements the provider sends, from <see cref="DisbursementsConstants.Methods"/>.
    /// </summary>
    string Method { get; }

    /// <summary>
    /// Sends a batch.
    /// </summary>
    /// <param name="batch">The batch.</param>
    /// <param name="disbursements">The batch's disbursements.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What the provider did.</returns>
    Task<DisbursementSubmitResult> SubmitAsync(DisbursementBatch batch, IReadOnlyList<Disbursement> disbursements, CancellationToken cancellationToken = default);
}
