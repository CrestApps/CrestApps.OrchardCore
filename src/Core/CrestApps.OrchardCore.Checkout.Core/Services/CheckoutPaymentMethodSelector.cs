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
    /// <param name="settlesExistingBalance">
    /// Whether the checkout pays off a balance that already exists. A provider that only records a promise to pay
    /// later can't do that, so it is left out.
    /// </param>
    /// <param name="savesPaymentMethod">
    /// Whether the checkout keeps the payment method for later charges without the customer present
    /// (<see cref="CheckoutSession.SavePaymentMethod"/>). A provider that can't keep one is left out.
    /// </param>
    public static ICheckoutPaymentProvider[] GetEligible(
        IEnumerable<ICheckoutPaymentProvider> providers,
        IReadOnlyList<string> obligations,
        bool settlesExistingBalance = false,
        bool savesPaymentMethod = false)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(obligations);

        return [.. providers.Where(provider =>
            (!settlesExistingBalance || !provider.Capabilities.DefersPayment) &&
            (!savesPaymentMethod || provider.Capabilities.SupportsSavedPaymentMethods) &&
            IsEligible(provider, obligations))];
    }

    /// <summary>
    /// Keeps the providers that are available for this checkout: a provider that implements
    /// <see cref="ICheckoutPaymentProviderAvailability"/> decides for itself, and any other always is.
    /// </summary>
    /// <param name="providers">The providers that can settle what the checkout owes.</param>
    /// <param name="session">The checkout being paid.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<ICheckoutPaymentProvider[]> GetAvailableAsync(IEnumerable<ICheckoutPaymentProvider> providers, ICheckoutFlowSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(session);

        var available = new List<ICheckoutPaymentProvider>();

        foreach (var provider in providers)
        {
            if (await IsAvailableAsync(provider, session, cancellationToken))
            {
                available.Add(provider);
            }
        }

        return [.. available];
    }

    /// <summary>
    /// Returns whether a provider is available for this checkout.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="session">The checkout being paid.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<bool> IsAvailableAsync(ICheckoutPaymentProvider provider, ICheckoutFlowSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return provider is not ICheckoutPaymentProviderAvailability availability ||
            await availability.IsAvailableAsync(session, cancellationToken);
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
