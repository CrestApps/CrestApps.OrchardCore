using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// The payment methods an administrator can take a plan's down payment with, and what a browser may send them.
/// </summary>
internal static class InstallmentPlanPaymentMethods
{
    private const int MaxProviderDataEntries = 16;
    private const int MaxProviderDataKeyLength = 64;
    private const int MaxProviderDataValueLength = 512;

    // Only a provider that can keep the card, and whose card form can be shown on the page, can take a down payment
    // that the schedule then charges.
    public static IEnumerable<ICheckoutPaymentProvider> GetEligible(
        ICheckoutPaymentProviderResolver providerResolver,
        IEnumerable<ICheckoutSavedPaymentMethodProvider> savedPaymentMethodProviders)
        => providerResolver.GetProviders().Where(provider =>
            provider.Capabilities.SupportsSavedPaymentMethods &&
            provider.Capabilities.SupportsEmbeddedElements &&
            savedPaymentMethodProviders.Any(saved => string.Equals(saved.Key, provider.Key, StringComparison.OrdinalIgnoreCase)));

    // What the browser sent is bounded, and the keys that decide whether a saved card is spent are stripped: only
    // this server decides to keep a card, and nothing a browser sends may charge one without the customer.
    public static Dictionary<string, string> BuildProviderData(Dictionary<string, string> providerData, bool savePaymentMethod)
    {
        var sanitized = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in providerData ?? [])
        {
            if (string.IsNullOrEmpty(pair.Key) ||
                pair.Key.Length > MaxProviderDataKeyLength ||
                (pair.Value is not null && pair.Value.Length > MaxProviderDataValueLength) ||
                pair.Key is CheckoutPaymentDataKeys.OffSession or CheckoutPaymentDataKeys.SavedCustomerReference or CheckoutPaymentDataKeys.SavedPaymentMethodReference or CheckoutPaymentDataKeys.SavePaymentMethod)
            {
                continue;
            }

            sanitized[pair.Key] = pair.Value;

            if (sanitized.Count == MaxProviderDataEntries)
            {
                break;
            }
        }

        if (savePaymentMethod)
        {
            sanitized[CheckoutPaymentDataKeys.SavePaymentMethod] = "true";
        }

        return sanitized;
    }
}
