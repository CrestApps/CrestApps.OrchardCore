using CrestApps.OrchardCore.Stripe.Services;

namespace CrestApps.OrchardCore.Stripe.ViewModels;

/// <summary>
/// The signed-in buyer's saved cards.
/// </summary>
public class AccountPaymentMethodsViewModel
{
    public IReadOnlyList<StripeSavedCard> Cards { get; set; } = [];
}
