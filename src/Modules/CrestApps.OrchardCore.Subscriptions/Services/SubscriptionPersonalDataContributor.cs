using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Exports and anonymizes the subscriptions a person owns, as an account holder or through the guest owners of their
/// checkouts, and adds the payment-provider customer each one bills to what identifies them.
/// </summary>
/// <remarks>
/// Anonymizing removes the guest contact and the person's emails and names from the title, the cancellation reason and
/// the history. The amounts, status, dates and the provider's references stay, so billing goes on as agreed.
/// </remarks>
public sealed class SubscriptionPersonalDataContributor : ICommercePersonalDataContributor
{
    private readonly ISubscriptionManager _subscriptionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="subscriptionManager">The subscription catalog.</param>
    public SubscriptionPersonalDataContributor(ISubscriptionManager subscriptionManager)
    {
        _subscriptionManager = subscriptionManager;
    }

    /// <inheritdoc/>
    public async Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        foreach (var subscription in await GetSubscriptionsAsync(subject, cancellationToken))
        {
            subject.AddCheckoutSession(subscription.CheckoutSessionId);
            subject.AddProviderCustomer(subscription.ProviderKey, subscription.ProviderCustomerId);

            if (subscription.OwnerKind == CustomerOwnerKind.Guest)
            {
                subject.AddName(subscription.GuestContactName);
            }
        }
    }

    /// <inheritdoc/>
    public async Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var subscription in await GetSubscriptionsAsync(context.Subject, cancellationToken))
        {
            context.Export.Subscriptions.Add(new CommerceSubscriptionRecord
            {
                ItemId = subscription.ItemId,
                Title = subscription.Title,
                Owner = subscription.OwnerKind == CustomerOwnerKind.Guest
                    ? CustomerOwner.ForGuest(subscription.OwnerId)
                    : CustomerOwner.ForUser(subscription.OwnerId),
                CheckoutSessionId = subscription.CheckoutSessionId,
                ProviderKey = subscription.ProviderKey,
                ProviderSubscriptionId = subscription.ProviderSubscriptionId,
                ProviderCustomerId = subscription.ProviderCustomerId,
                Status = subscription.Status.ToString(),
                Currency = subscription.Currency,
                Amount = subscription.Amount,
                TaxAmount = subscription.TaxAmount,
                CreatedUtc = subscription.CreatedUtc,
                CanceledUtc = subscription.CanceledUtc,
                ContactName = subscription.GuestContactName,
                ContactEmail = subscription.GuestContactEmail,
            });
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var subscription in await GetSubscriptionsAsync(context.Subject, cancellationToken))
        {
            subscription.GuestContactName = context.ReplaceName(subscription.GuestContactName);
            subscription.GuestContactEmail = null;
            subscription.Title = context.Scrub(subscription.Title);
            subscription.CancellationReason = context.Scrub(subscription.CancellationReason);

            foreach (var item in subscription.Events ?? [])
            {
                item.Message = context.Scrub(item.Message);
            }

            await _subscriptionManager.UpdateAsync(subscription, data: null, cancellationToken);

            context.Result.Subscriptions++;
        }
    }

    private async Task<IReadOnlyList<Subscription>> GetSubscriptionsAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var subscriptions = new Dictionary<string, Subscription>(StringComparer.Ordinal);

        foreach (var owner in subject.Owners.ToArray())
        {
            // A guest's owner id is a checkout session id and a user's is a user id, so the two never collide, but the
            // kind is still checked rather than assumed.
            foreach (var subscription in await _subscriptionManager.GetByOwnerAsync(owner.Id, cancellationToken))
            {
                if (subscription.OwnerKind == owner.Kind)
                {
                    subscriptions.TryAdd(subscription.ItemId, subscription);
                }
            }
        }

        return [.. subscriptions.Values.OrderBy(subscription => subscription.CreatedUtc)];
    }
}
