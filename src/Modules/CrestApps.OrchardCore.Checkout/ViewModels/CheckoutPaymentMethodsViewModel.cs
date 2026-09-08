namespace CrestApps.OrchardCore.Checkout.ViewModels;

/// <summary>
/// The payment methods offered on the payment step, built from the payment providers actually registered on
/// the tenant so the list can never advertise a method that cannot be executed.
/// </summary>
public class CheckoutPaymentMethodsViewModel
{
    /// <summary>
    /// Gets or sets the flow being paid.
    /// </summary>
    public CheckoutFlow Flow { get; set; }

    /// <summary>
    /// Gets or sets the provider key selected by default.
    /// </summary>
    public string SelectedProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the available methods.
    /// </summary>
    public IList<CheckoutPaymentMethodOption> Methods { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the checkout owes money. A checkout that owes nothing (a free
    /// plan, or a balance already covered) is completed without a payment method, so the page must not ask the
    /// customer to pick one.
    /// </summary>
    public bool RequiresPayment { get; set; }
}

/// <summary>
/// One selectable payment method.
/// </summary>
public sealed class CheckoutPaymentMethodOption
{
    /// <summary>
    /// Gets or sets the provider key posted back when the method is chosen.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the label shown to the customer.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets a short description of what choosing this method means.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this method actually moves money at a gateway, as opposed to
    /// recording an offline commitment to pay later.
    /// </summary>
    public bool HasProcessor { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method is preselected.
    /// </summary>
    public bool IsDefault { get; set; }
}
