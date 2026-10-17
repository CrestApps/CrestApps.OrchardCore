using System.Text.Json;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.PersonalData;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Erases and exports the personal data the other commerce modules keep about a customer, through
/// <see cref="ICommercePersonalDataService"/>: their checkouts, transactions, payments, refunds, subscriptions,
/// installment plans, coupon redemptions and payment-provider customers.
/// </summary>
/// <remarks>
/// The customer is identified by their account, when they have one, and by their email, which is what ties their guest
/// checkouts to them. Names are cleared rather than replaced, the same way the customer record itself is.
/// </remarks>
public sealed class CommercePersonalDataCustomerPrivacyHandler : ICustomerPrivacyHandler
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    private readonly ICommercePersonalDataService _personalDataService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommercePersonalDataCustomerPrivacyHandler"/> class.
    /// </summary>
    /// <param name="personalDataService">The commerce personal data service.</param>
    public CommercePersonalDataCustomerPrivacyHandler(ICommercePersonalDataService personalDataService)
    {
        _personalDataService = personalDataService;
    }

    /// <inheritdoc/>
    public async Task ErasingAsync(CustomerPrivacyContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var owner = GetOwner(context.UserId);

        if (owner is null && string.IsNullOrWhiteSpace(context.Customer.Email))
        {
            return;
        }

        await _personalDataService.AnonymizeAsync(owner, context.Customer.Email, replacementText: null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ExportingAsync(CustomerExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var owner = GetOwner(context.UserId);

        if (owner is null && string.IsNullOrWhiteSpace(context.Customer.Email))
        {
            return;
        }

        var export = await _personalDataService.ExportAsync(owner, context.Customer.Email, cancellationToken);

        context.Export["commerce"] = JsonSerializer.SerializeToNode(export, _options);
    }

    private static CustomerOwner GetOwner(string userId)
        => string.IsNullOrEmpty(userId) ? null : CustomerOwner.ForUser(userId);
}
