using CrestApps.OrchardCore.Customers.PersonalData;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Forgets the payment methods a person kept for charges made without them present when their personal data is
/// anonymized. The cards stay with the provider; only this site's references to them are removed, so they can no longer
/// be charged from here.
/// </summary>
internal sealed class SavedPaymentMethodPersonalDataContributor : ICommercePersonalDataContributor
{
    private readonly ISavedPaymentMethodStore _store;

    public SavedPaymentMethodPersonalDataContributor(ISavedPaymentMethodStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var userId in context.Subject.UserIds)
        {
            await _store.RemoveAllAsync(userId, cancellationToken);
        }
    }
}
