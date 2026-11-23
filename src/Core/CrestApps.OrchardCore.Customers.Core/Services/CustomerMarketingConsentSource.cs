using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Customers.Core.Indexes;
using CrestApps.OrchardCore.Customers.Core.Models;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// A commerce customer's consent to marketing, as a consent source, backed by <see cref="Customer.AcceptsMarketing"/>.
/// It keeps e-mail only, and the <see cref="ConsentPurposes.Marketing"/> purpose only: marketing needs the customer's
/// yes, so a customer who never said yes, or who unsubscribed, has opted out.
/// </summary>
/// <remarks>
/// <para>
/// It answers a question only when the question names a customer (<see cref="ConsentContactKinds.Customer"/>), with
/// the Marketing purpose or with no purpose (what callers that predate purposes ask): by the customer's id, or, without
/// one, by the e-mail address, when no customer at the address accepts marketing. A question that names no customer
/// is left to the address opt-outs: <c>AcceptsMarketing</c> starts out false, so answering it would stop every
/// address that once checked out without ticking the box, such as a mailing list's subscribers.
/// </para>
/// <para>
/// It records a Marketing change, and a change with no purpose that names a customer: on the customer with the given
/// id when the change names a customer, otherwise on every customer that holds the e-mail address, so an address-only
/// "stop all marketing" reaches each of a person's customer records (a guest one and an account one often share it).
/// </para>
/// </remarks>
public sealed class CustomerMarketingConsentSource : IConsentSource
{
    private readonly ICustomerStore _customers;
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerMarketingConsentSource"/> class.
    /// </summary>
    /// <param name="customers">The customer store, which finds a customer by id and saves the change.</param>
    /// <param name="session">The YesSql session the customers holding an e-mail address are found with.</param>
    /// <param name="clock">The clock.</param>
    public CustomerMarketingConsentSource(
        ICustomerStore customers,
        ISession session,
        IClock clock)
    {
        _customers = customers;
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Name
        => ConsentSourceNames.CustomerMarketing;

    /// <inheritdoc />
    public async Task<bool> HasOptedOutAsync(ConsentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!IsEmail(query.Channel) || !IsCustomer(query.ContactKind) || !IsMarketingOrEvery(query.Purpose))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(query.ContactId))
        {
            var customer = await _customers.FindByIdAsync(query.ContactId, cancellationToken);

            return customer is not null && !customer.AcceptsMarketing;
        }

        var holders = await FindByEmailAsync(query.Address, cancellationToken);

        return holders.Count > 0 && !holders.Any(customer => customer.AcceptsMarketing);
    }

    /// <inheritdoc />
    public async Task RecordAsync(string channel, string address, ConsentChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (!IsEmail(channel) || !(IsMarketing(change.Purpose) || (IsEvery(change.Purpose) && IsCustomer(change.ContactKind))))
        {
            return;
        }

        IReadOnlyList<Customer> customers;

        if (IsCustomer(change.ContactKind) && !string.IsNullOrEmpty(change.ContactId))
        {
            customers = await _customers.FindByIdAsync(change.ContactId, cancellationToken) is { } customer
                ? [customer]
                : [];
        }
        else
        {
            customers = await FindByEmailAsync(address, cancellationToken);
        }

        var accepts = !change.OptedOut;

        foreach (var customer in customers)
        {
            if (customer.AcceptsMarketing == accepts)
            {
                continue;
            }

            customer.AcceptsMarketing = accepts;
            customer.ModifiedUtc = _clock.UtcNow;

            await _customers.UpdateAsync(customer, cancellationToken);
        }
    }

    // Erased customers keep no address, and a merged guest's orders live on the account's customer.
    private async Task<IReadOnlyList<Customer>> FindByEmailAsync(string address, CancellationToken cancellationToken)
    {
        if (CustomerIndexProvider.Normalize(address) is not { } normalized)
        {
            return [];
        }

        var customers = await _session
            .Query<Customer, CustomerIndex>(index =>
                index.NormalizedEmail == normalized &&
                index.Status != CustomerStatus.Anonymized &&
                index.Status != CustomerStatus.Merged,
                collection: CustomersConstants.CollectionName)
            .ListAsync(cancellationToken);

        return customers.ToArray();
    }

    private static bool IsEmail(string channel)
        => string.Equals(channel, ConsentChannels.Email, StringComparison.Ordinal);

    private static bool IsCustomer(string kind)
        => string.Equals(kind, ConsentContactKinds.Customer, StringComparison.Ordinal);

    private static bool IsMarketing(string purpose)
        => string.Equals(purpose?.Trim(), ConsentPurposes.Marketing, StringComparison.Ordinal);

    private static bool IsEvery(string purpose)
        => string.IsNullOrWhiteSpace(purpose);

    private static bool IsMarketingOrEvery(string purpose)
        => IsEvery(purpose) || IsMarketing(purpose);
}
