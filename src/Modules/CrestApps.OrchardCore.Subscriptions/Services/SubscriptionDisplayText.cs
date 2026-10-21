using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Extensions;
using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// The words the subscription screens show for stored values: statuses, history events, billing intervals, and
/// payment methods. The stored values are identifiers, not text a customer or an operator should have to read.
/// </summary>
public sealed class SubscriptionDisplayText
{
    private readonly IEnumerable<ICheckoutPaymentProvider> _paymentProviders;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionDisplayText"/> class.
    /// </summary>
    /// <param name="paymentProviders">The registered payment providers, which name their own methods.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionDisplayText(
        IEnumerable<ICheckoutPaymentProvider> paymentProviders,
        IStringLocalizer<SubscriptionDisplayText> stringLocalizer)
    {
        _paymentProviders = paymentProviders;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the label for a subscription's status, the same one its badge shows.
    /// </summary>
    /// <param name="status">The status.</param>
    public string StatusName(SubscriptionStatus status)
        => status switch
        {
            SubscriptionStatus.Active => S["Active"],
            SubscriptionStatus.Trialing => S["Trial"],
            SubscriptionStatus.PastDue => S["Past due"],
            SubscriptionStatus.Canceled => S["Canceled"],
            SubscriptionStatus.Expired => S["Expired"],
            SubscriptionStatus.Paused => S["Paused"],
            SubscriptionStatus.Incomplete => S["Incomplete"],
            _ => status.ToString(),
        };

    /// <summary>
    /// Gets the label for an entry in a subscription's history.
    /// </summary>
    /// <param name="type">The event type.</param>
    public string EventTypeName(SubscriptionEventType type)
        => type switch
        {
            SubscriptionEventType.Created => S["Started"],
            SubscriptionEventType.Renewed => S["Renewed"],
            SubscriptionEventType.PaymentFailed => S["Payment failed"],
            SubscriptionEventType.Canceled => S["Canceled"],
            SubscriptionEventType.Expired => S["Expired"],
            SubscriptionEventType.Paused => S["Billing paused"],
            SubscriptionEventType.Resumed => S["Billing resumed"],
            SubscriptionEventType.Changed => S["Changed"],
            SubscriptionEventType.Note => S["Note"],
            _ => type.ToString(),
        };

    /// <summary>
    /// Gets the label for a site provisioning job's status.
    /// </summary>
    /// <param name="status">The status.</param>
    public string ProvisioningStatusName(TenantProvisioningStatus status)
        => status switch
        {
            TenantProvisioningStatus.Pending => S["Waiting"],
            TenantProvisioningStatus.Running => S["Being created"],
            TenantProvisioningStatus.Succeeded => S["Created"],
            TenantProvisioningStatus.Failed => S["Retrying"],
            TenantProvisioningStatus.Abandoned => S["Abandoned"],
            _ => status.ToString(),
        };

    /// <summary>
    /// Gets how often a subscription bills, for example "every month" or "every 3 months".
    /// </summary>
    /// <param name="count">The number of <paramref name="type"/> units in one cycle.</param>
    /// <param name="type">The unit.</param>
    public string BillingInterval(int count, DurationType type)
    {
        if (count <= 1)
        {
            return type switch
            {
                DurationType.Day => S["every day"],
                DurationType.Week => S["every week"],
                DurationType.Month => S["every month"],
                DurationType.Year => S["every year"],
                _ => type.ToString(),
            };
        }

        return type switch
        {
            DurationType.Day => S["every {0} days", count],
            DurationType.Week => S["every {0} weeks", count],
            DurationType.Month => S["every {0} months", count],
            DurationType.Year => S["every {0} years", count],
            _ => $"{count} {type}",
        };
    }

    /// <summary>
    /// Gets a recurring price with how often it bills, for example "USD 20.00 every month".
    /// </summary>
    /// <param name="amount">The amount billed each cycle.</param>
    /// <param name="currency">The ISO-4217 code of the currency the amount is in.</param>
    /// <param name="count">The number of <paramref name="type"/> units in one cycle.</param>
    /// <param name="type">The unit.</param>
    public string RecurringPrice(decimal amount, string currency, int count, DurationType type)
        => S["{0} {1}", PriceFormatting.Format(amount, currency), BillingInterval(count, type)];

    /// <summary>
    /// Gets the name of a payment method, falling back to its stored key when its provider is no longer enabled.
    /// </summary>
    /// <param name="providerKey">The stored provider key.</param>
    public string PaymentMethodName(string providerKey)
        => _paymentProviders.GetPaymentMethodName(providerKey);
}
