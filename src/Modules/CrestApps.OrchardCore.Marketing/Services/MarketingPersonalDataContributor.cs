using System.Globalization;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Marketing.Services;

/// <summary>
/// Exports and anonymizes what Marketing counts about a person's purchases for its segments. Marketing keeps its
/// counts by customer record, so the person's customer records are found through the Customers building block: the one
/// linked to their account and the guest records made with their email. The counts stay, so segments and campaign
/// reports keep adding up; the email and country go.
/// </summary>
public sealed class MarketingPersonalDataContributor : IPersonalDataContributor
{
    /// <summary>The record kind of a customer's counted purchases.</summary>
    public const string PurchasesKind = "MarketingPurchases";

    private readonly ICustomerService _customers;
    private readonly ICustomerStore _customerStore;
    private readonly ICustomerPurchasesStore _purchases;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketingPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="customers">The customer service.</param>
    /// <param name="customerStore">The customer store.</param>
    /// <param name="purchases">The store of counted purchases.</param>
    public MarketingPersonalDataContributor(
        ICustomerService customers,
        ICustomerStore customerStore,
        ICustomerPurchasesStore purchases)
    {
        _customers = customers;
        _customerStore = customerStore;
        _purchases = purchases;
    }

    /// <inheritdoc/>
    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var purchases in await ListAsync(context.Subject, cancellationToken))
        {
            var record = context.Add(PurchasesKind, purchases.ItemId, purchases.Email ?? purchases.ItemId, purchases.FirstOrderUtc);

            record.Fields[nameof(CustomerPurchases.Email)] = purchases.Email;
            record.Fields[nameof(CustomerPurchases.Country)] = purchases.Country;
            record.Fields[nameof(CustomerPurchases.OrderCount)] = purchases.OrderCount.ToString(CultureInfo.InvariantCulture);
            record.Fields[nameof(CustomerPurchases.TotalSpent)] = purchases.TotalSpent.ToString(CultureInfo.InvariantCulture);
            record.Fields[nameof(CustomerPurchases.FirstOrderUtc)] = purchases.FirstOrderUtc?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(CustomerPurchases.LastOrderUtc)] = purchases.LastOrderUtc?.ToString("O", CultureInfo.InvariantCulture);
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var purchases in await ListAsync(context.Subject, cancellationToken))
        {
            purchases.Email = null;
            purchases.Country = null;

            await _purchases.UpdateAsync(purchases, cancellationToken);
            context.Result.Count(PurchasesKind);
        }
    }

    private async Task<IReadOnlyList<CustomerPurchases>> ListAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var customerIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var userId in subject.UserIds)
        {
            if (await _customers.FindForUserAsync(userId, cancellationToken) is { } customer)
            {
                customerIds.Add(customer.ItemId);
            }
        }

        foreach (var email in subject.Emails)
        {
            foreach (var guest in await _customerStore.ListGuestsByEmailAsync(email, cancellationToken))
            {
                customerIds.Add(guest.ItemId);
            }
        }

        var found = new List<CustomerPurchases>();

        foreach (var customerId in customerIds)
        {
            if (await _purchases.FindByIdAsync(customerId, cancellationToken) is { } purchases)
            {
                found.Add(purchases);
            }
        }

        return found;
    }
}
