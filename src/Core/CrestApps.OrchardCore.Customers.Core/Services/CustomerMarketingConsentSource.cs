using CrestApps.OrchardCore.Consent;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// A commerce customer's consent to marketing, as a consent source. It answers only for e-mail and only for a customer
/// (<see cref="ConsentContactKinds.Customer"/>): marketing needs the customer's yes, so a customer who never said yes,
/// or who unsubscribed, has opted out. A change sets <c>AcceptsMarketing</c>.
/// </summary>
public sealed class CustomerMarketingConsentSource : IConsentSource
{
    private readonly ICustomerStore _customers;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerMarketingConsentSource"/> class.
    /// </summary>
    /// <param name="customers">The customer store.</param>
    /// <param name="clock">The clock.</param>
    public CustomerMarketingConsentSource(ICustomerStore customers, IClock clock)
    {
        _customers = customers;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Name
        => ConsentSourceNames.CustomerMarketing;

    /// <inheritdoc />
    public async Task<bool> HasOptedOutAsync(ConsentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!Applies(query.Channel, query.ContactKind, query.ContactId))
        {
            return false;
        }

        var customer = await _customers.FindByIdAsync(query.ContactId, cancellationToken);

        return customer is not null && !customer.AcceptsMarketing;
    }

    /// <inheritdoc />
    public async Task RecordAsync(string channel, string address, ConsentChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (!Applies(channel, change.ContactKind, change.ContactId) ||
            await _customers.FindByIdAsync(change.ContactId, cancellationToken) is not { } customer ||
            customer.AcceptsMarketing == !change.OptedOut)
        {
            return;
        }

        customer.AcceptsMarketing = !change.OptedOut;
        customer.ModifiedUtc = _clock.UtcNow;

        await _customers.UpdateAsync(customer, cancellationToken);
    }

    private static bool Applies(string channel, string kind, string contactId)
        => string.Equals(channel, ConsentChannels.Email, StringComparison.Ordinal) &&
            string.Equals(kind, ConsentContactKinds.Customer, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(contactId);
}
