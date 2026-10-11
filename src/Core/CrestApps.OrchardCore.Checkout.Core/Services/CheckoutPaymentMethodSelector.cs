using CrestApps.OrchardCore.Checkout.Services;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Decides which payment providers may be offered for a given invoice, and which one is preselected.
/// </summary>
/// <remarks>
/// This is separated from the payment step's rendering because it is the rule that protects the customer from
/// a dead end: offering a method that cannot settle what the invoice owes lets them select it, submit, and
/// then wait on a payment the framework was never able to collect. Keeping it in one named place also means
/// the engine's capability check and the page's method list can never drift apart.
/// </remarks>
public static class CheckoutPaymentMethodSelector
{
    /// <summary>
    /// Returns the providers capable of settling every obligation the invoice owes.
    /// </summary>
    /// <param name="providers">The registered providers.</param>
    /// <param name="obligations">The obligations the invoice must settle.</param>
    public static ICheckoutPaymentProvider[] GetEligible(
        IEnumerable<ICheckoutPaymentProvider> providers,
        IReadOnlyList<string> obligations)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(obligations);

        return [.. providers.Where(provider => IsEligible(provider, obligations))];
    }

    /// <summary>
    /// Returns the provider to preselect: the configured default when it can settle the invoice, otherwise a
    /// real gateway in preference to an offline commitment, so the common case is one click.
    /// </summary>
    /// <param name="eligible">The providers that may be offered.</param>
    /// <param name="configuredDefault">The site's configured default provider key.</param>
    public static string ResolveDefault(ICheckoutPaymentProvider[] eligible, string configuredDefault)
    {
        ArgumentNullException.ThrowIfNull(eligible);

        if (eligible.Length == 0)
        {
            return null;
        }

        // A configured default that cannot settle this invoice is ignored rather than preselected: the
        // customer's first click would otherwise land on a method that is about to be refused.
        if (!string.IsNullOrEmpty(configuredDefault) &&
            Array.Exists(eligible, provider => string.Equals(provider.Key, configuredDefault, StringComparison.OrdinalIgnoreCase)))
        {
            return configuredDefault;
        }

        var withGateway = Array.Find(eligible, IsGateway);

        return (withGateway ?? eligible[0]).Key;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the provider collects payment through a gateway rather than
    /// recording an offline commitment.
    /// </summary>
    /// <param name="provider">The provider to inspect.</param>
    public static bool IsGateway(ICheckoutPaymentProvider provider)
        => provider.Capabilities.SupportsEmbeddedElements || provider.Capabilities.SupportsHostedCheckout;

    private static bool IsEligible(ICheckoutPaymentProvider provider, IReadOnlyList<string> obligations)
    {
        if (obligations.Count == 0)
        {
            return true;
        }

        var hasOneTime = obligations.Contains(CheckoutObligations.OneTime);
        var hasRecurring = obligations.Any(id => !string.Equals(id, CheckoutObligations.OneTime, StringComparison.Ordinal));

        if (hasOneTime && !provider.Capabilities.SupportsOneTimePayments)
        {
            return false;
        }

        if (hasRecurring && !provider.Capabilities.SupportsRecurringPayments)
        {
            return false;
        }

        // A provider that can do each separately but not together cannot settle an invoice that owes both in
        // one interaction, so it is not offered for that invoice.
        return !hasOneTime || !hasRecurring || provider.Capabilities.SupportsCombinedOneTimeAndRecurring;
    }
}
