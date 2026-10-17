namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Decides where the checkout pages show the link back to the page that started a checkout.
/// </summary>
public static class CheckoutReturnLinks
{
    /// <summary>
    /// Gets the return information to show as a <b>Back</b> link on the step the flow is on, or
    /// <see langword="null"/> when that step shows none.
    /// </summary>
    /// <remarks>
    /// The payment step always shows it, because that is where a customer decides not to pay after all and needs a
    /// way back to where they came from. The first step shows it too, because it has no <b>Back</b> of its own.
    /// Other steps already go back to the previous step.
    /// </remarks>
    /// <param name="flow">The checkout flow being rendered.</param>
    public static CheckoutReturnInfo GetBackLink(CheckoutFlow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var returnInfo = CheckoutReturnInfo.GetFrom(flow.Session);

        if (returnInfo is null || string.IsNullOrEmpty(returnInfo.ReturnUrl))
        {
            return null;
        }

        return flow.CurrentStepEquals(CheckoutConstants.PaymentStepKey) || flow.GetPreviousStep() is null
            ? returnInfo
            : null;
    }
}
