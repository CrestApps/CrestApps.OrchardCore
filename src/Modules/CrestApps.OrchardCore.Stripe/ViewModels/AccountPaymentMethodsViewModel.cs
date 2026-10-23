using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Stripe.Services;

namespace CrestApps.OrchardCore.Stripe.ViewModels;

/// <summary>
/// The signed-in buyer's saved cards.
/// </summary>
public class AccountPaymentMethodsViewModel
{
    /// <summary>
    /// Gets or sets the cards the buyer saved at checkout to pay with next time.
    /// </summary>
    public IReadOnlyList<StripeSavedCard> Cards { get; set; } = [];

    /// <summary>
    /// Gets or sets the cards the buyer kept for payments taken without them, such as installments and renewals, from
    /// any provider, the default first.
    /// </summary>
    public IReadOnlyList<CustomerPaymentMethod> KeptPaymentMethods { get; set; } = [];
}
